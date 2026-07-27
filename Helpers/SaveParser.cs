// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>The characters and pets extracted from one save file.</summary>
internal sealed record ParsedSave(IReadOnlyList<Character> Characters, IReadOnlyList<Pet> Pets);

/// <summary>
/// Parses the decrypted save-file XML into model objects.
/// </summary>
internal static class SaveParser
{
    private static readonly string[] StatNames =
        ["Strength", "Dexterity", "Intelligence", "Charisma", "Perception", "Fortitude"];

    private static readonly string[] PetSkillNames = ["PreyDrive", "Scavenging", "Affection"];

    /// <summary>
    /// Parses the whole save in one pass over a single <see cref="XDocument"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The content is not valid XML.</exception>
    internal static ParsedSave Parse(string decryptedContent)
    {
        ArgumentException.ThrowIfNullOrEmpty(decryptedContent);

        XDocument document;
        try
        {
            document = XDocument.Parse(decryptedContent);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("Failed to parse the decrypted content into valid XML.", ex);
        }

        return document.Root is null
            ? new ParsedSave([], [])
            : new ParsedSave(ParseCharacters(document.Root), ParsePets(document.Root));
    }

    private static IReadOnlyList<Character> ParseCharacters(XElement root)
    {
        List<Character> characters = [];
        XElement? familyMembers = root.Element("FamilyMembers");
        if (familyMembers is null)
        {
            return characters;
        }

        // FamilyManager keys per-member extras (e.g. the psycho flag) by uniqueId.
        Dictionary<int, bool> psychoById = ParsePsychoStates(root.Element("FamilyManager"));

        foreach (XElement memberElement in familyMembers.Elements())
        {
            Character character = ParseMember(memberElement);
            if (psychoById.TryGetValue(character.UniqueId, out bool isPsycho))
            {
                character.IsPsycho = isPsycho;
            }

            characters.Add(character);
        }

        return characters;
    }

    private static IReadOnlyList<Pet> ParsePets(XElement root)
    {
        List<Pet> pets = [];
        foreach (XElement petElement in root.Elements())
        {
            if (petElement.Name.LocalName.StartsWith("Pet_", StringComparison.Ordinal))
            {
                pets.Add(ParsePet(petElement));
            }
        }

        return pets;
    }

    private static Character ParseMember(XElement memberElement)
    {
        Character character = new()
        {
            UniqueId = ParseIdSuffix(memberElement.Name.LocalName, "Member_"),
            FirstName = memberElement.Element("firstName")?.Value ?? string.Empty,
            LastName = memberElement.Element("lastName")?.Value ?? string.Empty,
            CurrentHealth = ParseInt(memberElement.Element("health"), 0),
            MaxHealth = ParseInt(memberElement.Element("maxHealth"), 0),
            Interacting = ParseBool(memberElement.Element("interacting")),
            InteractingWithObj = ParseBool(memberElement.Element("interactingWithObj")),
            HasBeenDefibbed = ParseBool(memberElement.Element("hasBeenDefibbed")),
            PassedOut = ParseBool(memberElement.Element("PassedOut")),
            IsUnconscious = ParseBool(memberElement.Element("isUnconscious")),
        };

        ParseStats(memberElement.Element("BaseStats"), character);
        ParseSkills(memberElement.Element("Profession"), character);
        ParseNeeds(memberElement.Element("NeedsStats"), character);
        ParseRelationships(memberElement.Element("AI_Relationships"), character);

        return character;
    }

    private static Dictionary<int, bool> ParsePsychoStates(XElement? familyManagerElement)
    {
        Dictionary<int, bool> psychoById = [];
        XElement? members = familyManagerElement?.Element("members");
        if (members is null)
        {
            return psychoById;
        }

        foreach (XElement entry in members.Elements())
        {
            XElement? idElement = entry.Element("uniqueId");
            XElement? isPsychoElement = entry.Element("Psycho")?.Element("isPsycho");
            if (idElement is not null && TryParseInt(idElement.Value, out int id))
            {
                psychoById[id] = ParseBool(isPsychoElement);
            }
        }

        return psychoById;
    }

    private static void ParseStats(XElement? baseStatsElement, Character character)
    {
        if (baseStatsElement is null)
        {
            return;
        }

        foreach (string statName in StatNames)
        {
            XElement? statElement = baseStatsElement.Element(statName);
            if (statElement is not null)
            {
                GetStat(character, statName).Level = ParseInt(statElement.Element("level"), Stat.MinLevel);
            }
        }
    }

    private static void ParseSkills(XElement? professionElement, Character character)
    {
        if (professionElement is null)
        {
            return;
        }

        // Each tree is <StrengthSkills><strengthSkills size="n">: an outer element
        // wrapping an inner camel-cased list.
        foreach (string statName in StatNames)
        {
            XElement? listElement = professionElement
                .Element($"{statName}Skills")?
                .Element($"{ToCamelCase(statName)}Skills");
            ObservableCollection<SkillInstance>? target = character.GetSkillTree(statName);
            if (listElement is null || target is null)
            {
                continue;
            }

            foreach (XElement skillElement in listElement.Elements())
            {
                XElement? keyElement = skillElement.Element("skillKey");
                XElement? levelElement = skillElement.Element("skillLevel");
                if (keyElement is not null && levelElement is not null)
                {
                    target.Add(new SkillInstance(ParseInt(keyElement, 0), ParseInt(levelElement, 0)));
                }
            }
        }
    }

    private static void ParseNeeds(XElement? needsElement, Character character)
    {
        if (needsElement is null)
        {
            return;
        }

        character.Hunger = ParseNeedValue(needsElement, "hunger");
        character.Thirst = ParseNeedValue(needsElement, "thirst");
        character.Fatigue = ParseNeedValue(needsElement, "fatigue");
        character.Dirtiness = ParseNeedValue(needsElement, "dirtiness");
        character.Toilet = ParseNeedValue(needsElement, "toilet");
        character.Stress = ParseNeedValue(needsElement, "stress");
    }

    private static void ParseRelationships(XElement? aiRelationshipsElement, Character character)
    {
        XElement? relationships = aiRelationshipsElement?.Element("relationships");
        if (relationships is null)
        {
            return;
        }

        foreach (XElement entry in relationships.Elements())
        {
            XElement? memberIdElement = entry.Element("memberID");
            XElement? levelElement = entry.Element("relationshipLevel");
            if (memberIdElement is not null && TryParseInt(memberIdElement.Value, out int memberId))
            {
                character.Relationships.Add(new Relationship(memberId, ParseInt(levelElement, 0)));
            }
        }
    }

    private static Pet ParsePet(XElement petElement)
    {
        Pet pet = new()
        {
            PetId = ParseIdSuffix(petElement.Name.LocalName, "Pet_"),
            Name = petElement.Element("name")?.Value ?? string.Empty,
            Age = ParseInt(petElement.Element("age"), 0),
            Health = ParseInt(petElement.Element("health"), 0),
            Hunger = ParsePercentage(petElement.Element("hunger")),
            Starving = ParseBool(petElement.Element("starving")),
            Poisoned = ParseBool(petElement.Element("poisoned")),
            Immune = ParseBool(petElement.Element("immune")),
        };

        foreach (string skillName in PetSkillNames)
        {
            XElement? skillElement = petElement.Element(skillName);
            PetSkill? skill = pet.GetSkill(skillName);
            if (skillElement is null || skill is null)
            {
                continue;
            }

            skill.Level = ParseInt(skillElement.Element("level"), 1);
            skill.LevelCap = ParseInt(skillElement.Element("levelCap"), 9);
            skill.Experience = ParseInt(skillElement.Element("experience"), 0);
        }

        return pet;
    }

    private static double ParseNeedValue(XElement needsElement, string needName) =>
        ParsePercentage(needsElement.Element(needName)?.Element("value"));

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

    // Extracts the numeric suffix of names like Member_2 or Pet_0.
    private static int ParseIdSuffix(string elementName, string prefix) =>
        elementName.StartsWith(prefix, StringComparison.Ordinal)
            && TryParseInt(elementName.AsSpan(prefix.Length), out int id)
            ? id
            : -1;

    private static int ParseInt(XElement? element, int fallback) =>
        TryParseInt(element?.Value, out int value) ? value : fallback;

    private static bool TryParseInt(string? value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseInt(ReadOnlySpan<char> value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static double ParsePercentage(XElement? element) =>
        Math.Clamp(ParseDouble(element, 0), 0, 100);

    private static double ParseDouble(XElement? element, double fallback) =>
        double.TryParse(element?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            && double.IsFinite(value)
            ? value
            : fallback;

    private static bool ParseBool(XElement? element) =>
        bool.TryParse(element?.Value, out bool value) && value;

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
