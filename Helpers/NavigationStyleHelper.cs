// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Persists only the navigation preference while leaving control ownership in MainWindow. This
/// avoids storing XAML elements in a static helper and keeps startup and live changes on one path.
/// </summary>
internal static class NavigationStyleHelper
{
    private const string TopStyleSettingKey = "NavigationStyleIsTop";

    private static bool _isTopStyle;

    /// <summary>
    /// Applying before persistence ensures the current window and the next launch cannot disagree
    /// after a successful assignment.
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

    /// <summary>Defers restoration until the startup window owns the controls that must move.</summary>
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
