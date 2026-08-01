// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Applies supported edits onto the original decrypted XML rather than serializing models into a
/// replacement document. That asymmetry with <see cref="SaveParser"/> is deliberate: unknown game
/// fields, ordering, attributes, and future-version data retain their original XML nodes unless
/// an edited structure must be rebuilt.
/// </summary>
internal static class SaveWriter
{
    /// <summary>
    /// Reuses the parser's document-order identity for members and inventory stacks because their
    /// apparent IDs and definition keys are not universally unique.
    /// </summary>
    internal static string ApplyEdits(
        string originalXml,
        IReadOnlyList<Character> characters,
        IReadOnlyList<Pet> pets,
        ShelterInventory? inventory)
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
        ApplyShelterInventory(document.Root, inventory);

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

        foreach (CharacterStat statKind in SaveFieldKind.CharacterStats)
        {
            string statName = statKind.XmlName();
            XElement? statElement = baseStatsElement.Element(statName);
            if (statElement is null)
            {
                continue;
            }

            Stat stat = character.GetStat(statKind);
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
                && TryParseInt(entry.Element("uniqueId")?.Value, out int id)
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

        // Relationship order is not guaranteed to match FamilyMembers order; memberID is the
        // explicit cross-reference here, unlike the positional identity used for member subtrees.
        Dictionary<int, Relationship> byMemberId = [];
        foreach (Relationship relationship in character.Relationships)
        {
            byMemberId[relationship.MemberId] = relationship;
        }

        foreach (XElement entry in relationships.Elements())
        {
            XElement? levelElement = entry.Element("relationshipLevel");
            if (levelElement is not null
                && TryParseInt(entry.Element("memberID")?.Value, out int memberId)
                && byMemberId.TryGetValue(memberId, out Relationship? relationship))
            {
                levelElement.Value = Int(relationship.Level);
            }
        }
    }

    private static void ApplyPets(XElement? root, IReadOnlyList<Pet> pets)
    {
        // Existing Pet_N elements are mutated in place to preserve unknown species state. Only
        // session-created pets go through PetXmlFactory's verified complete entry shape.
        if (root is null)
        {
            return;
        }

        Dictionary<int, XElement> elementsById = [];
        XElement? lastPetElement = null;
        foreach (XElement petElement in root.Elements().ToArray())
        {
            string name = petElement.Name.LocalName;
            if (name.StartsWith("Pet_", StringComparison.Ordinal)
                && TryParseInt(name.AsSpan(4), out int petId))
            {
                elementsById[petId] = petElement;
                lastPetElement = petElement;
            }
        }

        bool hasNewPets = pets.Any(pet => pet.PetId >= 0 && !elementsById.ContainsKey(pet.PetId));
        if (hasNewPets && root.Element("PetManager")?.Element("pets") is null)
        {
            throw new InvalidDataException("This save does not contain the PetManager list required to add a pet.");
        }

        XElement? shelterPosition = elementsById.Values.FirstOrDefault()?
            .Element("transform")?
            .Element("pos")
            ?? root.Element("FamilyMembers")?.Elements().FirstOrDefault()?
                .Element("transform")?
                .Element("pos");

        foreach (Pet pet in pets.Where(pet => pet.PetId >= 0))
        {
            if (!elementsById.TryGetValue(pet.PetId, out XElement? petElement))
            {
                petElement = PetXmlFactory.CreatePetElement(pet, shelterPosition);
                if (lastPetElement is not null)
                {
                    lastPetElement.AddAfterSelf(petElement);
                }
                else if (root.Element("FamilyMembers") is { } familyMembers)
                {
                    familyMembers.AddBeforeSelf(petElement);
                }
                else
                {
                    root.Add(petElement);
                }

                elementsById[pet.PetId] = petElement;
                lastPetElement = petElement;
            }

            ApplyPet(petElement, pet);
        }

        ApplyPetManager(root.Element("PetManager"), pets, elementsById);
    }

    private static void ApplyPet(XElement petElement, Pet pet)
    {
        SetValue(petElement, "name", pet.Name);
        SetValue(petElement, "age", Int(pet.Age));
        SetValue(petElement, "health", Int(pet.Health));
        SetValue(petElement, "hunger", Dbl(NormalizePercentage(pet.Hunger)));
        SetValue(petElement, "starving", Bool(pet.Starving));
        SetValue(petElement, "poisoned", Bool(pet.Poisoned));
        SetValue(petElement, "immune", Bool(pet.Immune));

        if (pet.IsCat)
        {
            foreach (PetSkillKind skillKind in SaveFieldKind.PetSkills)
            {
                string skillName = skillKind.XmlName();
                XElement? skillElement = petElement.Element(skillName);
                if (skillElement is null)
                {
                    continue;
                }

                PetSkill skill = pet.GetSkill(skillKind);

                SetValue(skillElement, "level", Int(skill.Level));
                SetValue(skillElement, "levelCap", Int(skill.LevelCap));
                SetValue(skillElement, "experience", Int(skill.Experience));
            }
        }

        if (pet.IsDog)
        {
            ApplyDogSkills(petElement.Element("Dog_Skills"), pet);
        }
    }

    private static void ApplyDogSkills(XElement? dogSkillsElement, Pet pet)
    {
        if (dogSkillsElement is null)
        {
            return;
        }

        ApplyDogSkillList(dogSkillsElement.Element("shelterSkills"), pet.ShelterSkills);
        ApplyDogSkillList(dogSkillsElement.Element("utilitySkills"), pet.UtilitySkills);
        ApplyDogSkillList(dogSkillsElement.Element("combatSkills"), pet.CombatSkills);
        SetValue(dogSkillsElement, "shelterPoints", Int(Math.Max(0, pet.ShelterSkillPoints)));
        SetValue(dogSkillsElement, "utilityPoints", Int(Math.Max(0, pet.UtilitySkillPoints)));
        SetValue(dogSkillsElement, "combatPoints", Int(Math.Max(0, pet.CombatSkillPoints)));
    }

    private static void ApplyDogSkillList(XElement? listElement, IReadOnlyList<DogSkill> skills)
    {
        if (listElement is null)
        {
            return;
        }

        Dictionary<int, XElement> existingByKey = [];
        foreach (XElement entry in listElement.Elements())
        {
            if (TryParseInt(entry.Element("skillKey")?.Value, out int key))
            {
                existingByKey[key] = entry;
            }
        }

        foreach (DogSkill skill in skills)
        {
            if (!existingByKey.TryGetValue(skill.Key, out XElement? entry))
            {
                entry = new XElement($"i{listElement.Elements().Count()}",
                    new XElement("skillKey", Int(skill.Key)),
                    new XElement("skillName", Int(skill.Key)),
                    new XElement("trainingTimeRequired", Dbl(skill.TrainingTimeRequired)),
                    new XElement("currentTrainingTime", "0"),
                    new XElement("purchased", "False"));
                listElement.Add(entry);
            }

            entry.SetElementValue("currentTrainingTime", Dbl(skill.CurrentTrainingTime));
            entry.SetElementValue("purchased", Bool(skill.Purchased));
        }

        listElement.SetAttributeValue("size", listElement.Elements().Count());
    }

    private static void ApplyPetManager(
        XElement? petManagerElement,
        IReadOnlyList<Pet> pets,
        IReadOnlyDictionary<int, XElement> petElementsById)
    {
        XElement? entries = petManagerElement?.Element("pets");
        if (entries is null)
        {
            return;
        }

        Dictionary<int, XElement> entriesById = [];
        foreach (XElement entry in entries.Elements())
        {
            if (TryParseInt(entry.Element("uniqueId")?.Value, out int id))
            {
                entriesById[id] = entry;
            }
        }

        XElement? fallbackRotation = entries.Elements().FirstOrDefault()?.Element("spawnRot");
        bool addedEntry = false;
        foreach (Pet pet in pets.Where(pet => pet.PetId >= 0))
        {
            if (entriesById.TryGetValue(pet.PetId, out XElement? entry))
            {
                if (pet.Species != PetSpecies.Unknown)
                {
                    SetValue(entry, "petSpecies", Int((int)pet.Species));
                }

                continue;
            }

            if (!petElementsById.TryGetValue(pet.PetId, out XElement? petElement))
            {
                continue;
            }

            int index = NextEntryIndex(entries);
            entry = PetXmlFactory.CreateManagerEntry(
                index,
                pet,
                petElement.Element("transform")?.Element("pos") ?? new XElement("pos"),
                fallbackRotation);
            entries.Add(entry);
            entriesById[pet.PetId] = entry;
            addedEntry = true;
        }

        if (!addedEntry)
        {
            return;
        }

        entries.SetAttributeValue("size", entries.Elements().Count());

        int nextId = pets.Count == 0 ? 0 : pets.Max(pet => pet.PetId) + 1;
        int savedNextId = TryParseInt(petManagerElement?.Element("uniqueID")?.Value, out int value) ? value : 0;
        petManagerElement?.SetElementValue("uniqueID", Int(Math.Max(savedNextId, nextId)));
    }

    private static int NextEntryIndex(XElement entries)
    {
        HashSet<string> names = [.. entries.Elements().Select(entry => entry.Name.LocalName)];
        int index = 0;
        while (names.Contains($"i{index}"))
        {
            index++;
        }

        return index;
    }

    private static void ApplySkills(XElement? professionElement, XElement? baseStatsElement, Character character)
    {
        if (professionElement is null)
        {
            return;
        }

        foreach (CharacterStat statKind in SaveFieldKind.CharacterStats)
        {
            string statName = statKind.XmlName();
            XElement? listElement = professionElement
                .Element($"{statName}Skills")?
                .Element(statKind.SkillListXmlName());
            if (listElement is null)
            {
                continue;
            }

            ObservableCollection<SkillInstance> tree = character.GetSkillTree(statKind);

            // Keep the extra per-skill fields (accuracy/damage/...) of skills that already
            // existed, keyed by skillKey, so re-saving doesn't wipe in-game upgrades.
            Dictionary<int, XElement> existingByKey = [];
            foreach (XElement entry in listElement.Elements())
            {
                if (TryParseInt(entry.Element("skillKey")?.Value, out int key))
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

            UpdatePointsSpentCounter(baseStatsElement?.Element(statName), statKind, tree);
        }
    }

    /// <summary>
    /// Keeps <c>pointsSpent_tierOne</c> consistent with the invested skills. The game uses
    /// this counter to unlock tiers 2 and 3 (at 5 and 10 points); without it, skills added
    /// to a locked tier appear locked in-game.
    /// </summary>
    private static void UpdatePointsSpentCounter(XElement? statElement, CharacterStat stat, ObservableCollection<SkillInstance> tree)
    {
        XElement? counterElement = statElement?.Element("pointsSpent_tierOne");
        if (counterElement is null)
        {
            return;
        }

        Dictionary<int, int> tierByKey = SkillCatalog.ForStat(stat).ToDictionary(d => d.Key, d => d.Tier);

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
        int current = TryParseInt(counterElement.Value, out int value) ? value : 0;
        counterElement.Value = Int(Math.Max(current, required));
    }

    private static XElement BuildSkillEntry(int index, SkillInstance skill, XElement? existing)
    {
        XElement entry = existing is null
            ? new XElement($"i{index}")
            : new XElement(existing);

        entry.Name = $"i{index}";
        entry.SetElementValue("skillKey", Int(skill.Key));
        entry.SetElementValue("skillLevel", Int(skill.Level));
        entry.SetElementValue("accuracyLevel", Int(ChildInt(existing, "accuracyLevel")));
        entry.SetElementValue("damageLevel", Int(ChildInt(existing, "damageLevel")));
        entry.SetElementValue("staminaLevel", Int(ChildInt(existing, "staminaLevel")));
        entry.SetElementValue("chanceLevel", Int(ChildInt(existing, "chanceLevel")));
        return entry;
    }

    /// <summary>
    /// Applies inventory edits while retaining the full XML of surviving source entries. Source
    /// indices preserve duplicate definition keys; new entries use the verified game entry shape.
    /// </summary>
    private static void ApplyShelterInventory(XElement? root, ShelterInventory? inventory)
    {
        if (root is null || inventory is null)
        {
            return;
        }

        root.Element("StoredWater")?.SetValue(Int(inventory.StoredWater));

        XElement? shelterInventoryElement = root.Element("ShelterInventory");
        ApplyInventoryContainer(
            shelterInventoryElement?.Element("Storage")?.Element("Inventory"),
            inventory.Storage);
        ApplyInventoryContainer(
            shelterInventoryElement?.Element("Overflow")?.Element("Inventory"),
            inventory.Overflow);
    }

    private static void ApplyInventoryContainer(XElement? inventoryElement, InventoryContainer? container)
    {
        XElement? contents = inventoryElement?.Element("InventoryContents");
        if (contents is null || container is null)
        {
            return;
        }

        List<XElement> sourceEntries = [.. contents.Elements()];
        bool structureChanged = sourceEntries.Count != container.Items.Count;
        if (!structureChanged)
        {
            for (int index = 0; index < container.Items.Count; index++)
            {
                if (container.Items[index].SourceIndex != index)
                {
                    structureChanged = true;
                    break;
                }
            }
        }

        if (!structureChanged)
        {
            for (int index = 0; index < container.Items.Count; index++)
            {
                ApplyInventoryItemValues(sourceEntries[index], container.Items[index]);
            }

            return;
        }

        List<XElement> updatedEntries = new(container.Items.Count);
        for (int index = 0; index < container.Items.Count; index++)
        {
            InventoryItem item = container.Items[index];
            int sourceIndex = item.SourceIndex ?? -1;
            bool isSourceEntry = sourceIndex >= 0 && sourceIndex < sourceEntries.Count;
            XElement entry = isSourceEntry
                ? sourceEntries[sourceIndex]
                : BuildInventoryEntry(item);

            entry.Name = $"i{index}";
            if (isSourceEntry)
            {
                ApplyInventoryItemValues(entry, item);
            }

            updatedEntries.Add(entry);
        }

        contents.ReplaceNodes(updatedEntries);
        contents.SetAttributeValue("size", Int(updatedEntries.Count));
    }

    private static XElement BuildInventoryEntry(InventoryItem item) =>
        new("i0",
            new XElement("defKey", item.DefinitionKey),
            new XElement("lastUpdateTime", "0"),
            new XElement("amount", Int(item.Amount)),
            new XElement("id", "0"),
            new XElement("integrity", Int(item.Integrity)),
            new XElement("quality", Int(item.Quality)),
            new XElement("spawnType", "0"));

    private static void ApplyInventoryItemValues(XElement entry, InventoryItem item)
    {
        SetValue(entry, "amount", Int(item.Amount));
        SetValue(entry, "integrity", Int(item.Integrity));
        SetValue(entry, "quality", Int(item.Quality));
    }

    private static void SetValue(XElement parent, string childName, string value)
    {
        // Supported edits never create a missing field in an existing subtree: absence may signal
        // a different save-version schema, and guessing a location would be destructive.
        XElement? child = parent.Element(childName);
        child?.Value = value;
    }

    private static void SetNeedValue(XElement needsElement, string needName, double value)
    {
        XElement? valueElement = needsElement.Element(needName)?.Element("value");
        valueElement?.Value = Dbl(NormalizePercentage(value));
    }

    private static double NormalizePercentage(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;

    private static int ChildInt(XElement? parent, string childName) =>
        TryParseInt(parent?.Element(childName)?.Value, out int value) ? value : 0;

    private static bool TryParseInt(string? value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseInt(ReadOnlySpan<char> value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    // Match the game's capitalization so edits do not introduce a second lexical form.
    private static string Bool(bool value) => value ? "True" : "False";

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    // Round-trip invariant formatting preserves precision and avoids locale-specific decimal commas.
    private static string Dbl(double value) => value.ToString("R", CultureInfo.InvariantCulture);

}
