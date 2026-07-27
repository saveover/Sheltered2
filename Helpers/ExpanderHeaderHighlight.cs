// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Lights the whole header row of an <see cref="Expander"/> while the pointer is over it.
/// </summary>
/// <remarks>
/// <para>
/// The stock Expander doesn't do this. Its header is a full-width ToggleButton, but the PointerOver
/// state in <c>ExpanderHeaderDownStyle</c> only animates the foreground, the border brush and the
/// chevron's own background - so all that lights up is the little box around the chevron. There is
/// no <c>ExpanderHeaderBackgroundPointerOver</c> resource to override, and the header style can't
/// be replaced piecemeal because the template resolves it inside WinUI's own dictionary.
/// </para>
/// <para>
/// Both brushes are properties rather than resource lookups on purpose. Set with
/// <c>{ThemeResource}</c> in markup, the framework re-resolves them when the theme changes; a
/// lookup from code would read Application.Current.Resources, which answers for the system theme
/// rather than the one the user picked in settings.
/// </para>
/// </remarks>
internal static class ExpanderHeaderHighlight
{
    /// <summary>Header background while the pointer is over it.</summary>
    public static readonly DependencyProperty HoverBrushProperty = DependencyProperty.RegisterAttached(
        "HoverBrush", typeof(Brush), typeof(ExpanderHeaderHighlight), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>Header background the rest of the time. Should match <c>ExpanderHeaderBackground</c>.</summary>
    public static readonly DependencyProperty RestBrushProperty = DependencyProperty.RegisterAttached(
        "RestBrush", typeof(Brush), typeof(ExpanderHeaderHighlight), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>The header once the template has produced it, so a later brush change can repaint
    /// it without hunting through the tree again.</summary>
    private static readonly DependencyProperty HeaderProperty = DependencyProperty.RegisterAttached(
        "Header", typeof(ToggleButton), typeof(ExpanderHeaderHighlight), new PropertyMetadata(null));

    private static readonly DependencyProperty IsPointerOverProperty = DependencyProperty.RegisterAttached(
        "IsPointerOver", typeof(bool), typeof(ExpanderHeaderHighlight), new PropertyMetadata(false));

    public static Brush? GetHoverBrush(DependencyObject element) => (Brush?)element.GetValue(HoverBrushProperty);
    public static void SetHoverBrush(DependencyObject element, Brush? value) => element.SetValue(HoverBrushProperty, value);

    public static Brush? GetRestBrush(DependencyObject element) => (Brush?)element.GetValue(RestBrushProperty);
    public static void SetRestBrush(DependencyObject element, Brush? value) => element.SetValue(RestBrushProperty, value);

    private static void OnBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not Expander expander)
        {
            return;
        }

        // Already hooked, so this is a theme change re-resolving the ThemeResource. The background
        // we painted is now the previous theme's brush, so repaint with the replacement.
        if (expander.GetValue(HeaderProperty) is ToggleButton header)
        {
            Repaint(expander, header);
            return;
        }

        expander.Loaded += OnExpanderLoaded;
    }

    /// <summary>
    /// The header only exists once the template has been applied, which Loaded guarantees.
    /// </summary>
    private static void OnExpanderLoaded(object sender, RoutedEventArgs args)
    {
        Expander expander = (Expander)sender;

        // Runs once ever: a cached page raises Loaded again each time it is navigated back to, and
        // the header outlives that, so re-hooking would only stack handlers.
        expander.Loaded -= OnExpanderLoaded;

        if (FindHeader(expander) is not ToggleButton header)
        {
            return;
        }

        expander.SetValue(HeaderProperty, header);
        Repaint(expander, header);

        header.PointerEntered += (_, _) => SetPointerOver(expander, header, true);
        header.PointerExited += (_, _) => SetPointerOver(expander, header, false);

        // Clicking can collapse the row out from under the pointer without an exit following,
        // which would otherwise leave the header lit.
        header.PointerCaptureLost += (_, _) => SetPointerOver(expander, header, false);
    }

    private static void SetPointerOver(Expander expander, ToggleButton header, bool isOver)
    {
        expander.SetValue(IsPointerOverProperty, isOver);
        Repaint(expander, header);
    }

    private static void Repaint(Expander expander, ToggleButton header) =>
        header.Background = (bool)expander.GetValue(IsPointerOverProperty)
            ? GetHoverBrush(expander)
            : GetRestBrush(expander);

    private static ToggleButton? FindHeader(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);

        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);

            if (child is ToggleButton header)
            {
                return header;
            }

            if (FindHeader(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
