// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaveOver.Sheltered2.Models;

/// <summary>
/// Mirrors the catalog taxonomy rather than inferring groups from save keys, which keeps filtering
/// deterministic when the game introduces similarly named items.
/// </summary>
public enum InventoryCategory
{
    Ammunition,
    Books,
    Consumables,
    Drugs,
    Equipment,
    Flora,
    Junk,
    Materials,
    Medicines,
    Pets,
    Seeds,
    SpecialItems,
    Tools,
    Valuables,
    Vegetables,
    VehicleParts,
    Weapons,
}

public static class InventoryCategoryExtensions
{
    public static string DisplayName(this InventoryCategory category) => category switch
    {
        InventoryCategory.SpecialItems => "Special Items",
        InventoryCategory.VehicleParts => "Vehicle Parts",
        _ => category.ToString(),
    };
}

/// <summary>
/// One item stack in an inventory container. Its position in the containing collection is its
/// identity for write-back: save-file definition keys and ids are not unique stack identifiers.
/// </summary>
public sealed partial class InventoryItem : ObservableObject
{
    /// <summary>
    /// Zero-based position in the XML collection when this save baseline was parsed. New items
    /// have no source index until the save succeeds and the session establishes a new baseline.
    /// </summary>
    internal int? SourceIndex { get; set; }

    /// <summary>
    /// Preserves the source <c>defKey</c> spelling for round trips even though catalog resolution
    /// follows the game's case-insensitive comparison.
    /// </summary>
    public string DefinitionKey { get; init; } = string.Empty;

    /// <summary>Kept raw in the model; the editing surface normalizes only a user-entered value.</summary>
    [ObservableProperty]
    public partial int Amount { get; set; }

    /// <inheritdoc cref="Amount"/>
    [ObservableProperty]
    public partial int Integrity { get; set; }

    /// <inheritdoc cref="Amount"/>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityLabel))]
    public partial int Quality { get; set; }

    /// <summary>Falls back to the raw key so unknown future-game items remain visible and editable.</summary>
    public string DisplayName => ItemCatalog.Find(DefinitionKey)?.DisplayName ?? DefinitionKey;

    /// <summary>Null keeps unknown definitions in the unfiltered view instead of misclassifying them.</summary>
    public InventoryCategory? Category => ItemCatalog.Find(DefinitionKey)?.Category;

    /// <summary>Makes the unknown state explicit rather than presenting an empty category.</summary>
    public string CategoryLabel => Category?.DisplayName() ?? "Unmapped";

    /// <summary>Retains a readable fallback for out-of-range values instead of hiding save anomalies.</summary>
    public string QualityLabel => Quality switch
    {
        0 => "Poor · 1 of 3 stars",
        1 => "Good · 2 of 3 stars",
        2 => "Excellent · 3 of 3 stars",
        _ => $"Quality {Quality}",
    };
}

/// <summary>
/// One saved inventory container, such as the shelter storage or the overflow item bin.
/// Items retain the document order used for safe write-back.
/// </summary>
public sealed class InventoryContainer(
    string name,
    int maxWeight,
    IEnumerable<InventoryItem> items)
{
    public string Name { get; } = name;

    public int MaxWeight { get; } = maxWeight;

    public ObservableCollection<InventoryItem> Items { get; } = [.. items];

    public int StackCount => Items.Count;

    /// <summary>
    /// Advances identity only after a successful save so subsequent structural edits clone the
    /// correct new baseline rather than the pre-save XML positions.
    /// </summary>
    internal void RebaselineSourceIndices()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            Items[index].SourceIndex = index;
        }
    }
}

/// <summary>
/// Keeps storage, overflow, and water under one optional session branch so saves missing the
/// shelter inventory schema can disable the whole editing surface without fabricating nodes.
/// </summary>
public sealed partial class ShelterInventory : ObservableObject
{
    /// <summary>Prevents the writer from inventing <c>StoredWater</c> for schemas that omit it.</summary>
    public bool HasStoredWater { get; init; }

    /// <summary>Observable so the shared session becomes dirty without page-specific bookkeeping.</summary>
    [ObservableProperty]
    public partial int StoredWater { get; set; }

    public InventoryContainer? Storage { get; init; }

    public InventoryContainer? Overflow { get; init; }
}
