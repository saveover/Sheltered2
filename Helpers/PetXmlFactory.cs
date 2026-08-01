// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Materializes the verified minimum XML required for a newly added pet. Existing pets are edited
/// in place by <see cref="SaveWriter"/>, but a new pet has no source subtree to preserve, so its
/// species-specific state, manager entry, and inert world objects must be supplied together.
/// </summary>
internal static class PetXmlFactory
{
    /// <summary>
    /// Places the pet at a known shelter position rather than inventing coordinates; zero is only
    /// a fallback for saves with no usable donor transform.
    /// </summary>
    internal static XElement CreatePetElement(Pet pet, XElement? shelterPosition)
    {
        XElement position = CopyVector(shelterPosition, "pos", ("x", "0"), ("y", "0"), ("z", "0"));
        XElement petElement = new($"Pet_{pet.PetId}",
            new XElement("transform",
                new XElement(position),
                Vector("scale", ("x", "1"), ("y", "1"), ("z", "1")),
                Vector("rot", ("x", "0"), ("y", "0"), ("z", "0"))),
            new XElement("Navigation",
                CopyVector(position, "targetPos", ("x", "0"), ("y", "0"), ("z", "0")),
                new XElement("walkSpeedMultiplier", "1"),
                new XElement("walkSpeedTraitMultiplier", "1")),
            new XElement("name", pet.Name),
            new XElement("age", pet.Age.ToString(CultureInfo.InvariantCulture)),
            new XElement("dead", "False"),
            new XElement("health", pet.Health.ToString(CultureInfo.InvariantCulture)),
            new XElement("hunger", pet.Hunger.ToString("R", CultureInfo.InvariantCulture)),
            new XElement("starving", "False"),
            new XElement("poisoned", "False"),
            new XElement("starvationTimer", "0"),
            new XElement("stateTimer", "0"),
            new XElement("immune", "False"),
            new XElement("immuneTimer", "0"),
            new XElement("isAway", "False"),
            new XElement("bowlID", "-1"),
            CreateSpawnedObjects(position),
            new XElement("sleepPlaying", "False"),
            CreateCurrentState(pet.Species, position),
            new XElement("interacting", "False"),
            new XElement("running", "False"),
            new XElement("animHash", pet.Species == PetSpecies.Dog ? "73253703" : "1551589887"),
            new XElement("animTime", "0"),
            new XElement("starvDamageTime", "0"),
            new XElement("suffocationDamageTime", "0"),
            new XElement("waitingToDie", "False"),
            new XElement("Apperance_",
                new XElement("appearanceIndex", "0"),
                new XElement("saddlebagTrained", "False")),
            new XElement("sleepTimer", "0"));

        if (pet.Species == PetSpecies.Dog)
        {
            petElement.Add(CreateDogSkills(pet));
            petElement.Add(new XElement("digTimer", "0"));
        }
        else
        {
            petElement.Add(
                new XElement("preyDriveXPTimer", "300"),
                new XElement("scavengingXPTimer", "300"),
                new XElement("affectionXPTimer", "300"),
                new XElement("playwithtoytimer", "0"),
                new XElement("damageObjTimer", "0"),
                new XElement("hunRatsTimer", "0"),
                new XElement("lastToy", "322"),
                new XElement("damageObjId", "-1"),
                CreateCatSkill("PreyDrive", pet.PreyDrive),
                CreateCatSkill("Scavenging", pet.Scavenging),
                CreateCatSkill("Affection", pet.Affection));
        }

        return petElement;
    }

    /// <summary>
    /// Keeps the PetManager index synchronized with the root <c>Pet_N</c> element; the game uses
    /// both structures and cannot discover a newly appended root element on its own.
    /// </summary>
    internal static XElement CreateManagerEntry(int index, Pet pet, XElement petPosition, XElement? spawnRotation) =>
        new($"i{index}",
            new XElement("uniqueId", pet.PetId.ToString(CultureInfo.InvariantCulture)),
            CopyVector(petPosition, "spawnPos", ("x", "0"), ("y", "0"), ("z", "0")),
            CopyVector(spawnRotation, "spawnRot", ("w", "1"), ("x", "0"), ("y", "0"), ("z", "0")),
            new XElement("petSpecies", ((int)pet.Species).ToString(CultureInfo.InvariantCulture)));

    private static XElement CreateSpawnedObjects(XElement position) =>
        new("spawned_objects",
            CreateSpawnedObject("object_-100", position, previouslyConstructed: false, includeExtraState: true),
            CreateSpawnedObject("object_-200", position, previouslyConstructed: true, includeExtraState: false));

    private static XElement CreateSpawnedObject(
        string name,
        XElement position,
        bool previouslyConstructed,
        bool includeExtraState)
    {
        XElement element = new(name,
            new XElement("wasInitialObject", "False"),
            new XElement("canBeMoved", "False"),
            new XElement("transform",
                new XElement(position),
                Vector("scale", ("x", "1"), ("y", "1"), ("z", "1")),
                Vector("rot", ("x", "0"), ("y", "0"), ("z", "0"))),
            new XElement("withinHoldingCell", "False"),
            new XElement("constuctionAmount", "100"),
            new XElement("previouslyConstructed", previouslyConstructed ? "True" : "False"),
            new XElement("isPowered", "True"),
            new XElement("isSwitchOn", "True"),
            new XElement("beingUsed", "False"),
            new XElement("name", string.Empty),
            new XElement("selectable", "True"),
            new XElement("breachNPCID", "-1"),
            new XElement("usedByPetID", "-1"));

        if (includeExtraState)
        {
            element.Add(
                new XElement("unconsciousTimer", "0"),
                new XElement("flyState", "False"),
                new XElement("interactingmember", "-1"));
        }

        return element;
    }

    private static XElement CreateCurrentState(PetSpecies species, XElement position) =>
        species == PetSpecies.Dog
            ? new XElement("PetCurrentState",
                new XElement("stateEnum", "0"),
                new XElement("entered", "True"),
                new XElement("timer", "0"),
                new XElement("idleTimer", "0"),
                new XElement("idleTriggerTime", "1"),
                new XElement("idleTriggered", "False"))
            : new XElement("PetCurrentState",
                new XElement("stateEnum", "18"),
                new XElement("entered", "True"),
                new XElement("timer", "0"),
                CopyVector(position, "pos", ("x", "0"), ("y", "0"), ("z", "0")),
                new XElement("arrivalState", "99"));

    private static XElement CreateCatSkill(string name, PetSkill skill) =>
        new(name,
            new XElement("level", skill.Level.ToString(CultureInfo.InvariantCulture)),
            new XElement("levelCap", skill.LevelCap.ToString(CultureInfo.InvariantCulture)),
            new XElement("experience", skill.Experience.ToString(CultureInfo.InvariantCulture)));

    private static XElement CreateDogSkills(Pet pet) =>
        new("Dog_Skills",
            CreateDogSkillList("shelterSkills", pet, DogSkillCategory.Shelter),
            CreateDogSkillList("utilitySkills", pet, DogSkillCategory.Utility),
            CreateDogSkillList("combatSkills", pet, DogSkillCategory.Combat),
            new XElement("shelterPoints", pet.ShelterSkillPoints.ToString(CultureInfo.InvariantCulture)),
            new XElement("utilityPoints", pet.UtilitySkillPoints.ToString(CultureInfo.InvariantCulture)),
            new XElement("combatPoints", pet.CombatSkillPoints.ToString(CultureInfo.InvariantCulture)),
            new XElement("skillBeingTrained", "999"));

    private static XElement CreateDogSkillList(string name, Pet pet, DogSkillCategory category)
    {
        // The size attribute counts serialized entries and iN names are positional, so build both
        // from one deterministic sequence rather than trusting collection insertion history.
        DogSkill[] skills = [.. pet.DogSkills
            .Where(skill => skill.Category == category)
            .OrderBy(skill => skill.Key)];
        XElement list = new(name, new XAttribute("size", skills.Length));
        for (int index = 0; index < skills.Length; index++)
        {
            DogSkill skill = skills[index];
            list.Add(new XElement($"i{index}",
                new XElement("skillKey", skill.Key.ToString(CultureInfo.InvariantCulture)),
                new XElement("skillName", skill.Key.ToString(CultureInfo.InvariantCulture)),
                new XElement("trainingTimeRequired", skill.TrainingTimeRequired.ToString("R", CultureInfo.InvariantCulture)),
                new XElement("currentTrainingTime", skill.CurrentTrainingTime.ToString("R", CultureInfo.InvariantCulture)),
                new XElement("purchased", skill.Purchased ? "True" : "False")));
        }

        return list;
    }

    private static XElement CopyVector(
        XElement? source,
        string targetName,
        params (string Name, string DefaultValue)[] attributes)
    {
        // Copy only the schema's known axes. Cloning a donor element wholesale could carry its
        // element name or future unrelated attributes into a different vector role.
        XElement result = new(targetName);
        foreach ((string name, string defaultValue) in attributes)
        {
            result.SetAttributeValue(name, source?.Attribute(name)?.Value ?? defaultValue);
        }

        return result;
    }

    private static XElement Vector(string name, params (string Name, string Value)[] attributes)
    {
        XElement result = new(name);
        foreach ((string attributeName, string value) in attributes)
        {
            result.SetAttributeValue(attributeName, value);
        }

        return result;
    }
}
