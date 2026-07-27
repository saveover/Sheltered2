// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SaveOver.Sheltered2.Models;
using SaveOver.Sheltered2.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Edits the shelter's stored water and existing inventory stacks. Stack creation, removal, and
/// reordering intentionally remain unsupported until the game's definition-key and size rules are
/// confirmed; this page only updates fields already present in the source XML.
/// </summary>
public sealed partial class InventoryPage : Page
{
    private readonly ObservableCollection<InventoryItemViewModel> visibleItems = [];
    private readonly Dictionary<InventoryItem, InventoryItemViewModel> itemViewModels = [];

    private ShelterInventory? boundInventory;
    private InventoryContainer? selectedContainer;
    private InventoryCategory? selectedCategory;
    private bool isPopulating;

    public InventoryPage()
    {
        InitializeComponent();
        ItemsRepeater.ItemsSource = visibleItems;
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        App.CurrentSaveData.SaveDataChanged += OnSaveDataChanged;
        RefreshInventory();
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.CurrentSaveData.SaveDataChanged -= OnSaveDataChanged;
        base.OnNavigatedFrom(e);
    }

    private void OnSaveDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshInventory);

    private void RefreshInventory()
    {
        ShelterInventory? inventory = App.CurrentSaveData.Inventory;

        isPopulating = true;
        try
        {
            if (!ReferenceEquals(boundInventory, inventory))
            {
                itemViewModels.Clear();
            }

            boundInventory = inventory;

            bool hasWater = inventory?.HasStoredWater == true;
            bool hasContainers = inventory?.Storage is not null || inventory?.Overflow is not null;
            NoInventoryInfoBar.IsOpen = !hasWater && !hasContainers;
            WaterCard.Visibility = hasWater ? Visibility.Visible : Visibility.Collapsed;
            InventoryCard.Visibility = hasContainers ? Visibility.Visible : Visibility.Collapsed;

            if (inventory is null)
            {
                selectedContainer = null;
                visibleItems.Clear();
                return;
            }

            StoredWaterNumberBox.IsEnabled = hasWater;
            if (hasWater)
            {
                StoredWaterNumberBox.Value = inventory.StoredWater;
            }

            StorageSelectorItem.IsEnabled = inventory.Storage is not null;
            OverflowSelectorItem.IsEnabled = inventory.Overflow is not null;

            if (!hasContainers)
            {
                selectedContainer = null;
                visibleItems.Clear();
                return;
            }

            SelectAvailableContainer(inventory);
            selectedCategory = CategorySelector.SelectedItem?.Tag is string tag
                ? ParseCategory(tag)
                : null;
            RefreshSelectedContainer();
        }
        finally
        {
            isPopulating = false;
        }
    }

    /// <summary>Retains the selected container when a cached page is revisited or refreshed.</summary>
    private void SelectAvailableContainer(ShelterInventory inventory)
    {
        string? selectedTag = ContainerSelector.SelectedItem?.Tag as string;

        if (selectedTag == "Overflow" && inventory.Overflow is not null)
        {
            OverflowSelectorItem.IsSelected = true;
            selectedContainer = inventory.Overflow;
        }
        else if (inventory.Storage is not null)
        {
            StorageSelectorItem.IsSelected = true;
            selectedContainer = inventory.Storage;
        }
        else
        {
            OverflowSelectorItem.IsSelected = true;
            selectedContainer = inventory.Overflow;
        }
    }

    private void ContainerSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (isPopulating || boundInventory is null || sender.SelectedItem?.Tag is not string tag)
        {
            return;
        }

        selectedContainer = tag switch
        {
            "Storage" => boundInventory.Storage,
            "Overflow" => boundInventory.Overflow,
            _ => null,
        };
        RefreshSelectedContainer();
    }

    private void CategorySelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (isPopulating || sender.SelectedItem?.Tag is not string tag)
        {
            return;
        }

        selectedCategory = ParseCategory(tag);
        RefreshVisibleItems();
    }

    private void RefreshSelectedContainer()
    {
        if (selectedContainer is null)
        {
            ContainerNameTextBlock.Text = "No storage selected";
            ContainerSummaryTextBlock.Text = string.Empty;
            RefreshVisibleItems();
            return;
        }

        ContainerNameTextBlock.Text = selectedContainer.Name;
        string stackLabel = selectedContainer.StackCount == 1 ? "stack" : "stacks";
        ContainerSummaryTextBlock.Text =
            $"{selectedContainer.StackCount} {stackLabel} · Maximum weight {selectedContainer.MaxWeight}";
        RefreshVisibleItems();
    }

    private void RefreshVisibleItems()
    {
        visibleItems.Clear();
        int unmappedCount = 0;

        if (selectedContainer is not null)
        {
            foreach (InventoryItem item in selectedContainer.Items)
            {
                if (item.Category is null)
                {
                    unmappedCount++;
                }

                if (selectedCategory is null || item.Category == selectedCategory)
                {
                    visibleItems.Add(GetViewModel(item));
                }
            }
        }

        NoItemsInfoBar.IsOpen = visibleItems.Count == 0;
        UnmappedItemsInfoBar.IsOpen = selectedCategory is null && unmappedCount > 0;
    }

    private InventoryItemViewModel GetViewModel(InventoryItem item)
    {
        if (!itemViewModels.TryGetValue(item, out InventoryItemViewModel? viewModel))
        {
            viewModel = new InventoryItemViewModel(item);
            itemViewModels.Add(item, viewModel);
        }

        return viewModel;
    }

    private void StoredWaterNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (isPopulating || boundInventory is not { HasStoredWater: true } || double.IsNaN(args.NewValue))
        {
            return;
        }

        int value = ToNonNegativeInteger(args.NewValue);
        if (value != boundInventory.StoredWater)
        {
            boundInventory.StoredWater = value;
        }

        SetNormalizedValue(sender, value);
    }

    private static InventoryCategory? ParseCategory(string tag) =>
        Enum.TryParse(tag, ignoreCase: false, out InventoryCategory category) ? category : null;

    private static int ToNonNegativeInteger(double value)
    {
        if (value <= 0 || double.IsNaN(value))
        {
            return 0;
        }

        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    private static void SetNormalizedValue(NumberBox numberBox, int value)
    {
        if (numberBox.Value != value)
        {
            numberBox.Value = value;
        }
    }
}
