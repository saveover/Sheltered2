// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Coordinates the process-wide ElementSoundPlayer with persisted settings so individual pages do
/// not compete over a global state or leave spatial audio enabled behind a disabled parent option.
/// </summary>
/// <remarks>
/// <see cref="ElementSoundPlayer"/> is app-wide and off by default on desktop, so this is opt-in.
/// Spatial audio only means anything while the sounds themselves are on, which is why turning them
/// off turns it off too rather than leaving a setting that quietly does nothing.
/// </remarks>
internal static class SoundHelper
{
    private const string SoundSettingKey = "SoundEnabled";
    private const string SpatialAudioSettingKey = "SpatialAudioEnabled";

    private static bool _isSoundEnabled;
    private static bool _isSpatialAudioEnabled;

    /// <summary>Whether controls play a sound when the user interacts with them.</summary>
    internal static bool IsSoundEnabled
    {
        get => _isSoundEnabled;
        set
        {
            _isSoundEnabled = value;

            if (!value)
            {
                IsSpatialAudioEnabled = false;
            }

            Apply();
            UserSettings.Write(SoundSettingKey, value);
        }
    }

    /// <summary>Whether those sounds are placed in 3D space. Ignored while sound is off.</summary>
    internal static bool IsSpatialAudioEnabled
    {
        get => _isSpatialAudioEnabled;
        set
        {
            _isSpatialAudioEnabled = value;
            Apply();
            UserSettings.Write(SpatialAudioSettingKey, value);
        }
    }

    /// <summary>Restores both settings together so spatial audio is never briefly active alone.</summary>
    internal static void Initialize()
    {
        _isSoundEnabled = UserSettings.ReadBool(SoundSettingKey, false);
        _isSpatialAudioEnabled = _isSoundEnabled && UserSettings.ReadBool(SpatialAudioSettingKey, false);
        Apply();
    }

    private static void Apply()
    {
        ElementSoundPlayer.State = _isSoundEnabled ? ElementSoundPlayerState.On : ElementSoundPlayerState.Off;
        ElementSoundPlayer.SpatialAudioMode = _isSoundEnabled && _isSpatialAudioEnabled
            ? ElementSpatialAudioMode.On
            : ElementSpatialAudioMode.Off;
    }
}
