// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.Extensions.Logging;
using Microsoft.Windows.Storage;
using System;
using System.Threading;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Isolates deployment-specific ApplicationData access and makes preferences non-fatal. A damaged
/// settings store should fall back to defaults rather than prevent save recovery or app startup.
/// </summary>
internal static class UserSettings
{
    private static readonly ILogger Logger = App.LoggerFactory.CreateLogger(typeof(UserSettings).FullName!);
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
            Logger.LogWarning(ex, "Could not store the {PreferenceKey} preference.", key);
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
            Logger.LogWarning(ex, "Could not remove the {PreferenceKey} preference.", key);
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
            Logger.LogWarning(ex, "Could not read the {PreferenceKey} preference.", key);
        }

        value = default;
        return false;
    }

    private static ApplicationData? GetSettings()
    {
        // Settings are used from UI continuations and startup helpers; serialize lazy creation so
        // both cannot race into different unpackaged stores.
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
            Logger.LogWarning(ex, "Could not initialize application settings.");
            return null;
        }
    }
}
