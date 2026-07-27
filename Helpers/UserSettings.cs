// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.Diagnostics;
using Windows.Storage;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Stores the handful of preferences the settings page owns, so they survive a restart.
/// </summary>
/// <remarks>
/// Local settings needs package identity, which an unpackaged run doesn't have. Forgetting a
/// preference is not worth failing a settings change over, so every path here shrugs the failure
/// off and falls back to the caller's default.
/// </remarks>
internal static class UserSettings
{
    internal static string? ReadString(string key)
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[key] as string;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read the '{key}' preference: {ex}");
            return null;
        }
    }

    internal static bool ReadBool(string key, bool fallback)
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[key] as bool? ?? fallback;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read the '{key}' preference: {ex}");
            return fallback;
        }
    }

    internal static void Write(string key, object value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not store the '{key}' preference: {ex}");
        }
    }
}
