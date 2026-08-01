// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaveOver.Sheltered2.Models;

/// <summary>
/// Represents both a parsed <c>Pet_N</c> subtree and a session-created pet awaiting materialization.
/// Unknown species remains valid so incomplete saves can still round-trip without being guessed
/// into the dog or cat schema.
/// </summary>
public sealed partial class Pet : ObservableObject
{
    /// <summary>
    /// Retains the <c>Pet_N</c> suffix because it is also the cross-reference used by
    /// <c>PetManager</c>; display order is not a safe substitute.
    /// </summary>
    public int PetId { get; set; } = -1;

    /// <summary>
    /// Comes from <c>PetManager/pets</c> because the individual pet subtree does not carry a
    /// reliable species discriminator.
    /// </summary>
    public PetSpecies Species { get; init; } = PetSpecies.Unknown;

    public string SpeciesName => Species switch
    {
        PetSpecies.Dog => "Dog",
        PetSpecies.Cat => "Cat",
        _ => "Pet",
    };

    public bool IsDog => Species == PetSpecies.Dog;

    public bool IsCat => Species == PetSpecies.Cat;

    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, (value ?? string.Empty).Trim()))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"(unnamed {SpeciesName.ToLowerInvariant()} {PetId})"
        : $"{Name} ({SpeciesName.ToLowerInvariant()})";

    [ObservableProperty]
    public partial int Age { get; set; }

    [ObservableProperty]
    public partial int Health { get; set; }

    // Preserve the game's inverse scale so parsing and writing do not need a lossy semantic flip.
    [ObservableProperty]
    public partial double Hunger { get; set; }

    [ObservableProperty]
    public partial bool Starving { get; set; }

    [ObservableProperty]
    public partial bool Poisoned { get; set; }

    [ObservableProperty]
    public partial bool Immune { get; set; }

    public PetSkill PreyDrive { get; } = new();
    public PetSkill Scavenging { get; } = new();
    public PetSkill Affection { get; } = new();

    /// <summary>
    /// Starts from the complete catalog so unavailable skills remain addressable in the UI; save
    /// keys, not list positions, reconcile them with existing XML.
    /// </summary>
    public IReadOnlyList<DogSkill> DogSkills { get; init; } = [.. DogSkillCatalog.All.Select(definition => new DogSkill(definition))];

    [ObservableProperty]
    public partial int ShelterSkillPoints { get; set; }

    [ObservableProperty]
    public partial int UtilitySkillPoints { get; set; }

    [ObservableProperty]
    public partial int CombatSkillPoints { get; set; }

    public IReadOnlyList<DogSkill> ShelterSkills => [.. DogSkills.Where(static skill => skill.Category == DogSkillCategory.Shelter)];

    public IReadOnlyList<DogSkill> UtilitySkills => [.. DogSkills.Where(static skill => skill.Category == DogSkillCategory.Utility)];

    public IReadOnlyList<DogSkill> CombatSkills => [.. DogSkills.Where(static skill => skill.Category == DogSkillCategory.Combat)];

    internal PetSkill GetSkill(PetSkillKind skill) => skill switch
    {
        PetSkillKind.PreyDrive => PreyDrive,
        PetSkillKind.Scavenging => Scavenging,
        PetSkillKind.Affection => Affection,
        _ => throw new ArgumentOutOfRangeException(nameof(skill)),
    };
}

/// <summary>The species values used by Sheltered 2's <c>PetManager</c>.</summary>
public enum PetSpecies
{
    Unknown = -1,
    Dog = 0,
    Cat = 1,
}

/// <summary>
/// Leaves parsed level and experience values unnormalized so merely opening a save cannot erase an
/// unusual value; bulk UI operations write explicit canonical boundaries.
/// </summary>
public sealed partial class PetSkill : ObservableObject
{
    [ObservableProperty]
    public partial int Level { get; set; } = 1;

    [ObservableProperty]
    public partial int LevelCap { get; set; } = 9;

    [ObservableProperty]
    public partial int Experience { get; set; }
}

/// <summary>
/// Translates the game's two-field purchase/progress representation into one UI state while still
/// retaining finite intermediate training progress from existing saves.
/// </summary>
public sealed partial class DogSkill : ObservableObject
{
    internal DogSkill(DogSkillDefinition definition)
    {
        Key = definition.Key;
        Name = definition.Name;
        Category = definition.Category;
        TrainingTimeRequired = definition.TrainingTimeRequired;
    }

    public int Key { get; }

    public string Name { get; }

    public DogSkillCategory Category { get; }

    public double TrainingTimeRequired { get; }

    public string AutomationId => $"DogSkill{Key}StateComboBox";

    public string TrainingDescription => $"{TrainingTimeRequired:0} seconds required";

    public bool Purchased
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(SelectedStateIndex));
            }
        }
    }

    public double CurrentTrainingTime
    {
        get;
        set
        {
            // Preserve unusual but finite progress values from existing saves. Choosing a
            // state in the UI still writes the canonical 0 or required-time boundary.
            double normalised = double.IsFinite(value) ? Math.Max(0, value) : 0;
            if (SetProperty(ref field, normalised))
            {
                OnPropertyChanged(nameof(SelectedStateIndex));
            }
        }
    }

    /// <summary>
    /// Uses three canonical states because RatingControl cannot directly express the independent
    /// <see cref="Purchased"/> and <see cref="CurrentTrainingTime"/> fields.
    /// </summary>
    public int SelectedStateIndex
    {
        get => !Purchased ? 0 : CurrentTrainingTime >= TrainingTimeRequired ? 2 : 1;
        set
        {
            switch (Math.Clamp(value, 0, 2))
            {
                case 0:
                    Purchased = false;
                    CurrentTrainingTime = 0;
                    break;
                case 1:
                    Purchased = true;
                    CurrentTrainingTime = 0;
                    break;
                case 2:
                    Purchased = true;
                    CurrentTrainingTime = TrainingTimeRequired;
                    break;
            }

            OnPropertyChanged();
        }
    }
}
