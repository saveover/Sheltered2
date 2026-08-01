// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Owns the original XML baseline and the observable model graph derived from it. One process-wide
/// instance lets cached pages share edits while central change tracking decides whether replacing
/// or closing that graph would lose work.
/// </summary>
internal sealed class SaveSession
{
    private readonly List<INotifyPropertyChanged> trackedObjects = [];
    private readonly List<INotifyCollectionChanged> trackedCollections = [];
    private bool suppressChangeTracking;
    private int nextPetId;

    public string? SourceFilePath { get; private set; }

    public string DecryptedContent { get; private set; } = string.Empty;

    public IReadOnlyList<Character> Characters { get; private set; } = [];

    public IReadOnlyList<Pet> Pets { get; private set; } = [];

    public bool CanAddPets { get; private set; }

    /// <summary>Nullable because absence must disable inventory editing, not synthesize save XML.</summary>
    public ShelterInventory? Inventory { get; private set; }

    /// <summary>
    /// The character the user last selected, so pages can restore the selection after
    /// navigating away and back.
    /// </summary>
    public Character? SelectedCharacter { get; set; }

    /// <summary>The pet the user last selected, including a pet newly added in this session.</summary>
    public Pet? SelectedPet { get; set; }

    public bool IsLoaded => !string.IsNullOrEmpty(SourceFilePath) && !string.IsNullOrEmpty(DecryptedContent);

    public bool HasUnsavedChanges { get; private set; }

    /// <summary>
    /// Raised when the loaded save data is replaced. Note this also fires when a second
    /// save is loaded over an already open one, where <see cref="IsLoaded"/> stays true.
    /// </summary>
    public event EventHandler? SaveDataChanged;

    /// <summary>Separated from data replacement so HomePage can update save affordances cheaply.</summary>
    public event EventHandler? DirtyStateChanged;

    /// <summary>
    /// Replaces the model graph as one transaction, then rebuilds subscriptions before notifying
    /// pages so no page can observe new data with the old dirty-state wiring.
    /// </summary>
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
        nextPetId = Math.Max(data.NextPetId, NextAvailablePetId(Pets));
        CanAddPets = data.HasPetManager;
        SelectedCharacter = null;
        SelectedPet = null;

        TrackEditableData();
        SetDirty(false);
        SaveDataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reserves an ID against both PetManager's counter and parsed pets because either can be stale
    /// in hand-edited saves; the writer materializes XML only after the user saves.
    /// </summary>
    public Pet AddPet(PetSpecies species)
    {
        if (!IsLoaded)
        {
            throw new InvalidOperationException("Load a save before adding a pet.");
        }

        if (!CanAddPets)
        {
            throw new InvalidOperationException("This save does not contain a PetManager list.");
        }

        if (species is not PetSpecies.Cat and not PetSpecies.Dog)
        {
            throw new ArgumentOutOfRangeException(nameof(species));
        }

        int petId = nextPetId;
        HashSet<int> usedIds = [.. Pets.Select(pet => pet.PetId)];
        while (usedIds.Contains(petId))
        {
            petId++;
        }

        nextPetId = checked(petId + 1);
        Pet pet = new()
        {
            PetId = petId,
            Species = species,
            Name = species == PetSpecies.Dog ? "New dog" : "New cat",
            Age = 1,
            Health = 100,
            Hunger = 0,
            ShelterSkillPoints = species == PetSpecies.Dog ? 2 : 0,
            UtilitySkillPoints = species == PetSpecies.Dog ? 2 : 0,
            CombatSkillPoints = species == PetSpecies.Dog ? 2 : 0,
        };

        if (species == PetSpecies.Cat)
        {
            pet.PreyDrive.LevelCap = 7;
            pet.Scavenging.LevelCap = 9;
            pet.Affection.LevelCap = 9;
        }

        Pets = [.. Pets, pet];
        SelectedPet = pet;
        TrackEditableData();
        SetDirty(true);
        SaveDataChanged?.Invoke(this, EventArgs.Empty);
        return pet;
    }

    /// <summary>
    /// Advances positional baselines only after the encrypted file replacement succeeds. Without
    /// this step, a second save could treat newly added or reordered inventory entries as new again.
    /// </summary>
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

            Inventory?.Storage?.RebaselineSourceIndices();
            Inventory?.Overflow?.RebaselineSourceIndices();
        }
        finally
        {
            suppressChangeTracking = false;
        }

        SetDirty(false);
    }

    private void TrackEditableData()
    {
        // The graph is intentionally traversed explicitly. Reflection would hide which nested
        // objects participate in dirty tracking and could begin tracking display-only properties.
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
            TrackPet(pet);
        }

        if (Inventory is { } inventory)
        {
            Track(inventory);
            TrackInventoryItems(inventory.Storage);
            TrackInventoryItems(inventory.Overflow);
        }
    }

    private void TrackPet(Pet pet)
    {
        Track(pet);
        Track(pet.PreyDrive);
        Track(pet.Scavenging);
        Track(pet.Affection);
        foreach (DogSkill skill in pet.DogSkills)
        {
            Track(skill);
        }
    }

    private static int NextAvailablePetId(IReadOnlyList<Pet> pets)
    {
        int max = -1;
        foreach (Pet pet in pets)
        {
            max = Math.Max(max, pet.PetId);
        }

        return checked(max + 1);
    }

    private void TrackInventoryItems(InventoryContainer? container)
    {
        if (container is null)
        {
            return;
        }

        TrackCollection(container.Items);
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
