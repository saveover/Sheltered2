// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Keeps live theme switching and persistence on the window root, the only WinUI theme scope that
/// remains writable after application startup.
/// </summary>
/// <remarks>
/// The theme is set on the window's root element rather than on <see cref="Application"/>:
/// <see cref="Application.RequestedTheme"/> can only be assigned before the first window exists,
/// so it can't be changed from a settings page. <see cref="FrameworkElement.RequestedTheme"/> can,
/// and everything below the root inherits it.
/// </remarks>
internal static class ThemeHelper
{
    private const string ThemeSettingKey = "AppTheme";

    /// <summary>
    /// The theme currently applied to the window, or <see cref="ElementTheme.Default"/> to follow
    /// the system. Assigning also stores the choice for the next launch.
    /// </summary>
    internal static ElementTheme RootTheme
    {
        get => RootElement?.RequestedTheme ?? ElementTheme.Default;
        set
        {
            if (RootElement is { } root)
            {
                root.RequestedTheme = value;
            }

            Store(value);
        }
    }

    /// <summary>Defers restoration until a root exists so the stored choice reaches the visual tree.</summary>
    internal static void Initialize()
    {
        if (RootElement is { } root)
        {
            root.RequestedTheme = Restore();
        }
    }

    private static void Store(ElementTheme theme) => UserSettings.Write(ThemeSettingKey, theme.ToString());

    private static ElementTheme Restore() =>
        Enum.TryParse(UserSettings.ReadString(ThemeSettingKey), out ElementTheme theme)
            ? theme
            : ElementTheme.Default;

    /// <summary>
    /// Re-applies the severity colouring of every <see cref="InfoBar"/> under <paramref name="root"/>
    /// against the theme now in force.
    /// </summary>
    /// <remarks>
    /// InfoBar paints its severity background from a <see cref="VisualState"/> setter, and a
    /// ThemeResource inside a setter resolves when the state is entered rather than re-resolving
    /// when the theme changes. A cached page sits off the visual tree while the theme is switched,
    /// so it misses the change and comes back with the old theme's colour baked in - a light-mode
    /// warning strip on a dark page. Nudging Severity re-enters the state and resolves the brush
    /// again. The intermediate value never reaches the screen: both writes land in one tick.
    /// </remarks>
    internal static void RefreshInfoBars(DependencyObject root)
    {
        foreach (InfoBar bar in root.FindDescendants().OfType<InfoBar>())
        {
            InfoBarSeverity severity = bar.Severity;

            bar.Severity = severity == InfoBarSeverity.Informational
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Informational;
            bar.Severity = severity;
        }
    }

    private static FrameworkElement? RootElement => App.StartupWindow?.Content as FrameworkElement;

}
