// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Diagnostics;
using Windows.Foundation;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Plays the "copied!" feedback on an icon button: the copy glyph shrinks away, a checkmark
/// bounces in over it, holds, then hands back to the copy glyph.
/// </summary>
/// <remarks>
/// <para>
/// The button's content must be a panel whose first two children are the copy glyph and then the
/// checkmark, stacked on top of each other, with the checkmark starting at <c>Opacity="0"</c>.
/// Nothing else is asked of the markup: the glyphs are found by position rather than by name, and
/// the storyboard is given the elements themselves rather than names, so the same button works
/// inside a <see cref="DataTemplate"/> where no name is addressable.
/// </para>
/// <para>
/// The hold is a pair of flat keyframes in one timeline rather than a timer, so there is nothing
/// to leak and no second callback that can race with a later copy.
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

    /// <summary>Scale a glyph shrinks to when it steps aside.</summary>
    private const double TuckedScale = 0.8;

    /// <summary>Scale the checkmark enters from, pulled in past <see cref="TuckedScale"/> so the
    /// overshoot has something to travel.</summary>
    private const double EnteringScale = 0.6;

    private static readonly Point Centre = new(0.5, 0.5);

    /// <summary>The row currently showing a checkmark, and the timeline driving it.</summary>
    private (Storyboard Storyboard, Action? OnSettled)? _shown;

    /// <summary>
    /// Swaps <paramref name="button"/>'s copy glyph for a checkmark, holds it, and brings the copy
    /// glyph back. Does nothing if the button's content isn't shaped as described on the class.
    /// </summary>
    /// <param name="button">The icon button that was just clicked.</param>
    /// <param name="onSettled">Raised once the checkmark is gone, whether the cycle ran its course
    /// or a later copy cut it short. Use it to undo anything set alongside the copy, such as a
    /// "copied!" tooltip.</param>
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
            CompositeTransform copyScale = CentreForScaling(copyIcon);
            CompositeTransform checkScale = CentreForScaling(checkIcon);

            CubicEase ease = new() { EasingMode = EasingMode.EaseOut };
            BackEase bounce = new() { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };

            Storyboard storyboard = new();

            // The copy glyph has no keyframe at 0, so it leaves from wherever it currently sits and
            // an interrupted cycle resumes rather than jumps. The flat middle keyframe is the hold.
            Add(storyboard, copyIcon, OpacityPath, (FadeOutMs, 0, ease), (CopyReturnsMs, 0, null), (CycleMs, 1, ease));
            Add(storyboard, copyScale, ScaleXPath, (FadeOutMs, TuckedScale, ease), (CopyReturnsMs, TuckedScale, null), (CycleMs, 1, ease));
            Add(storyboard, copyScale, ScaleYPath, (FadeOutMs, TuckedScale, ease), (CopyReturnsMs, TuckedScale, null), (CycleMs, 1, ease));

            // The checkmark does pin 0, so the bounce always travels the same distance.
            Add(storyboard, checkIcon, OpacityPath, (0, 0, null), (OverlapMs, 0, null), (CheckUpMs, 1, bounce), (CheckLeavesMs, 1, null), (CheckGoneMs, 0, ease));
            Add(storyboard, checkScale, ScaleXPath, (0, EnteringScale, null), (OverlapMs, EnteringScale, null), (CheckUpMs, 1, bounce), (CheckLeavesMs, 1, null), (CheckGoneMs, TuckedScale, ease));
            Add(storyboard, checkScale, ScaleYPath, (0, EnteringScale, null), (OverlapMs, EnteringScale, null), (CheckUpMs, 1, bounce), (CheckLeavesMs, 1, null), (CheckGoneMs, TuckedScale, ease));

            storyboard.Completed += (_, _) =>
            {
                // Stopping hands every animated property back to its markup value, which by now is
                // where the cycle has already left them: the copy glyph up, the checkmark gone.
                storyboard.Stop();

                if (_shown?.Storyboard == storyboard)
                {
                    _shown = null;
                    onSettled?.Invoke();
                }
            };

            storyboard.Begin();
            _shown = (storyboard, onSettled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Copy feedback animation error: {ex}");
            onSettled?.Invoke();
        }
    }

    /// <summary>
    /// Hands the row that is showing a checkmark straight back to its copy glyph.
    /// </summary>
    /// <remarks>
    /// Stopping a storyboard reverts what it animated to the value in the markup - copy glyph
    /// visible at full size, checkmark at <c>Opacity="0"</c> - which is exactly the resting state,
    /// so no return leg has to be animated. It lands at once rather than fading, which is what we
    /// want here: attention has already moved to the row that was just copied.
    /// </remarks>
    private void Settle()
    {
        if (_shown is not (Storyboard storyboard, var onSettled))
        {
            return;
        }

        _shown = null;
        storyboard.Stop();
        onSettled?.Invoke();
    }

    /// <summary>Gives the element a <see cref="CompositeTransform"/> that scales about its centre.</summary>
    private static CompositeTransform CentreForScaling(FrameworkElement element)
    {
        if (element.RenderTransform is not CompositeTransform transform)
        {
            transform = new CompositeTransform();
            element.RenderTransform = transform;
        }

        element.RenderTransformOrigin = Centre;
        return transform;
    }

    private const string OpacityPath = "Opacity";
    private const string ScaleXPath = "ScaleX";
    private const string ScaleYPath = "ScaleY";

    /// <summary>
    /// Adds one property's whole timeline, as offsets in milliseconds from the click. A null easing
    /// interpolates linearly, which is all a flat hold segment needs.
    /// </summary>
    private static void Add(
        Storyboard storyboard,
        DependencyObject target,
        string property,
        params (double AtMs, double To, EasingFunctionBase? Easing)[] frames)
    {
        DoubleAnimationUsingKeyFrames animation = new();

        foreach ((double atMs, double to, EasingFunctionBase? easing) in frames)
        {
            animation.KeyFrames.Add(new EasingDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(atMs)),
                Value = to,
                EasingFunction = easing,
            });
        }

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }
}
