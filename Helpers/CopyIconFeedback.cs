// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Diagnostics;
using System.Numerics;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Plays the "copied!" feedback on an icon button: the copy glyph shrinks away, a checkmark
/// bounces in over it, holds, then hands back to the copy glyph.
/// </summary>
/// <remarks>
/// <para>
/// The button's content must be a panel whose first two children are the copy glyph and then the
/// checkmark, stacked on top of each other, with the checkmark starting at <c>Opacity="0"</c>.
/// Nothing else is asked of the markup: the glyphs are found by position rather than by name, so
/// the same button works inside a <see cref="DataTemplate"/> where no name is addressable.
/// </para>
/// <para>
/// The whole cycle is composition animations running on the compositor thread, and the hold is a
/// pair of flat keyframes rather than a timer - so there is nothing to leak and no second
/// callback that can race with a later copy. Starting an animation replaces whatever was running
/// on that property, which is what lets an interrupted cycle pick up mid-flight instead of
/// snapping.
/// </para>
/// <para>
/// Hold one instance per page. It remembers the row currently showing a checkmark, so copying a
/// second row hands the first one back instead of leaving two ticks on screen.
/// </para>
/// </remarks>
internal sealed class CopyIconFeedback
{
    // The cycle, in milliseconds from the click. The incoming glyph overlaps the outgoing one, so
    // the swap reads as a hand-off rather than as two separate fades:
    //
    //      0   copy glyph starts shrinking away
    //    100   checkmark starts bouncing in
    //    300   checkmark is up; the hold starts
    //   2300   checkmark starts shrinking away
    //   2400   copy glyph starts fading back
    //   2600   back at rest
    private const double FadeOutMs = 150;
    private const double FadeInMs = 200;
    private const double OverlapMs = 100;
    private const double HoldMs = 2000;

    private const double CheckUpMs = OverlapMs + FadeInMs;
    private const double CheckLeavesMs = CheckUpMs + HoldMs;
    private const double CheckGoneMs = CheckLeavesMs + FadeOutMs;
    private const double CopyReturnsMs = CheckLeavesMs + OverlapMs;
    private const double CycleMs = CopyReturnsMs + FadeInMs;

    /// <summary>Length of the shortened hand-back played when another row steals the checkmark.</summary>
    private const double ReturnMs = OverlapMs + FadeInMs;

    /// <summary>Scale a glyph shrinks to when it steps aside.</summary>
    private const float TuckedScale = 0.8f;

    /// <summary>Scale the checkmark enters from, pulled in past <see cref="TuckedScale"/> so the
    /// overshoot has something to travel.</summary>
    private const float EnteringScale = 0.6f;

    /// <summary>Reads the property's live value, so a keyframe can pin it rather than retarget it.</summary>
    private const string LiveValue = "this.StartingValue";

    /// <summary>The glyphs of the button currently showing a checkmark, if any.</summary>
    private (FrameworkElement Copy, FrameworkElement Check, Action? OnSettled)? _shown;

    /// <summary>Bumped whenever a cycle is superseded, so a stale completion can't settle the row
    /// a later copy now owns.</summary>
    private int _generation;

    /// <summary>
    /// Swaps <paramref name="button"/>'s copy glyph for a checkmark, holds it, and brings the copy
    /// glyph back. Does nothing if the button's content isn't shaped as described on the class.
    /// </summary>
    /// <param name="button">The icon button that was just clicked.</param>
    /// <param name="onSettled">Raised once the checkmark is on its way out, whether the cycle ran
    /// its course or a later copy cut it short. Use it to undo anything set alongside the copy,
    /// such as a "copied!" tooltip.</param>
    public void Play(Button button, Action? onSettled = null)
    {
        if (button.Content is not Panel { Children: [FrameworkElement copyIcon, FrameworkElement checkIcon, ..] })
        {
            return;
        }

        // Copying a second row while the first still shows its checkmark would leave two ticks on
        // screen, so hand that row back before starting this one.
        Settle();

        // The feedback is decoration. The text is already on the clipboard by the time we get
        // here, so a failure to animate is not worth taking the app down for.
        try
        {
            // A XAML Opacity and its visual's Opacity are separate values that multiply, so the
            // markup's Opacity="0" would pin the checkmark at nothing however hard we animated the
            // visual. Open the XAML shutter and let composition own the glyph from here. Nothing
            // below writes the XAML property back, so re-opening it on every copy is a no-op.
            checkIcon.Opacity = 1;

            Visual copy = Prepare(copyIcon);
            Visual check = Prepare(checkIcon);
            Compositor compositor = copy.Compositor;

            CompositionEasingFunction ease = EaseOut(compositor);
            CompositionEasingFunction bounce = CompositionEasingFunction.CreateBackEasingFunction(
                compositor, CompositionEasingFunctionMode.Out, amplitude: 0.3f);

            int generation = ++_generation;
            CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            // The copy glyph has no keyframe at progress 0, so it leaves from wherever it
            // currently sits and an interrupted cycle resumes rather than jumps.
            Fade(copy, CycleMs, (FadeOutMs, 0f, ease), (CopyReturnsMs, 0f, ease), (CycleMs, 1f, ease));
            Scale(copy, CycleMs, (FadeOutMs, TuckedScale, ease), (CopyReturnsMs, TuckedScale, ease), (CycleMs, 1f, ease));

            // The checkmark does pin progress 0, both because the shutter above may just have
            // opened on a glyph the compositor still holds at full opacity, and so the bounce
            // always travels the same distance.
            Fade(check, CycleMs, (0, 0f, ease), (OverlapMs, 0f, ease), (CheckUpMs, 1f, bounce), (CheckLeavesMs, 1f, ease), (CheckGoneMs, 0f, ease));
            Scale(check, CycleMs, (0, EnteringScale, ease), (OverlapMs, EnteringScale, ease), (CheckUpMs, 1f, bounce), (CheckLeavesMs, 1f, ease), (CheckGoneMs, TuckedScale, ease));

            batch.Completed += (_, _) =>
            {
                // Replacing an animation also completes its batch, so ignore a batch that a later
                // copy has already superseded - that copy owns the row now.
                if (_generation != generation)
                {
                    return;
                }

                _shown = null;
                onSettled?.Invoke();
            };
            batch.End();

            _shown = (copyIcon, checkIcon, onSettled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Copy feedback animation error: {ex}");
            onSettled?.Invoke();
        }
    }

    /// <summary>
    /// Hands the row that is showing a checkmark back to its copy glyph, on the same overlap the
    /// tail of a full cycle uses.
    /// </summary>
    private void Settle()
    {
        if (_shown is not (FrameworkElement copyIcon, FrameworkElement checkIcon, var onSettled))
        {
            return;
        }

        _shown = null;
        _generation++;

        try
        {
            Visual copy = Prepare(copyIcon);
            Visual check = Prepare(checkIcon);
            CompositionEasingFunction ease = EaseOut(copy.Compositor);

            Fade(check, ReturnMs, (FadeOutMs, 0f, ease));
            Scale(check, ReturnMs, (FadeOutMs, TuckedScale, ease));

            // The copy glyph holds its live value through the overlap rather than a literal 0, so
            // settling a row that already finished on its own is a no-op instead of a flicker.
            Fade(copy, ReturnMs, (OverlapMs, null, ease), (ReturnMs, 1f, ease));
            Scale(copy, ReturnMs, (OverlapMs, null, ease), (ReturnMs, 1f, ease));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Copy feedback animation error: {ex}");
        }

        onSettled?.Invoke();
    }

    /// <summary>The everyday easing, matching the <c>CubicEase</c> the page styles use elsewhere:
    /// a cubic is a power curve of 3.</summary>
    private static CompositionEasingFunction EaseOut(Compositor compositor) =>
        CompositionEasingFunction.CreatePowerEasingFunction(compositor, CompositionEasingFunctionMode.Out, power: 3f);

    /// <summary>
    /// Hands back the element's composition visual, scaling about its centre. The centre is taken
    /// afresh each time so a font-size or scaling change is picked up.
    /// </summary>
    private static Visual Prepare(FrameworkElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(element.ActualSize / 2f, 0f);
        return visual;
    }

    /// <summary>Animates opacity across <paramref name="frames"/>, timed as offsets in milliseconds
    /// from the start of a <paramref name="lengthMs"/> timeline. A null value holds the live one.</summary>
    private static void Fade(Visual visual, double lengthMs, params (double AtMs, float? To, CompositionEasingFunction Ease)[] frames)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = TimeSpan.FromMilliseconds(lengthMs);

        foreach ((double atMs, float? to, CompositionEasingFunction ease) in frames)
        {
            if (to is float opacity)
            {
                animation.InsertKeyFrame((float)(atMs / lengthMs), opacity, ease);
            }
            else
            {
                animation.InsertExpressionKeyFrame((float)(atMs / lengthMs), LiveValue);
            }
        }

        visual.StartAnimation("Opacity", animation);
    }

    /// <inheritdoc cref="Fade"/>
    private static void Scale(Visual visual, double lengthMs, params (double AtMs, float? To, CompositionEasingFunction Ease)[] frames)
    {
        Vector3KeyFrameAnimation animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = TimeSpan.FromMilliseconds(lengthMs);

        foreach ((double atMs, float? to, CompositionEasingFunction ease) in frames)
        {
            if (to is float scale)
            {
                animation.InsertKeyFrame((float)(atMs / lengthMs), new Vector3(scale, scale, 1f), ease);
            }
            else
            {
                animation.InsertExpressionKeyFrame((float)(atMs / lengthMs), LiveValue);
            }
        }

        visual.StartAnimation("Scale", animation);
    }
}
