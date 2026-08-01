// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System.Collections.Generic;

namespace SaveOver.Sheltered2.Models;

public enum DogSkillCategory
{
    Shelter,
    Utility,
    Combat,
}

/// <summary>
/// Couples display metadata with the numeric key required for preservation-safe save matching.
/// </summary>
public sealed record DogSkillDefinition(
    int Key,
    string Name,
    DogSkillCategory Category,
    int TrainingTimeRequired);

/// <summary>
/// Centralizes verified dog-skill identities so parsing, editing, and new-pet XML cannot drift into
/// separate key tables. Display order follows the reference editor; write-back always uses keys.
/// </summary>
public static class DogSkillCatalog
{
    public static IReadOnlyList<DogSkillDefinition> All { get; } =
    [
        new(943, "Food Oriented", DogSkillCategory.Shelter, 150),
        new(945, "Playful Nature", DogSkillCategory.Shelter, 150),
        new(940, "Toilet Training", DogSkillCategory.Shelter, 300),
        new(941, "Therapy Dog Training", DogSkillCategory.Shelter, 300),
        new(942, "Digging", DogSkillCategory.Shelter, 600),
        new(944, "Protective", DogSkillCategory.Shelter, 600),

        new(934, "Guard Dog Training", DogSkillCategory.Utility, 150),
        new(930, "Saddlebag Training", DogSkillCategory.Utility, 150),
        new(935, "Gun Dog Training", DogSkillCategory.Utility, 300),
        new(933, "Sniffer Dog Training", DogSkillCategory.Utility, 300),
        new(931, "Intimidating Growl", DogSkillCategory.Utility, 600),
        new(932, "Scavenger", DogSkillCategory.Utility, 600),

        new(921, "Fetch", DogSkillCategory.Combat, 150),
        new(922, "Ferocious Bark", DogSkillCategory.Combat, 150),
        new(920, "Dog Bite", DogSkillCategory.Combat, 300),
        new(923, "Go For The Legs", DogSkillCategory.Combat, 300),
        new(924, "Tear Apart", DogSkillCategory.Combat, 600),
        new(925, "Keen Senses", DogSkillCategory.Combat, 600),
    ];
}
