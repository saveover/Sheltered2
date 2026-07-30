// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.Windows.Storage;
using System;
using System.Diagnostics;
using System.Threading;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Stores the handful of preferences the settings page owns, so they survive a restart.
/// </summary>
internal static class UserSettings
{
    private static readonly Lock SettingsLock = new();
    private static ApplicationData? _settings;

    internal static string? ReadString(string key) => TryRead(key, out string? value) ? value : null;

    internal static bool ReadBool(string key, bool fallback) => TryRead(key, out bool value) ? value : fallback;

    internal static int ReadInt32(string key, int fallback) => TryRead(key, out int value) ? value : fallback;

    internal static void Write(string key, object value)
    {
        try
        {
            if (GetSettings() is { } settings)
            {
                settings.LocalSettings.Values[key] = value;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not store the '{key}' preference: {ex}");
        }
    }

    internal static void Remove(string key)
    {
        try
        {
            _ = (GetSettings()?.LocalSettings.Values.Remove(key));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not remove the '{key}' preference: {ex}");
        }
    }

    private static bool TryRead<T>(string key, out T? value)
    {
        try
        {
            if (GetSettings()?.LocalSettings.Values.TryGetValue(key, out object? storedValue) == true &&
                storedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read the '{key}' preference: {ex}");
        }

        value = default;
        return false;
    }

    private static ApplicationData? GetSettings()
    {
        lock (SettingsLock)
        {
            return _settings ??= CreateApplicationData();
        }
    }

    private static ApplicationData? CreateApplicationData()
    {
        try
        {
#if DEBUG_UNPACKAGED || RELEASE_UNPACKAGED
            return ApplicationData.GetForUnpackaged("SaveOver", "SaveOver.Sheltered2");
#else
            return ApplicationData.GetDefault();
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not initialize application settings: {ex}");
            return null;
        }
    }
}
