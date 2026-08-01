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

    [ObservableProperty]
    public partial int Age { get; set; }

    [ObservableProperty]
    public partial int Health { get; set; }

    // 0-100 float in the save; lower is better.
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

    internal PetSkill GetSkill(PetSkillKind skill) => skill switch
    {
        PetSkillKind.PreyDrive => PreyDrive,
        PetSkillKind.Scavenging => Scavenging,
        PetSkillKind.Affection => Affection,
        _ => throw new System.ArgumentOutOfRangeException(nameof(skill)),
    };
}

/// <summary>
/// A pet training skill: level, level cap and accumulated experience, as stored in the save.
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
