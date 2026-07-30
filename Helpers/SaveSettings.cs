// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Owns preferences that control how edited save files are written.
/// </summary>
internal static class SaveSettings
{
    private const string ConfirmBeforeSavingSettingKey = "ConfirmBeforeSaving";

    internal static bool ConfirmBeforeSaving
    {
        get => UserSettings.ReadBool(ConfirmBeforeSavingSettingKey, true);
        set => UserSettings.Write(ConfirmBeforeSavingSettingKey, value);
    }
}
