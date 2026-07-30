// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.IO;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Owns the folder used for timestamped save-file backups.
/// </summary>
internal static class BackupSettings
{
    private const string BackupFolderSettingKey = "BackupFolder";

    internal static string DefaultFolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData",
        "LocalLow",
        "Unicube",
        "Sheltered2");

    internal static string FolderPath
    {
        get
        {
            string? storedPath = UserSettings.ReadString(BackupFolderSettingKey);
            return !string.IsNullOrWhiteSpace(storedPath) && Path.IsPathFullyQualified(storedPath)
                ? storedPath
                : DefaultFolderPath;
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            UserSettings.Write(BackupFolderSettingKey, Path.GetFullPath(value));
        }
    }
}
