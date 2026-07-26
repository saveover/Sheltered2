// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using Windows.Storage;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Reads and writes the app's light/dark preference, and remembers it between runs.
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

    /// <summary>Applies the stored preference. Call once the startup window has its content.</summary>
    internal static void Initialize()
    {
        if (RootElement is { } root)
        {
            root.RequestedTheme = Restore();
        }
    }

    private static FrameworkElement? RootElement => App.StartupWindow?.Content as FrameworkElement;

    /// <summary>
    /// Local settings needs package identity, which an unpackaged run doesn't have. Losing the
    /// preference is not worth failing a theme switch over, so both directions shrug it off.
    /// </summary>
    private static void Store(ElementTheme theme)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] = theme.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not store the theme preference: {ex}");
        }
    }

    /// <inheritdoc cref="Store"/>
    private static ElementTheme Restore()
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] is string stored
                && Enum.TryParse(stored, out ElementTheme theme)
                    ? theme
                    : ElementTheme.Default;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read the theme preference: {ex}");
            return ElementTheme.Default;
        }
    }
}
