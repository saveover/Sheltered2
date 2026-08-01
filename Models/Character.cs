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

    // One collection per skill tree, matching the save layout.
    public ObservableCollection<SkillInstance> StrengthSkills { get; } = [];
    public ObservableCollection<SkillInstance> DexteritySkills { get; } = [];
    public ObservableCollection<SkillInstance> IntelligenceSkills { get; } = [];
    public ObservableCollection<SkillInstance> CharismaSkills { get; } = [];
    public ObservableCollection<SkillInstance> PerceptionSkills { get; } = [];
    public ObservableCollection<SkillInstance> FortitudeSkills { get; } = [];

    // Needs (from NeedsStats). 0-100 floats in the save; lower is better in-game.
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
/// A character stat with a current level and a cap derived from it: twice the level up to
/// level 5, then always 20.
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
/// A relationship entry: the other member's unique id and how this character feels about
/// them (-100 hostile to 100 best friends).
/// </summary>
public sealed partial class Relationship(int memberId, int level) : ObservableObject
{
    public int MemberId { get; } = memberId;

    [ObservableProperty]
    public partial int Level { get; set; } = level;
}

/// <summary>
/// A skill entry as stored in the save: the numeric key and the trained level.
/// </summary>
public sealed partial class SkillInstance(int key, int level) : ObservableObject
{
    public int Key { get; } = key;

    [ObservableProperty]
    public partial int Level { get; set; } = level;
}
