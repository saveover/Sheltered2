// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Activates while <see cref="TargetElement"/> is at least <see cref="MinWidth"/> wide.
/// </summary>
/// <remarks>
/// <para>
/// The stand-in for <see cref="AdaptiveTrigger"/>, which measures the window. The window is the
/// wrong number for a page in this app: NavigationView's pane takes 48px collapsed and 320px
/// expanded, so a breakpoint written as "1400" is really asking for anywhere between 1080 and
/// 1350 of usable width depending on a pane the page can't see. Worse, the pane opens and closes
/// without the window resizing, which an AdaptiveTrigger never notices at all.
/// </para>
/// <para>
/// Point this at the page and the number means what it says, and the layout reflows when the pane
/// moves.
/// </para>
/// <para>
/// Unlike <see cref="AdaptiveTrigger"/>, give every state a half-open range: where more than one
/// state in a group qualifies, the first one declared wins, so open-ended thresholds would pin the
/// page to its narrowest layout for ever. Ranges that don't overlap leave exactly one state active
/// and take declaration order out of it.
/// </para>
/// </remarks>
public sealed class ControlSizeTrigger : StateTriggerBase
{
    /// <summary>Width, in effective pixels, at or above which this state applies.</summary>
    public static readonly DependencyProperty MinWidthProperty = DependencyProperty.Register(
        nameof(MinWidth),
        typeof(double),
        typeof(ControlSizeTrigger),
        new PropertyMetadata(0d, (d, _) => ((ControlSizeTrigger)d).Evaluate()));

    /// <summary>Width below which this state applies; the upper end is exclusive, so it can be the
    /// next state's <see cref="MinWidth"/> without the two overlapping.</summary>
    public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.Register(
        nameof(MaxWidth),
        typeof(double),
        typeof(ControlSizeTrigger),
        new PropertyMetadata(double.PositiveInfinity, (d, _) => ((ControlSizeTrigger)d).Evaluate()));

    /// <summary>The element whose width decides the state - normally the page itself.</summary>
    public static readonly DependencyProperty TargetElementProperty = DependencyProperty.Register(
        nameof(TargetElement),
        typeof(FrameworkElement),
        typeof(ControlSizeTrigger),
        new PropertyMetadata(null, OnTargetElementChanged));

    /// <inheritdoc cref="MinWidthProperty"/>
    public double MinWidth
    {
        get => (double)GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    /// <inheritdoc cref="MaxWidthProperty"/>
    public double MaxWidth
    {
        get => (double)GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    /// <inheritdoc cref="TargetElementProperty"/>
    public FrameworkElement? TargetElement
    {
        get => (FrameworkElement?)GetValue(TargetElementProperty);
        set => SetValue(TargetElementProperty, value);
    }

    private static void OnTargetElementChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ControlSizeTrigger trigger = (ControlSizeTrigger)sender;

        if (args.OldValue is FrameworkElement previous)
        {
            previous.SizeChanged -= trigger.OnTargetSizeChanged;
        }

        if (args.NewValue is FrameworkElement current)
        {
            current.SizeChanged += trigger.OnTargetSizeChanged;
        }

        trigger.Evaluate();
    }

    private void OnTargetSizeChanged(object sender, SizeChangedEventArgs args) => Evaluate();

    // Before the first layout pass the target measures zero, so only the MinWidth="0" state
    // qualifies and the page starts narrow - the same way an AdaptiveTrigger page starts.
    private void Evaluate() =>
        SetActive(TargetElement is { } target && target.ActualWidth >= MinWidth && target.ActualWidth < MaxWidth);
}
