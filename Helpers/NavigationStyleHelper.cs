// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Whether the navigation items sit down the left edge or across the top, remembered between runs.
/// </summary>
internal static class NavigationStyleHelper
{
    private const string TopStyleSettingKey = "NavigationStyleIsTop";

    private static bool _isTopStyle;

    /// <summary>
    /// True when the items run across the top of the window. Assigning moves them and stores the
    /// choice for the next launch.
    /// </summary>
    internal static bool IsTopStyle
    {
        get => _isTopStyle;
        set
        {
            _isTopStyle = value;
            Apply();
            UserSettings.Write(TopStyleSettingKey, value);
        }
    }

    /// <summary>Applies the stored preference. Call once the startup window exists.</summary>
    internal static void Initialize()
    {
        _isTopStyle = UserSettings.ReadBool(TopStyleSettingKey, false);
        Apply();
    }

    /// <summary>
    /// The window owns the controls this moves, so it does the work; this type only decides which
    /// way round they go.
    /// </summary>
    private static void Apply()
    {
        if (App.StartupWindow is MainWindow window)
        {
            window.ApplyNavigationStyle(_isTopStyle);
        }
    }
}
