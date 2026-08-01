// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace SaveOver.Sheltered2.Models;

/// <summary>The six gameplay categories shown by the inventory editor.</summary>
public enum InventoryCategory
{
    Weapons,
    Equipment,
    Medical,
    General,
    Junk,
    Food,
}

/// <summary>
/// One item stack in an inventory container. Its position in the containing collection is its
/// identity for write-back: save-file definition keys and ids are not unique stack identifiers.
/// </summary>
public sealed partial class InventoryItem : ObservableObject
{
    /// <summary>The raw, case-sensitive <c>defKey</c> stored by the game.</summary>
    public string DefinitionKey { get; init; } = string.Empty;

    /// <summary>Number of units in this stack.</summary>
    [ObservableProperty]
    public partial int Amount { get; set; }

    /// <summary>The raw integrity value stored for this stack.</summary>
    [ObservableProperty]
    public partial int Integrity { get; set; }

    /// <summary>The raw quality value stored for this stack.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityStars))]
    [NotifyPropertyChangedFor(nameof(QualityLabel))]
    public partial int Quality { get; set; }

    /// <summary>A friendly catalog name, or the raw definition key when it is not catalogued yet.</summary>
    public string DisplayName => ItemCatalog.Find(DefinitionKey)?.DisplayName ?? DefinitionKey;

    /// <summary>The known gameplay category, if this definition key has been catalogued.</summary>
    public InventoryCategory? Category => ItemCatalog.Find(DefinitionKey)?.Category;

    /// <summary>Text used by the item-card category badge.</summary>
    public string CategoryLabel => Category?.ToString() ?? "Unmapped";

    /// <summary>One to three stars for recognised quality values; raw values remain visible beside it.</summary>
    public string QualityStars => new('★', Math.Clamp(Quality, 0, 3));

    /// <summary>Accessible wording for the quality indicator without assuming a name for quality level two.</summary>
    public string QualityLabel => Quality is >= 1 and <= 3
        ? $"{Quality} of 3 stars"
        : $"Quality {Quality}";
}

/// <summary>
/// One saved inventory container, such as the shelter storage or the overflow item bin.
/// Items retain the document order used for safe write-back.
/// </summary>
public sealed class InventoryContainer(string name, int maxWeight, IReadOnlyList<InventoryItem> items)
{
    public string Name { get; } = name;

    public int MaxWeight { get; } = maxWeight;

    public IReadOnlyList<InventoryItem> Items { get; } = items;

    public int StackCount => Items.Count;
}

/// <summary>The inventory state owned by the shelter rather than any individual survivor.</summary>
public sealed partial class ShelterInventory : ObservableObject
{
    /// <summary>Whether the source save contains a writable root-level <c>StoredWater</c> element.</summary>
    public bool HasStoredWater { get; init; }

    /// <summary>The raw <c>StoredWater</c> value at the save root.</summary>
    [ObservableProperty]
    public partial int StoredWater { get; set; }

    public InventoryContainer? Storage { get; init; }

    public InventoryContainer? Overflow { get; init; }
}
