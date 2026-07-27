// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.Mvvm.ComponentModel;

namespace SaveOver.Sheltered2.Models;

/// <summary>
/// A pet parsed from a save file (a root-level <c>Pet_N</c> element).
/// </summary>
public sealed partial class Pet : ObservableObject
{
    /// <summary>Index taken from the <c>Pet_N</c> element name.</summary>
    public int PetId { get; set; } = -1;

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

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"(unnamed pet {PetId})" : Name;

    public int Age
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int Health
    {
        get;
        set => SetProperty(ref field, value);
    }

    // 0-100 float in the save; lower is better.
    public double Hunger
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool Starving
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool Poisoned
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool Immune
    {
        get;
        set => SetProperty(ref field, value);
    }

    public PetSkill PreyDrive { get; } = new();
    public PetSkill Scavenging { get; } = new();
    public PetSkill Affection { get; } = new();

    public PetSkill? GetSkill(string name) => name switch
    {
        "PreyDrive" => PreyDrive,
        "Scavenging" => Scavenging,
        "Affection" => Affection,
        _ => null,
    };
}

/// <summary>
/// A pet training skill: level, level cap and accumulated experience, as stored in the save.
/// </summary>
public sealed partial class PetSkill : ObservableObject
{
    public int Level
    {
        get;
        set => SetProperty(ref field, value);
    } = 1;

    public int LevelCap
    {
        get;
        set => SetProperty(ref field, value);
    } = 9;

    public int Experience
    {
        get;
        set => SetProperty(ref field, value);
    }
}
