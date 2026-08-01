// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Separates save-workflow policy from page state so confirmation and resume behavior remain
/// consistent when HomePage is recreated.
/// </summary>
internal static class SaveSettings
{
    private const string ConfirmBeforeSavingSettingKey = "ConfirmBeforeSaving";
    private const string LastOpenedSavePathSettingKey = "LastOpenedSavePath";
    private const string RememberLastOpenedSaveSettingKey = "RememberLastOpenedSave";

    internal static bool ConfirmBeforeSaving
    {
        get => UserSettings.ReadBool(ConfirmBeforeSavingSettingKey, true);
        set => UserSettings.Write(ConfirmBeforeSavingSettingKey, value);
    }

    internal static string? LastOpenedSavePath
    {
        get => UserSettings.ReadString(LastOpenedSavePathSettingKey);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                UserSettings.Remove(LastOpenedSavePathSettingKey);
            }
            else
            {
                UserSettings.Write(LastOpenedSavePathSettingKey, value);
            }
        }
    }

    internal static bool RememberLastOpenedSave
    {
        get => UserSettings.ReadBool(RememberLastOpenedSaveSettingKey, true);
        set
        {
            UserSettings.Write(RememberLastOpenedSaveSettingKey, value);
            if (!value)
            {
                // Opting out also removes the sensitive path already retained by an earlier opt-in.
                LastOpenedSavePath = null;
            }
        }
    }
}
