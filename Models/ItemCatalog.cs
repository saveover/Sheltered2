// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.Collections.Generic;

namespace SaveOver.Sheltered2.Models;

/// <summary>Trusted display metadata for one known, case-sensitive game definition key.</summary>
public sealed class InventoryItemDefinition(
    string definitionKey,
    string displayName,
    InventoryCategory category,
    string imageAssetFileName)
{
    public string DefinitionKey { get; } = definitionKey;

    public string DisplayName { get; } = displayName;

    public InventoryCategory Category { get; } = category;

    /// <summary>A packaged, fixed asset path rather than a URL derived from save-file data.</summary>
    public Uri ImageUri { get; } = new($"ms-appx:///Assets/Inventory/{imageAssetFileName}");
}

/// <summary>
/// Curated metadata for definition keys that have been verified against the supplied save and
/// the Sheltered 2 Wiki. This deliberately does not derive names, categories, or asset paths
/// from save-file input; unmapped keys remain visible with their raw <c>defKey</c>.
/// </summary>
public static class ItemCatalog
{
    private static readonly IReadOnlyDictionary<string, InventoryItemDefinition> Definitions =
        new Dictionary<string, InventoryItemDefinition>(StringComparer.Ordinal)
        {
            ["PetrolCan"] = new("PetrolCan", "Petrol Can", InventoryCategory.General, "petrol-can.png"),
            ["broccoliSeed"] = new("broccoliSeed", "Broccoli Seed", InventoryCategory.General, "broccoli-seed.png"),
            ["Bucket"] = new("Bucket", "Bucket", InventoryCategory.General, "bucket.png"),
            ["Bulb"] = new("Bulb", "Bulb", InventoryCategory.General, "bulb.png"),
            ["Limestone"] = new("Limestone", "Limestone", InventoryCategory.General, "limestone.png"),
            ["Metal"] = new("Metal", "Metal", InventoryCategory.General, "metal.png"),
            ["Nails"] = new("Nails", "Nails", InventoryCategory.General, "nails.png"),
            ["Nylon"] = new("Nylon", "Nylon", InventoryCategory.General, "nylon.png"),
            ["passionflower"] = new("passionflower", "Passion Flower", InventoryCategory.General, "passion-flower.png"),
            ["Piping"] = new("Piping", "Piping", InventoryCategory.General, "piping.png"),
            ["Rock"] = new("Rock", "Rock", InventoryCategory.General, "rock.png"),
            ["Sand"] = new("Sand", "Sand", InventoryCategory.General, "sand.png"),
            ["Wiring"] = new("Wiring", "Wiring", InventoryCategory.General, "wiring.png"),
            ["Wood"] = new("Wood", "Wood", InventoryCategory.General, "wood.png"),
            ["Wool"] = new("Wool", "Wool", InventoryCategory.General, "wool.png"),
            ["yellowjasmine"] = new("yellowjasmine", "Yellow Jasmine", InventoryCategory.General, "yellow-jasmine.png"),
            ["KnuckleDuster"] = new("KnuckleDuster", "Knuckle Duster", InventoryCategory.Weapons, "knuckle-duster.png"),
            ["animalFat"] = new("animalFat", "Animal Fat", InventoryCategory.General, "animal-fat.png"),
        };

    /// <summary>Gets metadata for a known key, or <see langword="null"/> for an unmapped item.</summary>
    public static InventoryItemDefinition? Find(string definitionKey) =>
        Definitions.GetValueOrDefault(definitionKey);
}
