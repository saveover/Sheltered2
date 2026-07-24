// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Writes edited model data back into a save file's XML. Edits are applied onto the
/// original decrypted document so everything the editor doesn't model stays untouched.
/// Characters map to <c>FamilyMembers</c> elements by document order, the same order the
/// parser produced them in.
/// </summary>
internal static class SaveWriter
{
    private static readonly string[] StatNames =
        ["Strength", "Dexterity", "Intelligence", "Charisma", "Perception", "Fortitude"];

    private static readonly string[] PetSkillNames = ["PreyDrive", "Scavenging", "Affection"];

    internal static string ApplyEdits(string originalXml, IReadOnlyList<Character> characters, IReadOnlyList<Pet> pets)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalXml);
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(pets);

        XDocument document = XDocument.Parse(originalXml, LoadOptions.PreserveWhitespace);

        XElement? familyMembers = document.Root?.Element("FamilyMembers");
        if (familyMembers is not null)
        {
            List<XElement> memberElements = [.. familyMembers.Elements()];
            int count = Math.Min(memberElements.Count, characters.Count);
            for (int i = 0; i < count; i++)
            {
                ApplyMember(memberElements[i], characters[i]);
            }

            ApplyPositionResets(memberElements, characters, count);
        }

        ApplyPsychoStates(document.Root?.Element("FamilyManager"), characters);
        ApplyPets(document.Root, pets);

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static void ApplyMember(XElement memberElement, Character character)
    {
        SetValue(memberElement, "firstName", character.FirstName);
        SetValue(memberElement, "lastName", character.LastName);
        SetValue(memberElement, "health", Int(character.CurrentHealth));
        SetValue(memberElement, "maxHealth", Int(character.MaxHealth));
        SetValue(memberElement, "interacting", Bool(character.Interacting));
        SetValue(memberElement, "interactingWithObj", Bool(character.InteractingWithObj));
        SetValue(memberElement, "hasBeenDefibbed", Bool(character.HasBeenDefibbed));
        SetValue(memberElement, "PassedOut", Bool(character.PassedOut));
        SetValue(memberElement, "isUnconscious", Bool(character.IsUnconscious));

        ApplyStats(memberElement.Element("BaseStats"), character);
        ApplySkills(memberElement.Element("Profession"), memberElement.Element("BaseStats"), character);
        ApplyNeeds(memberElement.Element("NeedsStats"), character);
        ApplyRelationships(memberElement.Element("AI_Relationships"), character);
    }

    /// <summary>
    /// Moves any member with a queued position reset onto a sibling's <c>transform</c>,
    /// freeing a member stuck in the world. The donor is the first other member, assumed
    /// to be standing somewhere valid.
    /// </summary>
    private static void ApplyPositionResets(List<XElement> memberElements, IReadOnlyList<Character> characters, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!characters[i].ResetPositionRequested)
            {
                continue;
            }

            int donor = i == 0 ? 1 : 0;
            if (donor < count)
            {
                ResetMemberPosition(memberElements[i], memberElements[donor]);
            }
        }
    }

    private static void ResetMemberPosition(XElement target, XElement donor)
    {
        XElement? targetTransform = target.Element("transform");
        XElement? donorTransform = donor.Element("transform");
        if (targetTransform is null || donorTransform is null)
        {
            return;
        }

        CopyVector(donorTransform, targetTransform, "pos");
        CopyVector(donorTransform, targetTransform, "scale");
        CopyVector(donorTransform, targetTransform, "rot");
    }

    private static void CopyVector(XElement fromParent, XElement toParent, string vectorName)
    {
        XElement? from = fromParent.Element(vectorName);
        XElement? to = toParent.Element(vectorName);
        if (from is null || to is null)
        {
            return;
        }

        string[] axes = ["x", "y", "z"];
        foreach (string axis in axes)
        {
            XAttribute? value = from.Attribute(axis);
            if (value is not null)
            {
                to.SetAttributeValue(axis, value.Value);
            }
        }
    }

    private static void ApplyStats(XElement? baseStatsElement, Character character)
    {
        if (baseStatsElement is null)
        {
            return;
        }

        foreach (string statName in StatNames)
        {
            XElement? statElement = baseStatsElement.Element(statName);
            if (statElement is null)
            {
                continue;
            }

            Stat stat = GetStat(character, statName);
            SetValue(statElement, "level", Int(stat.Level));
            SetValue(statElement, "cap", Int(stat.Cap));
        }
    }

    // The psycho flag lives under FamilyManager/members, keyed by uniqueId.
    private static void ApplyPsychoStates(XElement? familyManagerElement, IReadOnlyList<Character> characters)
    {
        XElement? members = familyManagerElement?.Element("members");
        if (members is null)
        {
            return;
        }

        Dictionary<int, Character> charactersById = [];
        foreach (Character character in characters.Where(c => c.UniqueId >= 0))
        {
            charactersById[character.UniqueId] = character;
        }

        foreach (XElement entry in members.Elements())
        {
            XElement? isPsychoElement = entry.Element("Psycho")?.Element("isPsycho");
            if (isPsychoElement is not null
                && int.TryParse(entry.Element("uniqueId")?.Value, out int id)
                && charactersById.TryGetValue(id, out Character? character))
            {
                isPsychoElement.Value = Bool(character.IsPsycho);
            }
        }
    }

    private static void ApplyNeeds(XElement? needsElement, Character character)
    {
        if (needsElement is null)
        {
            return;
        }

        SetNeedValue(needsElement, "hunger", character.Hunger);
        SetNeedValue(needsElement, "thirst", character.Thirst);
        SetNeedValue(needsElement, "fatigue", character.Fatigue);
        SetNeedValue(needsElement, "dirtiness", character.Dirtiness);
        SetNeedValue(needsElement, "toilet", character.Toilet);
        SetNeedValue(needsElement, "stress", character.Stress);
    }

    private static void ApplyRelationships(XElement? aiRelationshipsElement, Character character)
    {
        XElement? relationships = aiRelationshipsElement?.Element("relationships");
        if (relationships is null)
        {
            return;
        }

        Dictionary<int, Relationship> byMemberId = [];
        foreach (Relationship relationship in character.Relationships)
        {
            byMemberId[relationship.MemberId] = relationship;
        }

        foreach (XElement entry in relationships.Elements())
        {
            XElement? levelElement = entry.Element("relationshipLevel");
            if (levelElement is not null
                && int.TryParse(entry.Element("memberID")?.Value, out int memberId)
                && byMemberId.TryGetValue(memberId, out Relationship? relationship))
            {
                levelElement.Value = Int(relationship.Level);
            }
        }
    }

    private static void ApplyPets(XElement? root, IReadOnlyList<Pet> pets)
    {
        if (root is null || pets.Count == 0)
        {
            return;
        }

        Dictionary<int, Pet> petsById = [];
        foreach (Pet pet in pets.Where(p => p.PetId >= 0))
        {
            petsById[pet.PetId] = pet;
        }

        foreach (XElement petElement in root.Elements())
        {
            string name = petElement.Name.LocalName;
            if (!name.StartsWith("Pet_", StringComparison.Ordinal)
                || !int.TryParse(name.AsSpan(4), out int petId)
                || !petsById.TryGetValue(petId, out Pet? pet))
            {
                continue;
            }

            SetValue(petElement, "name", pet.Name);
            SetValue(petElement, "age", Int(pet.Age));
            SetValue(petElement, "health", Int(pet.Health));
            SetValue(petElement, "hunger", Dbl(pet.Hunger));
            SetValue(petElement, "starving", Bool(pet.Starving));
            SetValue(petElement, "poisoned", Bool(pet.Poisoned));
            SetValue(petElement, "immune", Bool(pet.Immune));

            foreach (string skillName in PetSkillNames)
            {
                XElement? skillElement = petElement.Element(skillName);
                PetSkill? skill = pet.GetSkill(skillName);
                if (skillElement is null || skill is null)
                {
                    continue;
                }

                SetValue(skillElement, "level", Int(skill.Level));
                SetValue(skillElement, "levelCap", Int(skill.LevelCap));
                SetValue(skillElement, "experience", Int(skill.Experience));
            }
        }
    }

    private static void ApplySkills(XElement? professionElement, XElement? baseStatsElement, Character character)
    {
        if (professionElement is null)
        {
            return;
        }

        foreach (string statName in StatNames)
        {
            XElement? listElement = professionElement
                .Element($"{statName}Skills")?
                .Element($"{ToCamelCase(statName)}Skills");
            ObservableCollection<SkillInstance>? tree = character.GetSkillTree(statName);
            if (listElement is null || tree is null)
            {
                continue;
            }

            // Keep the extra per-skill fields (accuracy/damage/...) of skills that already
            // existed, keyed by skillKey, so re-saving doesn't wipe in-game upgrades.
            Dictionary<int, XElement> existingByKey = [];
            foreach (XElement entry in listElement.Elements())
            {
                if (int.TryParse(entry.Element("skillKey")?.Value, out int key))
                {
                    existingByKey[key] = entry;
                }
            }

            // The save only lists unlocked skills and its size counts them, so rebuild the
            // list and renumber i0..iN.
            listElement.RemoveNodes();
            int index = 0;
            foreach (SkillInstance skill in tree.Where(skill => skill.Level > 0))
            {
                listElement.Add(BuildSkillEntry(index, skill, existingByKey.GetValueOrDefault(skill.Key)));
                index++;
            }

            listElement.SetAttributeValue("size", index);

            UpdatePointsSpentCounter(baseStatsElement?.Element(statName), statName, tree);
        }
    }

    /// <summary>
    /// Keeps <c>pointsSpent_tierOne</c> consistent with the invested skills. The game uses
    /// this counter to unlock tiers 2 and 3 (at 5 and 10 points); without it, skills added
    /// to a locked tier appear locked in-game.
    /// </summary>
    private static void UpdatePointsSpentCounter(XElement? statElement, string statName, ObservableCollection<SkillInstance> tree)
    {
        XElement? counterElement = statElement?.Element("pointsSpent_tierOne");
        if (counterElement is null)
        {
            return;
        }

        Dictionary<int, int> tierByKey = SkillCatalog.ForStat(statName).ToDictionary(d => d.Key, d => d.Tier);

        int tierOnePoints = 0;
        bool hasTierTwo = false;
        bool hasTierThree = false;
        foreach (SkillInstance skill in tree.Where(skill => skill.Level > 0))
        {
            switch (tierByKey.GetValueOrDefault(skill.Key))
            {
                case 1:
                    tierOnePoints += skill.Level;
                    break;
                case 2:
                    hasTierTwo = true;
                    break;
                case 3:
                    hasTierThree = true;
                    break;
            }
        }

        int required = Math.Max(tierOnePoints, hasTierThree ? 10 : hasTierTwo ? 5 : 0);
        int current = int.TryParse(counterElement.Value, out int value) ? value : 0;
        counterElement.Value = Int(Math.Max(current, required));
    }

    private static XElement BuildSkillEntry(int index, SkillInstance skill, XElement? existing) =>
        new(
            $"i{index}",
            new XElement("skillKey", skill.Key),
            new XElement("skillLevel", skill.Level),
            new XElement("accuracyLevel", ChildInt(existing, "accuracyLevel")),
            new XElement("damageLevel", ChildInt(existing, "damageLevel")),
            new XElement("staminaLevel", ChildInt(existing, "staminaLevel")),
            new XElement("chanceLevel", ChildInt(existing, "chanceLevel")));

    private static void SetValue(XElement parent, string childName, string value)
    {
        XElement? child = parent.Element(childName);
        child?.Value = value;
    }

    private static void SetNeedValue(XElement needsElement, string needName, double value)
    {
        XElement? valueElement = needsElement.Element(needName)?.Element("value");
        valueElement?.Value = Dbl(Math.Clamp(value, 0, 100));
    }

    private static Stat GetStat(Character character, string statName) => statName switch
    {
        "Strength" => character.Strength,
        "Dexterity" => character.Dexterity,
        "Intelligence" => character.Intelligence,
        "Charisma" => character.Charisma,
        "Perception" => character.Perception,
        "Fortitude" => character.Fortitude,
        _ => throw new ArgumentOutOfRangeException(nameof(statName), statName, "Unknown stat name."),
    };

    private static int ChildInt(XElement? parent, string childName) =>
        int.TryParse(parent?.Element(childName)?.Value, out int value) ? value : 0;

    // The game writes booleans capitalised (<isSlave>False</isSlave>).
    private static string Bool(bool value) => value ? "True" : "False";

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    // Round-trip format keeps the game's float precision without locale surprises.
    private static string Dbl(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
