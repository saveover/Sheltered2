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
    private const string BackupRetentionSettingKey = "BackupRetention";
    private const int DefaultRetentionCount = 10;

    internal static string DefaultFolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveOver",
        "Sheltered2",
        "Backups");

    internal static string GameSaveFolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Unicube", "Sheltered2");

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

    /// <summary>
    /// Number of backups retained for each source save. Zero means that all backups are kept.
    /// </summary>
    internal static int RetentionCount
    {
        get
        {
            int value = UserSettings.ReadInt32(BackupRetentionSettingKey, DefaultRetentionCount);
            return value is 0 or 5 or 10 or 20 ? value : DefaultRetentionCount;
        }
        set
        {
            if (value is not (0 or 5 or 10 or 20))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            UserSettings.Write(BackupRetentionSettingKey, value);
        }
    }

    internal static bool IsGameSaveFolder
    {
        get
        {
            string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(FolderPath));
            string gameFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(GameSaveFolderPath));
            return string.Equals(folder, gameFolder, StringComparison.OrdinalIgnoreCase) ||
                   folder.StartsWith($"{gameFolder}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static void ResetFolder() => UserSettings.Remove(BackupFolderSettingKey);
}
