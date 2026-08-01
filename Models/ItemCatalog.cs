// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaveOver.Sheltered2.Models;

/// <summary>
/// Trusted metadata for a known game definition. Construction is assembly-only so fixed asset
/// paths and game constraints cannot be supplied from save-file input.
/// </summary>
public sealed class InventoryItemDefinition
{
    internal InventoryItemDefinition(
        string definitionKey,
        string displayName,
        InventoryCategory category,
        string imageAssetFileName,
        int minimumQuality = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageAssetFileName);

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (minimumQuality is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumQuality),
                minimumQuality,
                "Quality must use the game's zero-based range from 0 to 2.");
        }

        if (!IsSafePngFileName(imageAssetFileName))
        {
            throw new ArgumentException(
                "The image asset must be a lowercase PNG file name without a path.",
                nameof(imageAssetFileName));
        }

        DefinitionKey = definitionKey;
        DisplayName = displayName;
        Category = category;
        MinimumQuality = minimumQuality;
        ImageAssetPath = $"ms-appx:///Assets/Inventory/{imageAssetFileName}";
    }

    public string DefinitionKey { get; }

    public string DisplayName { get; }

    public InventoryCategory Category { get; }

    /// <summary>The lowest zero-based quality value the game accepts for this item.</summary>
    public int MinimumQuality { get; }

    /// <summary>A packaged, fixed asset path rather than a URL derived from save-file data.</summary>
    public string ImageAssetPath { get; }

    private static bool IsSafePngFileName(string value)
    {
        // A narrow allowlist makes the catalog incapable of escaping Assets/Inventory even if a
        // future entry is copied from an untrusted source.
        const string Extension = ".png";
        if (!value.EndsWith(Extension, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> stem = value.AsSpan(0, value.Length - Extension.Length);
        if (stem.IsEmpty)
        {
            return false;
        }

        foreach (char character in stem)
        {
            if (!char.IsAsciiLetterLower(character)
                && !char.IsAsciiDigit(character)
                && character != '-')
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Curated metadata imported from the supplied item catalog and paired with locally packaged
/// Sheltered 2 Wiki artwork. Save-file input is only resolved through this ordinal,
/// case-insensitive dictionary.
/// </summary>
public static class ItemCatalog
{
    private static readonly ReadOnlyCollection<InventoryItemDefinition> Items =
        Array.AsReadOnly<InventoryItemDefinition>
        ([
        // Junk
        new("BrokenGamesConsole", "Broken Games Console", InventoryCategory.Junk, "item-junk-broken-games-console.png"),
        new("BrokenLaptop", "Broken Laptop", InventoryCategory.Junk, "item-junk-broken-laptop.png"),
        new("BrokenRadio", "Broken Radio", InventoryCategory.Junk, "item-junk-broken-radio.png"),
        new("BrokenTV", "Broken TV", InventoryCategory.Junk, "item-junk-broken-tv.png"),
        new("BrokenWoodenToys", "Broken Wooden Toys", InventoryCategory.Junk, "item-junk-broken-wooden-toys.png"),
        new("burnedClothes", "Burned Clothes", InventoryCategory.Junk, "item-junk-burned-clothes.png"),
        new("EmptyPetrolCan", "Empty Petrol Can", InventoryCategory.Junk, "item-junk-empty-petrol-can.png"),
        new("GlassJar", "Glass Jar", InventoryCategory.Junk, "item-junk-glass-jar.png"),
        new("Logs", "Log", InventoryCategory.Junk, "item-junk-log.png"),
        new("MineralOre", "Mineral Ore", InventoryCategory.Junk, "item-junk-mineral-ore.png"),
        new("PoppedYogaBall", "Popped Yoga Ball", InventoryCategory.Junk, "item-junk-popped-yoga-ball.png"),
        new("PuncturedTyre", "Punctured Tyre", InventoryCategory.Junk, "item-junk-punctured-tyre.png"),
        new("RustyFryingPan", "Rusty Frying Pan", InventoryCategory.Junk, "item-junk-rusty-frying-pan.png"),
        new("ScrapPile", "Scrap Pile", InventoryCategory.Junk, "item-junk-scrap-pile.png"),
        new("SmashedMicrowave", "Smashed Microwave", InventoryCategory.Junk, "item-junk-smashed-microwave.png"),
        new("SplinteredCrate", "Splintered Crate", InventoryCategory.Junk, "item-junk-splintered-crate.png"),

        // Valuables
        new("goldingot", "Gold Ingot", InventoryCategory.Valuables, "item-valuables-gold-ingot.png"),
        new("goldnugget", "Gold Nugget", InventoryCategory.Valuables, "item-valuables-gold-nugget.png"),
        new("goldscrap", "Gold Scrap", InventoryCategory.Valuables, "item-valuables-gold-scrap.png"),
        new("silver", "Silver Ingot", InventoryCategory.Valuables, "item-valuables-silver-ingot.png"),
        new("silvernugget", "Silver Nugget", InventoryCategory.Valuables, "item-valuables-silver-nugget.png"),
        new("silverscrap", "Silver Scrap", InventoryCategory.Valuables, "item-valuables-silver-scrap.png"),

        // Tools
        new("Chisel", "Chisel", InventoryCategory.Tools, "item-tools-chisel.png"),
        new("Drill", "Drill", InventoryCategory.Tools, "item-tools-drill.png"),
        new("Hammer", "Hammer", InventoryCategory.Tools, "item-tools-hammer.png"),
        new("Handsaw", "Hand Saw", InventoryCategory.Tools, "item-tools-hand-saw.png"),
        new("Nailgun", "Nail Gun", InventoryCategory.Tools, "item-tools-nail-gun.png"),
        new("Pliers", "Pliers", InventoryCategory.Tools, "item-tools-pliers.png"),
        new("Ratchet", "Ratchet", InventoryCategory.Tools, "item-tools-ratchet.png"),
        new("Sander", "Sander", InventoryCategory.Tools, "item-tools-sander.png"),
        new("Screwdriver", "Screwdriver", InventoryCategory.Tools, "item-tools-screwdriver.png"),
        new("Wrench", "Wrench", InventoryCategory.Tools, "item-tools-wrench.png"),

        // SpecialItems
        new("batteryBankBlueprint", "Blueprint (Battery Bank)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("defibBlueprint", "Blueprint (Defibrillator)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("efficientPlanterBlueprint", "Blueprint (Efficient Planter)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("electricityTrapBlueprint", "Blueprint (Electricity Trap)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("flashbangMineBlueprint", "Blueprint (Flashbang Proximity Mine)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("gasMineBlueprint", "Blueprint (Gas Proximity Mine)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("industrialGenBlueprint", "Blueprint (Industrial Generator)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("laboratoryBlueprint", "Blueprint (Laboratory)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("medicalBedBlueprint", "Blueprint (Medical Bed)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("quantumBatteryBlueprint", "Blueprint (Quantum Battery)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("recyclerBlueprint", "Blueprint (Recycler)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("solarPanelBlueprint", "Blueprint (Solar Panel)", InventoryCategory.SpecialItems, "item-misc-blueprint.png"),
        new("FloppyDisk", "Floppy Disk", InventoryCategory.SpecialItems, "item-misc-floppy-disk.png"),

        // Materials
        new("animalFat", "Animal Fat", InventoryCategory.Materials, "animal-fat.png"),
        new("Battery", "Battery", InventoryCategory.Materials, "item-materials-battery.png"),
        new("Bone", "Bone", InventoryCategory.Materials, "item-materials-bone.png"),
        new("Bucket", "Bucket", InventoryCategory.Materials, "bucket.png"),
        new("Bulb", "Bulb", InventoryCategory.Materials, "bulb.png"),
        new("Cement", "Cement", InventoryCategory.Materials, "item-materials-cement.png"),
        new("Chain", "Chain", InventoryCategory.Materials, "item-materials-chain.png"),
        new("CircuitBoard", "Circuit Board", InventoryCategory.Materials, "item-materials-circuit-board.png"),
        new("CircuitBreaker", "Circuit Breaker", InventoryCategory.Materials, "item-materials-circuit-breaker.png"),
        new("Cog", "Cog", InventoryCategory.Materials, "item-materials-cog.png"),
        new("Cordite", "Cordite", InventoryCategory.Materials, "item-materials-cordite.png"),
        new("DuctTape", "Duct Tape", InventoryCategory.Materials, "item-materials-duct-tape.png"),
        new("Fuse", "Fuse", InventoryCategory.Materials, "item-materials-fuse.png"),
        new("Glass", "Glass", InventoryCategory.Materials, "item-materials-glass.png"),
        new("Glue", "Glue", InventoryCategory.Materials, "item-materials-glue.png"),
        new("Hinge", "Hinge", InventoryCategory.Materials, "item-materials-hinge.png"),
        new("Leather", "Leather", InventoryCategory.Materials, "item-materials-leather.png"),
        new("lens", "Lens", InventoryCategory.Materials, "item-materials-lens.png"),
        new("Limestone", "Limestone", InventoryCategory.Materials, "limestone.png"),
        new("Lubricant", "Lubricant", InventoryCategory.Materials, "item-materials-lubricant.png"),
        new("Magnesium", "Magnesium", InventoryCategory.Materials, "item-materials-magnesium.png"),
        new("Metal", "Metal", InventoryCategory.Materials, "metal.png"),
        new("Motor", "Motor", InventoryCategory.Materials, "item-materials-motor.png"),
        new("Nails", "Nails", InventoryCategory.Materials, "nails.png"),
        new("nitroglycerin", "Nitroglycerin", InventoryCategory.Materials, "item-materials-nitroglycerin.png"),
        new("NutsAndBolts", "Nuts And Bolts", InventoryCategory.Materials, "item-materials-nuts-and-bolts.png"),
        new("Nylon", "Nylon", InventoryCategory.Materials, "nylon.png"),
        new("paintCan", "Paint Can", InventoryCategory.Materials, "item-materials-paint-can.png"),
        new("Piping", "Pipe", InventoryCategory.Materials, "piping.png"),
        new("Piston", "Piston", InventoryCategory.Materials, "item-materials-piston.png"),
        new("Plastic", "Plastic", InventoryCategory.Materials, "item-materials-plastic.png"),
        new("rawhoney", "Raw Honey", InventoryCategory.Materials, "item-materials-raw-honey.png"),
        new("Rock", "Rock", InventoryCategory.Materials, "rock.png"),
        new("Rope", "Rope", InventoryCategory.Materials, "item-materials-rope.png"),
        new("Rubber", "Rubber", InventoryCategory.Materials, "item-materials-rubber.png"),
        new("Sand", "Sand", InventoryCategory.Materials, "sand.png"),
        new("Sensor", "Sensor", InventoryCategory.Materials, "item-materials-sensor.png"),
        new("silicon", "Silicon", InventoryCategory.Materials, "item-materials-silicon.png"),
        new("Spring", "Spring", InventoryCategory.Materials, "item-materials-spring.png"),
        new("Switch", "Switch", InventoryCategory.Materials, "item-materials-switch.png"),
        new("Transistor", "Transistor", InventoryCategory.Materials, "item-materials-transistor.png"),
        new("Valve", "Valve", InventoryCategory.Materials, "item-materials-valve.png"),
        new("Wiring", "Wiring", InventoryCategory.Materials, "wiring.png"),
        new("Wood", "Wood", InventoryCategory.Materials, "wood.png"),
        new("Wool", "Wool", InventoryCategory.Materials, "wool.png"),
        new("zinc", "Zinc", InventoryCategory.Materials, "item-materials-zinc.png"),

        // Books
        new("bookcharismaone", "Oratory Book (Beginner)", InventoryCategory.Books, "item-books-oratory.png"),
        new("bookcharismatwo", "Oratory Book (Intermediate)", InventoryCategory.Books, "item-books-oratory.png"),
        new("bookcharismathree", "Oratory Book (Advanced)", InventoryCategory.Books, "item-books-oratory.png"),
        new("bookcharismafour", "Oratory Book (Expert)", InventoryCategory.Books, "item-books-oratory.png"),
        new("bookintelligenceone", "Logic Book (Beginner)", InventoryCategory.Books, "item-books-logic.png"),
        new("bookintelligencetwo", "Logic Book (Intermediate)", InventoryCategory.Books, "item-books-logic.png"),
        new("bookintelligencethree", "Logic Book (Advanced)", InventoryCategory.Books, "item-books-logic.png"),
        new("bookintelligencefour", "Logic Book (Expert)", InventoryCategory.Books, "item-books-logic.png"),
        new("bookperceptionone", "Sleuthing Book (Beginner)", InventoryCategory.Books, "item-books-sleuthing.png"),
        new("bookperceptiontwo", "Sleuthing Book (Intermediate)", InventoryCategory.Books, "item-books-sleuthing.png"),
        new("bookperceptionthree", "Sleuthing Book (Advanced)", InventoryCategory.Books, "item-books-sleuthing.png"),
        new("bookperceptionfour", "Sleuthing Book (Expert)", InventoryCategory.Books, "item-books-sleuthing.png"),
        new("bookstoryone", "Story Book (The Wind in the Bellows)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorytwo", "Story Book (Of Dice and Pen)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorythree", "Story Book (Toby Slick)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryfour", "Story Book (Fighting and Talking)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryfive", "Story Book (The Misadventures of Tackleberry Gin)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorysix", "Story Book (The Pitcher in the Sky)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryseven", "Story Book (Harold Trotter and the Philosophy Degree)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryeight", "Story Book (Criminals and Punishers)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorynine", "Story Book (Janice's Adventures in Rehab)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryten", "Story Book (Proud and Very Prejudiced)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryeleven", "Story Book (Crankenstein)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorytwelve", "Story Book (The Lord of the Springs)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorythirteen", "Story Book (Nineteen Hundred, Eighty and Four)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryfourteen", "Story Book (Charlotte's Web of Lies)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryfifteen", "Story Book (Sinker Sailor Bowler Tie)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorysixteen", "Story Book (Sword of the Flies)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryseventeen", "Story Book (Harley and the Motorcycle Factory)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstoryeighteen", "Story Book (Moderate Expectations)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorynineteen", "Story Book (A Clockwork Apple)", InventoryCategory.Books, "item-books-story.png"),
        new("bookstorytwenty", "Story Book (A Backpacker's Guide to the Universe)", InventoryCategory.Books, "item-books-story.png"),

        // Drugs
        new("Crunk", "Crunk", InventoryCategory.Drugs, "item-drugs-crunk.png"),
        new("Feederral", "Feederral", InventoryCategory.Drugs, "item-drugs-feederral.png"),
        new("Python", "Python", InventoryCategory.Drugs, "item-drugs-python.png"),
        new("Sigma", "Sigma", InventoryCategory.Drugs, "item-drugs-sigma.png"),
        new("Snodge", "Snodge", InventoryCategory.Drugs, "item-drugs-snodge.png"),
        new("Swill", "Swill", InventoryCategory.Drugs, "item-drugs-swill.png"),
        new("Trankwill", "Trankwill", InventoryCategory.Drugs, "item-drugs-trankwill.png"),

        // Medicines
        new("antirad", "Anti-Radiation Tablets", InventoryCategory.Medicines, "item-medicine-anti-radiation-tablets.png"),
        new("antibiotics", "Antibiotics", InventoryCategory.Medicines, "item-medicine-antibiotics.png"),
        new("antidepressant", "Antidepressant", InventoryCategory.Medicines, "item-medicine-antidepressant.png"),
        new("antiemetic", "Antiemetic", InventoryCategory.Medicines, "item-medicine-antiemetic.png"),
        new("antiVenom", "Antivenom", InventoryCategory.Medicines, "item-medicine-antivenom.png"),
        new("bandages", "Bandages", InventoryCategory.Medicines, "item-medicine-bandages.png"),
        new("firstAid", "First-Aid Kit", InventoryCategory.Medicines, "item-medicine-first-aid-kit.png"),
        new("homemadeAntiRadiationTablets", "Homemade Anti-Radiation Tablet", InventoryCategory.Medicines, "item-medicine-homemade-anti-radiation-tablet.png"),
        new("homemadeAntibiotics", "Homemade Antibiotic", InventoryCategory.Medicines, "item-medicine-homemade-antibiotic.png"),
        new("homemadeAntidespressant", "Homemade Antidepressant", InventoryCategory.Medicines, "item-medicine-homemade-antidepressant.png"),
        new("homemadeAntiemetics", "Homemade Antiemetic", InventoryCategory.Medicines, "item-medicine-homemade-antiemetic.png"),
        new("splint", "Splint", InventoryCategory.Medicines, "item-medicine-splint.png"),
        new("stimulant", "Stimulant", InventoryCategory.Medicines, "item-medicine-stimulant.png"),

        // Flora
        new("alfalfa", "Alfalfa", InventoryCategory.Flora, "item-flora-alfalfa.png"),
        new("aloevera", "Aloe Vera", InventoryCategory.Flora, "item-flora-aloe-vera.png"),
        new("echinacea", "Echinacea", InventoryCategory.Flora, "item-flora-echinacea.png"),
        new("garlic", "Garlic", InventoryCategory.Flora, "item-flora-garlic.png"),
        new("gingerroot", "Ginger Root", InventoryCategory.Flora, "item-flora-ginger-root.png"),
        new("holybasil", "Holy Basil", InventoryCategory.Flora, "item-flora-holy-basil.png"),
        new("oakbark", "Oak Bark", InventoryCategory.Flora, "item-flora-oak-bark.png"),
        new("passionflower", "Passion Flower", InventoryCategory.Flora, "passion-flower.png"),
        new("stjohnswort", "St. John's Wort", InventoryCategory.Flora, "item-flora-st-johns-wort.png"),
        new("yellowjasmine", "Yellow Jasmine", InventoryCategory.Flora, "yellow-jasmine.png"),

        // Consumables
        new("waterBottle", "Bottled Water", InventoryCategory.Consumables, "item-consumables-bottled-water.png"),
        new("Coal", "Coal", InventoryCategory.Consumables, "item-consumables-coal.png"),
        new("Fertilizer", "Fertilizer", InventoryCategory.Consumables, "item-consumables-fertilizer.png"),
        new("PetrolCan", "Petrol Can", InventoryCategory.Consumables, "petrol-can.png", minimumQuality: 2),
        new("Soap", "Soap", InventoryCategory.Consumables, "item-consumables-soap.png"),

        // Seeds
        new("broccoliSeed", "Broccoli Seed", InventoryCategory.Seeds, "broccoli-seed.png"),
        new("cabbageSeed", "Cabbage Seed", InventoryCategory.Seeds, "item-seed-cabbage-seed.png"),
        new("carrotSeed", "Carrot Seed", InventoryCategory.Seeds, "item-seed-carrot-seed.png"),
        new("mushroomSpore", "Mushroom Spore", InventoryCategory.Seeds, "item-seed-mushroom-spore.png"),
        new("onionSeed", "Onion Seed", InventoryCategory.Seeds, "item-seed-onion-seed.png"),
        new("peaSeed", "Pea Plant Seed", InventoryCategory.Seeds, "item-seed-pea-plant-seed.png"),
        new("plantSeed", "Plant Seed", InventoryCategory.Seeds, "item-seed-plant-seed.png"),
        new("potatoSeed", "Potato Seed", InventoryCategory.Seeds, "item-seed-potato-seed.png"),
        new("riceSeed", "Rice Seed", InventoryCategory.Seeds, "item-seed-rice-seed.png"),
        new("spinachSeed", "Spinach Seed", InventoryCategory.Seeds, "item-seed-spinach-seed.png"),
        new("tomatoSeed", "Tomato Seed", InventoryCategory.Seeds, "item-seed-tomato-seed.png"),

        // Vegetables
        new("Broccoli", "Broccoli", InventoryCategory.Vegetables, "item-crops-broccoli.png"),
        new("Cabbage", "Cabbage", InventoryCategory.Vegetables, "item-crops-cabbage.png"),
        new("Carrot", "Carrot", InventoryCategory.Vegetables, "item-crops-carrot.png"),
        new("mushroom", "Mushrooms", InventoryCategory.Vegetables, "item-crops-mushrooms.png"),
        new("Onion", "Onion", InventoryCategory.Vegetables, "item-crops-onion.png"),
        new("Peas", "Peas", InventoryCategory.Vegetables, "item-crops-peas.png"),
        new("Potato", "Potato", InventoryCategory.Vegetables, "item-crops-potato.png"),
        new("Rice", "Rice", InventoryCategory.Vegetables, "item-crops-rice.png"),
        new("Spinach", "Spinach", InventoryCategory.Vegetables, "item-crops-spinach.png"),
        new("Tomato", "Tomato", InventoryCategory.Vegetables, "item-crops-tomato.png"),

        // Equipment
        new("BulletproofVest", "Bulletproof vest", InventoryCategory.Equipment, "item-equipment-bulletproof-vest.png"),
        new("camouflage", "Camouflage", InventoryCategory.Equipment, "item-equipment-camouflage.png"),
        new("satchel", "Satchel", InventoryCategory.Equipment, "item-equipment-satchel.png"),
        new("stabVest", "Stab Proof Vest", InventoryCategory.Equipment, "item-equipment-stab-proof-vest.png"),
        new("Binoculars", "Binoculars", InventoryCategory.Equipment, "item-equipment-binoculars.png"),
        new("campingGear", "Camping Gear", InventoryCategory.Equipment, "item-equipment-camping-gear.png"),
        new("childsschoolbag", "Child's Schoolbag", InventoryCategory.Equipment, "item-equipment-childs-schoolbag.png"),
        new("bulletProofVestImproved", "Improved Bulletproof Vest", InventoryCategory.Equipment, "item-equipment-improved-bulletproof-vest.png"),
        new("stabVestImproved", "Improved Stab Proof Vest", InventoryCategory.Equipment, "item-equipment-improved-stab-proof-vest.png"),
        new("animalRepellent", "Animal Repellent", InventoryCategory.Equipment, "item-equipment-animal-repellent.png"),
        new("climbingTethers", "Climbing Tethers", InventoryCategory.Equipment, "item-equipment-climbing-tethers.png"),
        new("explorersBackpack", "Explorer's Backpack", InventoryCategory.Equipment, "item-equipment-explorers-backpack.png"),
        new("inflatableRaft", "Inflatable Raft", InventoryCategory.Equipment, "item-equipment-inflatable-raft.png"),
        new("metalDetector", "Metal Detector", InventoryCategory.Equipment, "item-equipment-metal-detector.png"),
        new("motionDetector", "Motion Detector", InventoryCategory.Equipment, "item-equipment-motion-detector.png"),
        new("militaryBackpackSmall", "Small Military Backpack", InventoryCategory.Equipment, "item-equipment-small-military-backpack.png"),
        new("militaryBackpackLarge", "Large Military Backpack", InventoryCategory.Equipment, "item-equipment-large-military-backpack.png"),
        new("bulletProofVestSuperior", "Superior Bulletproof Vest", InventoryCategory.Equipment, "item-equipment-superior-bulletproof-vest.png"),
        new("stabVestSuperior", "Superior Stab Proof Vest", InventoryCategory.Equipment, "item-equipment-superior-stab-proof-vest.png"),
        new("CatBell", "Cat Bell", InventoryCategory.Equipment, "item-equipment-cat-bell.png"),
        new("DogWhistle", "Dog Whistle", InventoryCategory.Equipment, "item-equipment-dog-whistle.png"),

        // VehicleParts
        new("BicycleFrame", "Bicycle Frame", InventoryCategory.VehicleParts, "item-vehicle-part-bicycle-frame.png"),
        new("Alternator", "Alternator", InventoryCategory.VehicleParts, "item-vehicle-part-alternator.png"),
        new("CarBattery", "Car Battery", InventoryCategory.VehicleParts, "item-vehicle-part-car-battery.png"),
        new("Distributor", "Distributor", InventoryCategory.VehicleParts, "item-vehicle-part-distributor.png"),
        new("SparkPlug", "Spark Plug", InventoryCategory.VehicleParts, "item-vehicle-part-spark-plug.png"),
        new("StarterMotor", "Starter Motor", InventoryCategory.VehicleParts, "item-vehicle-part-starter-motor.png"),
        new("Tyre", "Tyre", InventoryCategory.VehicleParts, "item-vehicle-part-tyre.png"),
        new("ClutchCable", "Clutch Cable", InventoryCategory.VehicleParts, "item-vehicle-part-clutch-cable.png"),
        new("FanBelt", "Fan Belt", InventoryCategory.VehicleParts, "item-vehicle-part-fan-belt.png"),
        new("MotorcycleChassis", "Motorcycle Chassi", InventoryCategory.VehicleParts, "item-vehicle-part-motorcycle-chassis.png"),

        // Weapons
        new("empGrenade", "EMP Grenade", InventoryCategory.Weapons, "item-weapons-emp-grenade.png"),
        new("BaseballBat", "Baseball Bat", InventoryCategory.Weapons, "item-weapons-baseball-bat.png"),
        new("bladedBaseballBat", "Bladed Baseball Bat", InventoryCategory.Weapons, "item-weapons-bladed-baseball-bat.png"),
        new("brutalMorningstar", "Brutal Morningstar", InventoryCategory.Weapons, "item-weapons-brutal-morningstar.png"),
        new("Knife", "Knife", InventoryCategory.Weapons, "item-weapons-knife.png"),
        new("cementedRebar", "Cemented Rebar", InventoryCategory.Weapons, "item-weapons-cemented-rebar.png"),
        new("Crossbow", "Crossbow", InventoryCategory.Weapons, "item-weapons-crossbow.png"),
        new("Crowbar", "Crowbar", InventoryCategory.Weapons, "item-weapons-crowbar.png"),
        new("electrifiedKnife", "Electrified Knife", InventoryCategory.Weapons, "item-weapons-electrified-knife.png"),
        new("extendedHatchet", "Extended Hatchet", InventoryCategory.Weapons, "item-weapons-extended-hatchet.png"),
        new("flashBang", "Flashbang", InventoryCategory.Weapons, "item-weapons-flashbang.png"),
        new("Grenade", "Grenade", InventoryCategory.Weapons, "item-weapons-grenade.png"),
        new("Hatchet", "Hatchet", InventoryCategory.Weapons, "item-weapons-hatchet.png"),
        new("KnuckleDuster", "Knuckle Duster", InventoryCategory.Weapons, "knuckle-duster.png"),
        new("megaHammer", "Mega-Hammer", InventoryCategory.Weapons, "item-weapons-mega-hammer.png"),
        new("Morningstar", "Morningstar", InventoryCategory.Weapons, "item-weapons-morningstar.png"),
        new("nailedWood", "Nailed Wood", InventoryCategory.Weapons, "item-weapons-nailed-wood.png"),
        new("pipeBomb", "Pipe Bomb", InventoryCategory.Weapons, "item-weapons-pipe-bomb.png"),
        new("Pistol", "Pistol", InventoryCategory.Weapons, "item-weapons-pistol.png"),
        new("pitchForkedPipe", "Pitch-forked Pipe", InventoryCategory.Weapons, "item-weapons-pitchforked-pipe.png"),
        new("Rebar", "Rebar", InventoryCategory.Weapons, "item-weapons-rebar.png"),
        new("rifle", "Rifle", InventoryCategory.Weapons, "item-weapons-rifle.png"),
        new("rockClub", "Rock Club", InventoryCategory.Weapons, "item-weapons-rock-club.png"),
        new("shotgun", "Shotgun", InventoryCategory.Weapons, "item-weapons-shotgun.png"),
        new("SledgeHammer", "Sledgehammer", InventoryCategory.Weapons, "item-weapons-sledgehammer.png"),
        new("spikedknuckleduster", "Spiked Knuckle Duster", InventoryCategory.Weapons, "item-weapons-spiked-knuckle-duster.png"),
        new("toxicMorningstar", "Toxic Morningstar", InventoryCategory.Weapons, "item-weapons-toxic-morningstar.png"),

        // Ammunition
        new("PistolAmmo", "Pistol Ammo", InventoryCategory.Ammunition, "item-ammunition-pistol-ammo.png"),
        new("RifleAmmo", "Rifle Ammo", InventoryCategory.Ammunition, "item-ammunition-rifle-ammo.png"),
        new("ShotgunAmmo", "Shotgun Ammo", InventoryCategory.Ammunition, "item-ammunition-shotgun-ammo.png"),

        // Pets
        new("fertilisedChickenEgg", "Fertilized Chicken Egg", InventoryCategory.Pets, "item-crops-chicken-egg.png"),
        ]);

    private static readonly FrozenDictionary<string, InventoryItemDefinition> Definitions =
        // The game compares defKeys without case; ordinal comparison avoids culture-dependent
        // casing and also makes differently cased duplicate catalog entries fail at startup.
        Items.ToFrozenDictionary(static item => item.DefinitionKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<InventoryItemDefinition> All => Items;

    public static InventoryItemDefinition? Find(string definitionKey)
    {
        ArgumentNullException.ThrowIfNull(definitionKey);
        return Definitions.GetValueOrDefault(definitionKey);
    }
}
