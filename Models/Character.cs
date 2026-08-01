// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace SaveOver.Sheltered2.Models;

/// <summary>
/// A family member parsed from a save file. Members are public so WinUI bindings
/// (<c>DisplayMemberPath</c>, <c>x:Bind</c> in templates) can see them.
/// </summary>
public sealed partial class Character : ObservableObject
{
    /// <summary>
    /// Id taken from the <c>Member_N</c> element name. Keys this member's entry in
    /// <c>FamilyManager/members</c> and other members' relationship lists.
    /// </summary>
    public int UniqueId { get; set; } = -1;

    [ObservableProperty]
    public partial bool IsPsycho { get; set; }

    public string FirstName
    {
        get;
        set
        {
            if (SetProperty(ref field, (value ?? string.Empty).Trim()))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    } = string.Empty;

    public string LastName
    {
        get;
        set
        {
            if (SetProperty(ref field, (value ?? string.Empty).Trim()))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    } = string.Empty;

    [ObservableProperty]
    public partial int CurrentHealth { get; set; }

    [ObservableProperty]
    public partial int MaxHealth { get; set; }

    [ObservableProperty]
    public partial bool Interacting { get; set; }

    [ObservableProperty]
    public partial bool InteractingWithObj { get; set; }

    [ObservableProperty]
    public partial bool HasBeenDefibbed { get; set; }

    [ObservableProperty]
    public partial bool PassedOut { get; set; }

    [ObservableProperty]
    public partial bool IsUnconscious { get; set; }

    /// <summary>
    /// Session-only flag, never stored in the save: when set, the writer moves this
    /// member onto a sibling's transform on save, freeing a member stuck in the world.
    /// </summary>
    [ObservableProperty]
    public partial bool ResetPositionRequested { get; set; }

    public Stat Strength { get; } = new();
    public Stat Dexterity { get; } = new();
    public Stat Intelligence { get; } = new();
    public Stat Charisma { get; } = new();
    public Stat Perception { get; } = new();
    public Stat Fortitude { get; } = new();

    // Separate collections mirror the XML trees and let collection-change tracking identify the
    // exact tree whose entry list must be rebuilt.
    public ObservableCollection<SkillInstance> StrengthSkills { get; } = [];
    public ObservableCollection<SkillInstance> DexteritySkills { get; } = [];
    public ObservableCollection<SkillInstance> IntelligenceSkills { get; } = [];
    public ObservableCollection<SkillInstance> CharismaSkills { get; } = [];
    public ObservableCollection<SkillInstance> PerceptionSkills { get; } = [];
    public ObservableCollection<SkillInstance> FortitudeSkills { get; } = [];

    // Keep the game's inverse 0-100 scale rather than presenting a second semantic model that the
    // writer would have to translate and could accidentally invert.
    [ObservableProperty]
    public partial double Hunger { get; set; }

    [ObservableProperty]
    public partial double Thirst { get; set; }

    [ObservableProperty]
    public partial double Fatigue { get; set; }

    [ObservableProperty]
    public partial double Dirtiness { get; set; }

    [ObservableProperty]
    public partial double Toilet { get; set; }

    [ObservableProperty]
    public partial double Stress { get; set; }

    public ObservableCollection<Relationship> Relationships { get; } = [];

    public string FullName
    {
        get
        {
            string composed = $"{FirstName} {LastName}".Trim();
            return string.IsNullOrEmpty(composed) ? "(unnamed)" : composed;
        }
    }

    internal Stat GetStat(CharacterStat stat) => stat switch
    {
        CharacterStat.Strength => Strength,
        CharacterStat.Dexterity => Dexterity,
        CharacterStat.Intelligence => Intelligence,
        CharacterStat.Charisma => Charisma,
        CharacterStat.Perception => Perception,
        CharacterStat.Fortitude => Fortitude,
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };

    internal ObservableCollection<SkillInstance> GetSkillTree(CharacterStat stat) => stat switch
    {
        CharacterStat.Strength => StrengthSkills,
        CharacterStat.Dexterity => DexteritySkills,
        CharacterStat.Intelligence => IntelligenceSkills,
        CharacterStat.Charisma => CharismaSkills,
        CharacterStat.Perception => PerceptionSkills,
        CharacterStat.Fortitude => FortitudeSkills,
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };
}

/// <summary>
/// Enforces the game's level domain and raises for the derived cap in the same setter, preventing
/// XAML from displaying a cap that no longer matches the value the writer will persist.
/// </summary>
public sealed partial class Stat : ObservableObject
{
    public const int MinLevel = 1;
    public const int MaxLevel = 20;

    public int Level
    {
        get;
        set
        {
            int clamped = Math.Clamp(value, MinLevel, MaxLevel);
            if (field == clamped)
            {
                return;
            }

            field = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Cap));
        }
    } = MinLevel;

    public int Cap => Level <= 5 ? Level * 2 : MaxLevel;
}

/// <summary>
/// Keeps the other member's ID as the stable cross-reference. The raw level is not clamped here so
/// unusual existing values survive until the user deliberately edits that relationship.
/// </summary>
public sealed partial class Relationship(int memberId, int level) : ObservableObject
{
    public int MemberId { get; } = memberId;

    [ObservableProperty]
    public partial int Level { get; set; } = level;
}

/// <summary>
/// Uses the numeric game key as identity because display order and localized names cannot safely
/// map an edited rank back to the sparse unlocked-skill list.
/// </summary>
public sealed partial class SkillInstance(int key, int level) : ObservableObject
{
    public int Key { get; } = key;

    [ObservableProperty]
    public partial int Level { get; set; } = level;
}
