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

/// <summary>The editable data extracted from one save file.</summary>
internal sealed record ParsedSave(
    IReadOnlyList<Character> Characters,
    IReadOnlyList<Pet> Pets,
    ShelterInventory? Inventory,
    int NextPetId,
    bool HasPetManager);

/// <summary>
/// Parses the decrypted save-file XML into model objects.
/// </summary>
internal static class SaveParser
{
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
            document = XDocument.Parse(decryptedContent, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("Failed to parse the decrypted content into valid XML.", ex);
        }

        return document.Root?.Name != "root"
            ? throw new InvalidDataException("The decrypted content does not have the expected root element.")
            : new ParsedSave(
            ParseCharacters(document.Root),
            ParsePets(document.Root),
            ParseShelterInventory(document.Root),
            ParseInt(document.Root.Element("PetManager")?.Element("uniqueID"), 0),
            document.Root.Element("PetManager")?.Element("pets") is not null);
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
        Dictionary<int, PetSpecies> speciesById = ParsePetSpecies(root.Element("PetManager"));
        foreach (XElement petElement in root.Elements())
        {
            if (petElement.Name.LocalName.StartsWith("Pet_", StringComparison.Ordinal))
            {
                int petId = ParseIdSuffix(petElement.Name.LocalName, "Pet_");
                PetSpecies species = speciesById.GetValueOrDefault(petId, InferPetSpecies(petElement));
                pets.Add(ParsePet(petElement, species));
            }
        }

        return pets;
    }

    private static Dictionary<int, PetSpecies> ParsePetSpecies(XElement? petManagerElement)
    {
        Dictionary<int, PetSpecies> speciesById = [];
        XElement? entries = petManagerElement?.Element("pets");
        if (entries is null)
        {
            return speciesById;
        }

        foreach (XElement entry in entries.Elements())
        {
            if (!TryParseInt(entry.Element("uniqueId")?.Value, out int id)
                || !TryParseInt(entry.Element("petSpecies")?.Value, out int rawSpecies)
                || !Enum.IsDefined((PetSpecies)rawSpecies))
            {
                continue;
            }

            speciesById[id] = (PetSpecies)rawSpecies;
        }

        return speciesById;
    }

    private static PetSpecies InferPetSpecies(XElement petElement) =>
        petElement.Element("Dog_Skills") is not null
            ? PetSpecies.Dog
            : petElement.Element("PreyDrive") is not null
                ? PetSpecies.Cat
                : PetSpecies.Unknown;

    /// <summary>
    /// Parses shelter-owned water and both inventory containers. Entries remain in their XML
    /// order because neither <c>defKey</c> nor <c>id</c> identifies a stack uniquely.
    /// </summary>
    private static ShelterInventory? ParseShelterInventory(XElement root)
    {
        XElement? storedWaterElement = root.Element("StoredWater");
        XElement? shelterInventoryElement = root.Element("ShelterInventory");
        return storedWaterElement is null && shelterInventoryElement is null
            ? null
            : new ShelterInventory
        {
            HasStoredWater = storedWaterElement is not null,
            StoredWater = ParseInt(storedWaterElement, 0),
            Storage = ParseInventoryContainer(
                shelterInventoryElement?.Element("Storage")?.Element("Inventory"),
                "Shelter Inventory"),
            Overflow = ParseInventoryContainer(
                shelterInventoryElement?.Element("Overflow")?.Element("Inventory"),
                "Overflow (ItemBin)"),
        };
    }

    private static InventoryContainer? ParseInventoryContainer(XElement? inventoryElement, string fallbackName)
    {
        if (inventoryElement is null)
        {
            return null;
        }

        List<InventoryItem> items = [];
        XElement? contents = inventoryElement.Element("InventoryContents");
        if (contents is not null)
        {
            foreach (XElement entry in contents.Elements())
            {
                items.Add(new InventoryItem
                {
                    DefinitionKey = entry.Element("defKey")?.Value ?? string.Empty,
                    Amount = ParseInt(entry.Element("amount"), 0),
                    Integrity = ParseInt(entry.Element("integrity"), 0),
                    Quality = ParseInt(entry.Element("quality"), 0),
                });
            }
        }

        return new InventoryContainer(
            inventoryElement.Element("name")?.Value ?? fallbackName,
            ParseInt(inventoryElement.Element("maxWeight"), 0),
            items);
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

        foreach (CharacterStat stat in SaveFieldKind.CharacterStats)
        {
            XElement? statElement = baseStatsElement.Element(stat.XmlName());
            if (statElement is not null)
            {
                character.GetStat(stat).Level = ParseInt(statElement.Element("level"), Stat.MinLevel);
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
        foreach (CharacterStat stat in SaveFieldKind.CharacterStats)
        {
            string statName = stat.XmlName();
            XElement? listElement = professionElement
                .Element($"{statName}Skills")?
                .Element(stat.SkillListXmlName());
            if (listElement is null)
            {
                continue;
            }

            ObservableCollection<SkillInstance> target = character.GetSkillTree(stat);

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

    private static Pet ParsePet(XElement petElement, PetSpecies species)
    {
        Pet pet = new()
        {
            PetId = ParseIdSuffix(petElement.Name.LocalName, "Pet_"),
            Species = species,
            Name = petElement.Element("name")?.Value ?? string.Empty,
            Age = ParseInt(petElement.Element("age"), 0),
            Health = ParseInt(petElement.Element("health"), 0),
            Hunger = ParsePercentage(petElement.Element("hunger")),
            Starving = ParseBool(petElement.Element("starving")),
            Poisoned = ParseBool(petElement.Element("poisoned")),
            Immune = ParseBool(petElement.Element("immune")),
        };

        foreach (PetSkillKind skillKind in SaveFieldKind.PetSkills)
        {
            string skillName = skillKind.XmlName();
            XElement? skillElement = petElement.Element(skillName);
            if (skillElement is null)
            {
                continue;
            }

            PetSkill skill = pet.GetSkill(skillKind);

            skill.Level = ParseInt(skillElement.Element("level"), 1);
            skill.LevelCap = ParseInt(skillElement.Element("levelCap"), 9);
            skill.Experience = ParseInt(skillElement.Element("experience"), 0);
        }

        ParseDogSkills(petElement.Element("Dog_Skills"), pet);

        return pet;
    }

    private static void ParseDogSkills(XElement? dogSkillsElement, Pet pet)
    {
        if (dogSkillsElement is null)
        {
            return;
        }

        Dictionary<int, XElement> entriesByKey = [];
        foreach (string listName in new[] { "shelterSkills", "utilitySkills", "combatSkills" })
        {
            XElement? list = dogSkillsElement.Element(listName);
            if (list is null)
            {
                continue;
            }

            foreach (XElement entry in list.Elements())
            {
                if (TryParseInt(entry.Element("skillKey")?.Value, out int key))
                {
                    entriesByKey[key] = entry;
                }
            }
        }

        foreach (DogSkill skill in pet.DogSkills)
        {
            if (entriesByKey.TryGetValue(skill.Key, out XElement? entry))
            {
                skill.Purchased = ParseBool(entry.Element("purchased"));
                skill.CurrentTrainingTime = ParseDouble(entry.Element("currentTrainingTime"), 0);
            }
        }

        pet.ShelterSkillPoints = ParseInt(dogSkillsElement.Element("shelterPoints"), 0);
        pet.UtilitySkillPoints = ParseInt(dogSkillsElement.Element("utilityPoints"), 0);
        pet.CombatSkillPoints = ParseInt(dogSkillsElement.Element("combatPoints"), 0);
    }

    private static double ParseNeedValue(XElement needsElement, string needName) =>
        ParsePercentage(needsElement.Element(needName)?.Element("value"));

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

}
