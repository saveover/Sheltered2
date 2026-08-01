// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.Collections.Generic;
using System.Linq;

namespace SaveOver.Sheltered2.Models;

/// <summary>
/// Couples presentation data to the numeric key needed for sparse save-list matching. Keeping icon
/// IDs beside keys makes a mismatched asset or skill identity reviewable in one catalog row.
/// </summary>
public sealed class SkillDefinition(string id, int key, string stat, int tier, string name, int maxLevel)
{
    public string Id { get; } = id;

    public int Key { get; } = key;

    public CharacterStat Stat { get; } = Enum.Parse<CharacterStat>(stat, ignoreCase: false);

    public int Tier { get; } = tier;

    public string Name { get; } = name;

    public int MaxLevel { get; } = maxLevel;

    /// <summary>Uses a fixed package URI so save data never participates in resource resolution.</summary>
    public Uri ImageUri { get; } = new($"ms-appx:///Assets/Skills/{id}.png");
}

/// <summary>
/// Provides one authoritative mapping for parser identity, writer identity, UI grouping, and local
/// artwork. Keeping those concerns in the same reviewed rows prevents a display reorder from being
/// mistaken for a save-key reorder.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SkillDefinition.Key"/> values are the actual <c>skillKey</c> values the
/// game writes to the save file, so a character's invested points map to the correct skill.
/// Tiers and maximum ranks were cross-checked against the Sheltered 2 wiki, and every icon
/// id matches a file in <c>Assets/Skills</c>.
/// </para>
/// <para>
/// The number of skills per tree matches the save's per-tree <c>size</c> ceiling which
/// counts unlocked skills, not invested points.
/// </para>
/// </remarks>
public static class SkillCatalog
{
    /// <summary>Shares save-format order with parser and writer rather than maintaining a UI copy.</summary>
    public static IReadOnlyList<CharacterStat> Stats => SaveFieldKind.CharacterStats;

    public static IReadOnlyList<SkillDefinition> All { get; } =
    [
        // Strength
        new("SkillStrengthCrushWindpipe", 15, "Strength", 1, "Crush Windpipe", 3),
        new("SkillStrengthPoisonPunch", 19, "Strength", 1, "Poison Punch", 3),
        new("SkillStrengthBackpackWeightTraining", 12, "Strength", 1, "Backpack Weight Training", 3),
        new("SkillStrengthPumpUp", 41, "Strength", 1, "Pump Up", 1),
        new("SkillStrengthInherentStrength", 30, "Strength", 1, "Inherent Strength", 3),
        new("SkillStrengthBluntForceSpecialisation", 28, "Strength", 1, "Blunt Force Specialisation", 3),
        new("SkillStrengthImposingPhysique", 25, "Strength", 1, "Imposing Physique", 1),
        new("SkillStrengthHeadbutt", 4, "Strength", 2, "Headbutt", 3),
        new("SkillStrengthSetBone", 42, "Strength", 2, "Set Bone", 3),
        new("SkillStrengthKick", 0, "Strength", 2, "Kick", 3),
        new("SkillStrengthShoulderBarge", 8, "Strength", 2, "Shoulder Barge", 3),
        new("SkillStrengthUtilitySpecialist", 24, "Strength", 3, "Utility Specialist", 2),
        new("SkillStrengthExplodingHeartAttack", 31, "Strength", 3, "Exploding Heart Attack", 3),
        new("SkillStrengthThunderousUppercut", 37, "Strength", 3, "Thunderous Uppercut", 3),

        // Dexterity
        new("SkillDexteritySprayGunshot", 102, "Dexterity", 1, "Spray Gunshot", 3),
        new("SkillDexterityRangedWeaponTraining", 105, "Dexterity", 1, "Ranged Weapon Training", 3),
        new("SkillDexterityFastReflexes", 131, "Dexterity", 1, "Fast Reflexes", 3),
        new("SkillDexterityBladeSpecialisation", 143, "Dexterity", 1, "Blade Specialisation", 3),
        new("SkillDexterityFlickSand", 110, "Dexterity", 1, "Flick Sand Attack", 3),
        new("SkillDexteritySleightOfHand", 122, "Dexterity", 1, "Sleight Of Hand Attack", 3),
        new("SkillDexterityAimedGunshot", 100, "Dexterity", 2, "Aimed Gunshot", 3),
        new("SkillDexterityKnickArtery", 115, "Dexterity", 2, "Knick Artery", 3),
        new("SkillDexterityRetreatAttack", 139, "Dexterity", 2, "Retreat Attack", 3),
        new("SkillDexterityBackstab", 127, "Dexterity", 3, "Backstab", 3),
        new("SkillDexterityDisarm", 106, "Dexterity", 3, "Disarm", 1),
        new("SkillDexterityCQCTraining", 104, "Dexterity", 3, "CQC Training", 3),

        // Intelligence
        new("SkillIntelligenceEmergencyTourniquet", 214, "Intelligence", 1, "Emergency Tourniquet", 1),
        new("SkillIntelligenceFocused", 200, "Intelligence", 1, "Focused", 3),
        new("SkillIntelligenceMedicalTraining", 208, "Intelligence", 1, "Medical Training", 3),
        new("SkillIntelligenceTactician", 229, "Intelligence", 1, "Tactician", 3),
        new("SkillIntelligencePuttingOnABraveFace", 240, "Intelligence", 1, "Putting On A Brave Face", 1),
        new("SkillIntelligenceThickSkinned", 234, "Intelligence", 1, "Thick Skinned", 3),
        new("SkillIntelligenceDistractionTactics", 230, "Intelligence", 1, "Distraction Tactics", 3),
        new("SkillIntelligenceMentalFortifications", 239, "Intelligence", 1, "Mental Fortifications", 3),
        new("SkillIntelligenceEmergencyHealing", 210, "Intelligence", 2, "Emergency Healing", 3),
        new("SkillIntelligenceAdvancedCPRTraining", 201, "Intelligence", 2, "Advanced CPR Training", 1),
        new("SkillIntelligenceKnowledgeOfAnatomy", 213, "Intelligence", 2, "Knowledge Of Anatomy", 3),
        new("SkillIntelligenceCombatAnalysis", 241, "Intelligence", 2, "Combat Analysis", 3),
        new("SkillIntelligenceResourcefulHealing", 209, "Intelligence", 2, "Resourceful Healing", 1),
        new("SkillIntelligenceCalculatedOneTwo", 235, "Intelligence", 3, "Calculated One-Two", 3),
        new("SkillIntelligenceSurgeon", 206, "Intelligence", 3, "Surgeon", 3),
        new("SkillIntelligenceExperiment", 243, "Intelligence", 3, "Experiment", 1),
        new("SkillIntelligenceImprovisedExplosive", 224, "Intelligence", 3, "Improvised Explosive", 1),

        // Charisma
        new("SkillCharismaSoothingWords", 327, "Charisma", 1, "Soothing Words", 1),
        new("SkillCharismaInspiring", 309, "Charisma", 1, "Inspiring", 3),
        new("SkillCharismaBedsideManner", 302, "Charisma", 1, "Bedside Manner", 3),
        new("SkillCharismaMotivator", 304, "Charisma", 1, "Motivator", 3),
        new("SkillCharismaWelcoming", 316, "Charisma", 1, "Welcoming", 1),
        new("SkillCharismaProductionManager", 319, "Charisma", 1, "Production Manager", 3),
        new("SkillCharismaConvincingVoice", 320, "Charisma", 1, "Convincing Voice", 3),
        new("SkillCharismaConfuseOpponent", 324, "Charisma", 2, "Confuse Opponent", 1),
        new("SkillCharismaMarchingSongs", 301, "Charisma", 2, "Marching Songs", 3),
        new("SkillCharismaPlaceboEffect", 429, "Charisma", 2, "Placebo Effect", 3),
        new("SkillCharismaMissionOfMercy", 326, "Charisma", 2, "Mission Of Mercy", 1),
        new("SkillCharismaSilverTongue", 306, "Charisma", 3, "Silver Tongue", 1),
        new("SkillCharismaRallying", 300, "Charisma", 3, "Rallying", 3),

        // Perception
        new("SkillPerceptionAssessOpponent", 405, "Perception", 1, "Assess Opponent", 1),
        new("SkillPerceptionAlwaysPrepared", 433, "Perception", 1, "Always Prepared", 3),
        new("SkillPerceptionUnshakeable", 432, "Perception", 1, "Unshakeable", 3),
        new("SkillPerceptionStudyMovements", 431, "Perception", 1, "Study Movements", 3),
        new("SkillPerceptionQuickStudy", 406, "Perception", 1, "Quick Study", 3),
        new("SkillPerceptionExpeditedHealing", 424, "Perception", 1, "Expedited Healing", 1),
        new("SkillPerceptionPoisonResilience", 507, "Perception", 1, "Poison Resilience", 3),
        new("SkillPerceptionTaunt", 402, "Perception", 2, "Taunt", 1),
        new("SkillPerceptionAutopsy", 425, "Perception", 2, "Autopsy", 3),
        new("SkillPerceptionEideticMemory", 420, "Perception", 2, "Eidetic Memory", 3),
        new("SkillPerceptionRelishesAChallenge", 517, "Perception", 2, "Relishes A Challenge", 1),
        new("SkillPerceptionHunter", 413, "Perception", 2, "Hunter", 3),
        new("SkillPerceptionTherapist", 403, "Perception", 3, "Therapist", 1),
        new("SkillPerceptionReturnToSender", 419, "Perception", 3, "Return To Sender", 1),
        new("SkillPerceptionAutomaticRepairing", 416, "Perception", 3, "Automatic Repairing", 1),
        new("SkillPerceptionDemoralise", 410, "Perception", 3, "Demoralise", 1),
        new("SkillPerceptionLocateWeakpoint", 422, "Perception", 3, "Locate Weakpoint", 1),

        // Fortitude
        new("SkillFortitudeExtractPoison", 519, "Fortitude", 1, "Extract Poison", 1),
        new("SkillFortitudePainResistanceTraining", 500, "Fortitude", 1, "Pain Resistance Training", 3),
        new("SkillFortitudeShakeItOff", 502, "Fortitude", 1, "Shake It Off", 3),
        new("SkillFortitudeIronStomach", 505, "Fortitude", 1, "Iron Stomach", 3),
        new("SkillFortitudeStrongImmuneSystem", 524, "Fortitude", 1, "Strong Immune System", 3),
        new("SkillFortitudeFastHealer", 518, "Fortitude", 1, "Fast Healer", 3),
        new("SkillFortitudeTirelessEngineering", 522, "Fortitude", 1, "Tireless Engineering", 3),
        new("SkillFortitudeHomeTurfAdvantage", 513, "Fortitude", 1, "Home Turf Advantage", 3),
        new("SkillFortitudeUnarmedSpecialisation", 515, "Fortitude", 1, "Unarmed Specialisation", 3),
        new("SkillFortitudeBloodTransfusion", 506, "Fortitude", 2, "Blood Transfusion", 1),
        new("SkillFortitudeHardenedSkin", 501, "Fortitude", 2, "Hardened Skin", 3),
        new("SkillFortitudeValiant", 504, "Fortitude", 2, "Valiant", 1),
        new("SkillFortitudeSharedHealing", 521, "Fortitude", 2, "Shared Healing", 1),
        new("SkillFortitudeWorkingLongHours", 523, "Fortitude", 2, "Working Long Hours", 1),
        new("SkillFortitudeWarmBlooded", 512, "Fortitude", 2, "Warm Blooded", 3),
        new("SkillFortitudeRageAttack", 526, "Fortitude", 3, "Rage Attack", 3),
        new("SkillFortitudeFinalCounterDown", 503, "Fortitude", 3, "Final Counter Down", 1),
        new("SkillFortitudeDeterminedToWin", 509, "Fortitude", 3, "Determined To Win", 1),
        new("SkillFortitudeHardy", 510, "Fortitude", 3, "Hardy", 1),
        new("SkillFortitudePatchYourselfUp", 516, "Fortitude", 3, "Patch Yourself Up", 3),
    ];

    private static readonly Dictionary<CharacterStat, IReadOnlyList<SkillDefinition>> ByStat =
        All.GroupBy(s => s.Stat)
           .ToDictionary(g => g.Key, g => (IReadOnlyList<SkillDefinition>)[.. g]);

    /// <summary>Returns an empty list for unknown enum values so corrupt UI state cannot mutate a tree.</summary>
    public static IReadOnlyList<SkillDefinition> ForStat(CharacterStat stat) =>
        ByStat.TryGetValue(stat, out IReadOnlyList<SkillDefinition>? skills) ? skills : [];
}
