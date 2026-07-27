// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using Windows.Storage;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// The currently loaded save file and its parsed data. One instance lives for the app's
/// lifetime (<see cref="App.CurrentSaveData"/>) so pages share state without reaching
/// across the visual tree.
/// </summary>
internal sealed class SaveSession
{
    public StorageFile? SourceFile { get; private set; }

    public string DecryptedContent { get; private set; } = string.Empty;

    public IReadOnlyList<Character> Characters { get; private set; } = [];

    public IReadOnlyList<Pet> Pets { get; private set; } = [];

    /// <summary>
    /// The character the user last selected, so pages can restore the selection after
    /// navigating away and back.
    /// </summary>
    public Character? SelectedCharacter { get; set; }

    public bool IsLoaded => SourceFile is not null && !string.IsNullOrEmpty(DecryptedContent);

    /// <summary>
    /// Raised when the loaded save data is replaced. Note this also fires when a second
    /// save is loaded over an already open one, where <see cref="IsLoaded"/> stays true.
    /// </summary>
    public event EventHandler? SaveDataChanged;

    public void Load(StorageFile file, string decryptedContent, ParsedSave data)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(decryptedContent);
        ArgumentNullException.ThrowIfNull(data);

        SourceFile = file;
        DecryptedContent = decryptedContent;
        Characters = data.Characters;
        Pets = data.Pets;
        SelectedCharacter = null;

        SaveDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Updates the XML baseline after the current session has been saved successfully.</summary>
    public void CommitSavedContent(string decryptedContent)
    {
        ArgumentException.ThrowIfNullOrEmpty(decryptedContent);
        DecryptedContent = decryptedContent;
    }
}
