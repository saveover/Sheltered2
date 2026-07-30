// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// The currently loaded save file and its parsed data. One instance lives for the app's
/// lifetime (<see cref="App.CurrentSaveData"/>) so pages share state without reaching
/// across the visual tree.
/// </summary>
internal sealed class SaveSession
{
    private readonly List<INotifyPropertyChanged> trackedObjects = [];
    private readonly List<INotifyCollectionChanged> trackedCollections = [];
    private bool suppressChangeTracking;

    public string? SourceFilePath { get; private set; }

    public string DecryptedContent { get; private set; } = string.Empty;

    public IReadOnlyList<Character> Characters { get; private set; } = [];

    public IReadOnlyList<Pet> Pets { get; private set; } = [];

    /// <summary>The shelter-owned water and inventory containers, when present in the save.</summary>
    public ShelterInventory? Inventory { get; private set; }

    /// <summary>
    /// The character the user last selected, so pages can restore the selection after
    /// navigating away and back.
    /// </summary>
    public Character? SelectedCharacter { get; set; }

    public bool IsLoaded => !string.IsNullOrEmpty(SourceFilePath) && !string.IsNullOrEmpty(DecryptedContent);

    public bool HasUnsavedChanges { get; private set; }

    /// <summary>
    /// Raised when the loaded save data is replaced. Note this also fires when a second
    /// save is loaded over an already open one, where <see cref="IsLoaded"/> stays true.
    /// </summary>
    public event EventHandler? SaveDataChanged;

    public event EventHandler? DirtyStateChanged;

    public void Load(string filePath, string decryptedContent, ParsedSave data)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(decryptedContent);
        ArgumentNullException.ThrowIfNull(data);

        SourceFilePath = filePath;
        DecryptedContent = decryptedContent;
        Characters = data.Characters;
        Pets = data.Pets;
        Inventory = data.Inventory;
        SelectedCharacter = null;

        TrackEditableData();
        SetDirty(false);
        SaveDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Updates the XML baseline after the current session has been saved successfully.</summary>
    public void CommitSavedContent(string decryptedContent)
    {
        ArgumentException.ThrowIfNullOrEmpty(decryptedContent);
        DecryptedContent = decryptedContent;

        suppressChangeTracking = true;
        try
        {
            foreach (Character character in Characters)
            {
                character.ResetPositionRequested = false;
            }
        }
        finally
        {
            suppressChangeTracking = false;
        }

        SetDirty(false);
    }

    private void TrackEditableData()
    {
        StopTrackingEditableData();

        foreach (Character character in Characters)
        {
            Track(character);
            Track(character.Strength);
            Track(character.Dexterity);
            Track(character.Intelligence);
            Track(character.Charisma);
            Track(character.Perception);
            Track(character.Fortitude);
            TrackCollection(character.Relationships);
            TrackCollection(character.StrengthSkills);
            TrackCollection(character.DexteritySkills);
            TrackCollection(character.IntelligenceSkills);
            TrackCollection(character.CharismaSkills);
            TrackCollection(character.PerceptionSkills);
            TrackCollection(character.FortitudeSkills);
        }

        foreach (Pet pet in Pets)
        {
            Track(pet);
            Track(pet.PreyDrive);
            Track(pet.Scavenging);
            Track(pet.Affection);
        }

        if (Inventory is { } inventory)
        {
            Track(inventory);
            TrackItems(inventory.Storage?.Items);
            TrackItems(inventory.Overflow?.Items);
        }
    }

    private void TrackItems(IReadOnlyList<InventoryItem>? items)
    {
        if (items is null)
        {
            return;
        }

        foreach (InventoryItem item in items)
        {
            Track(item);
        }
    }

    private void TrackCollection<T>(System.Collections.ObjectModel.ObservableCollection<T> collection)
        where T : INotifyPropertyChanged
    {
        collection.CollectionChanged += TrackedCollection_CollectionChanged;
        trackedCollections.Add(collection);

        foreach (T item in collection)
        {
            Track(item);
        }
    }

    private void Track(INotifyPropertyChanged item)
    {
        item.PropertyChanged += TrackedObject_PropertyChanged;
        trackedObjects.Add(item);
    }

    private void StopTracking(INotifyPropertyChanged item)
    {
        item.PropertyChanged -= TrackedObject_PropertyChanged;
        _ = trackedObjects.Remove(item);
    }

    private void StopTrackingEditableData()
    {
        foreach (INotifyPropertyChanged item in trackedObjects)
        {
            item.PropertyChanged -= TrackedObject_PropertyChanged;
        }

        foreach (INotifyCollectionChanged collection in trackedCollections)
        {
            collection.CollectionChanged -= TrackedCollection_CollectionChanged;
        }

        trackedObjects.Clear();
        trackedCollections.Clear();
    }

    private void TrackedObject_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!suppressChangeTracking)
        {
            SetDirty(true);
        }
    }

    private void TrackedCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Reset does not report the removed items. Rebuild all subscriptions from the
            // authoritative model so detached items cannot keep this session alive or dirty it.
            TrackEditableData();
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (object? item in e.OldItems)
                {
                    if (item is INotifyPropertyChanged observable)
                    {
                        StopTracking(observable);
                    }
                }
            }

            if (e.NewItems is not null)
            {
                foreach (object? item in e.NewItems)
                {
                    if (item is INotifyPropertyChanged observable)
                    {
                        Track(observable);
                    }
                }
            }
        }

        if (!suppressChangeTracking)
        {
            SetDirty(true);
        }
    }

    private void SetDirty(bool value)
    {
        if (HasUnsavedChanges == value)
        {
            return;
        }

        HasUnsavedChanges = value;
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
