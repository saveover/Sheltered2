// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.Collections.Generic;

namespace SaveOver.Sheltered2.Models;

/// <summary>The six character-stat trees represented in a Sheltered 2 save.</summary>
public enum CharacterStat
{
    Strength,
    Dexterity,
    Intelligence,
    Charisma,
    Perception,
    Fortitude,
}

/// <summary>The three trainable pet skills represented in a Sheltered 2 save.</summary>
public enum PetSkillKind
{
    PreyDrive,
    Scavenging,
    Affection,
}

/// <summary>
/// Provides the canonical, ordered save-format identities for character stats and pet skills.
/// </summary>
internal static class SaveFieldKind
{
    internal static IReadOnlyList<CharacterStat> CharacterStats { get; } =
        Enum.GetValues<CharacterStat>();

    internal static IReadOnlyList<PetSkillKind> PetSkills { get; } =
        Enum.GetValues<PetSkillKind>();

    internal static string XmlName(this CharacterStat stat) => stat.ToString();

    internal static string SkillListXmlName(this CharacterStat stat)
    {
        string name = stat.ToString();
        return $"{char.ToLowerInvariant(name[0])}{name[1..]}Skills";
    }

    internal static string XmlName(this PetSkillKind skill) => skill.ToString();
}
