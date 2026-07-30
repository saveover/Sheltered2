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

    public bool IsPsycho
    {
        get;
        set => SetProperty(ref field, value);
    }

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

    public int CurrentHealth
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int MaxHealth
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool Interacting
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool InteractingWithObj
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool HasBeenDefibbed
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool PassedOut
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsUnconscious
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Session-only flag, never stored in the save: when set, the writer moves this
    /// member onto a sibling's transform on save, freeing a member stuck in the world.
    /// </summary>
    public bool ResetPositionRequested
    {
        get;
        set => SetProperty(ref field, value);
    }

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
    public double Hunger
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double Thirst
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double Fatigue
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double Dirtiness
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double Toilet
    {
        get;
        set => SetProperty(ref field, value);
    }

    public double Stress
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<Relationship> Relationships { get; } = [];

    public string FullName
    {
        get
        {
            string composed = $"{FirstName} {LastName}".Trim();
            return string.IsNullOrEmpty(composed) ? "(unnamed)" : composed;
        }
    }

    public ObservableCollection<SkillInstance>? GetSkillTree(string statName) => statName switch
    {
        "Strength" => StrengthSkills,
        "Dexterity" => DexteritySkills,
        "Intelligence" => IntelligenceSkills,
        "Charisma" => CharismaSkills,
        "Perception" => PerceptionSkills,
        "Fortitude" => FortitudeSkills,
        _ => null,
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

    public int Level
    {
        get;
        set => SetProperty(ref field, value);
    } = level;
}

/// <summary>
/// A skill entry as stored in the save: the numeric key and the trained level.
/// </summary>
public sealed partial class SkillInstance(int key, int level) : ObservableObject
{
    public int Key { get; } = key;

    public int Level
    {
        get;
        set => SetProperty(ref field, value);
    } = level;
}
