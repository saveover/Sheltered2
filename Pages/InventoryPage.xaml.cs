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
/// Separates catalog presentation from source-entry identity. Filtering and local artwork use the
/// catalog, while every edit remains attached to the parsed stack object that SaveWriter maps back
/// by document position.
/// </summary>
public sealed partial class InventoryPage : Page
{
    private readonly ObservableCollection<InventoryItemViewModel> visibleItems = [];
    private readonly ObservableCollection<InventoryItemDefinition> addItemOptions = [];
    private readonly Dictionary<InventoryItem, InventoryItemViewModel> itemViewModels = [];

    private ShelterInventory? boundInventory;
    private InventoryContainer? selectedContainer;
    private InventoryCategory? selectedCategory;
    // XAML selection events fire during InitializeComponent before later named controls exist.
    // The same guard also prevents programmatic refresh values from becoming user edits.
    private bool isPopulating = true;

    public InventoryPage()
    {
        InitializeComponent();
        ItemsRepeater.ItemsSource = visibleItems;
        AddItemListView.ItemsSource = addItemOptions;
        isPopulating = false;
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
        // SaveDataChanged can follow worker-thread parsing as well as UI edits, so all refreshes
        // cross the dispatcher boundary consistently.
        DispatcherQueue.TryEnqueue(RefreshInventory);

    private void RefreshInventory()
    {
        ShelterInventory? inventory = App.CurrentSaveData.Inventory;

        isPopulating = true;
        try
        {
            if (!ReferenceEquals(boundInventory, inventory))
            {
                // View models contain model identity and stable automation IDs. Reuse them for
                // filter/container changes, but never across a newly loaded save graph.
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
                AddItemButton.IsEnabled = false;
                IncreaseAllQualityButton.IsEnabled = false;
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
                AddItemButton.IsEnabled = false;
                IncreaseAllQualityButton.IsEnabled = false;
                visibleItems.Clear();
                return;
            }

            SelectAvailableContainer(inventory);
            selectedCategory = CategoryComboBox.SelectedItem is ComboBoxItem { Tag: string tag }
                ? ParseCategory(tag)
                : null;
            RefreshSelectedContainer();
        }
        finally
        {
            isPopulating = false;
        }
    }

    /// <summary>
    /// Prefers the cached selection when that container still exists, then falls back predictably;
    /// SaveDataChanged can replace a save with a different container shape.
    /// </summary>
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

    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isPopulating || CategoryComboBox.SelectedItem is not ComboBoxItem { Tag: string tag })
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
            AddItemButton.IsEnabled = false;
            IncreaseAllQualityButton.IsEnabled = HasAnyItems();
            RefreshVisibleItems();
            return;
        }

        ContainerNameTextBlock.Text = selectedContainer.Name;
        string stackLabel = selectedContainer.StackCount == 1 ? "stack" : "stacks";
        ContainerSummaryTextBlock.Text =
            $"{selectedContainer.StackCount} {stackLabel} · Maximum weight {selectedContainer.MaxWeight}";
        AddItemButton.IsEnabled = true;
        IncreaseAllQualityButton.IsEnabled = HasAnyItems();
        RefreshVisibleItems();
    }

    private bool HasAnyItems() =>
        (boundInventory?.Storage?.Items.Count ?? 0) +
        (boundInventory?.Overflow?.Items.Count ?? 0) > 0;

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
        // One wrapper per model prevents recycled ItemsRepeater controls from writing through a
        // newly created wrapper with a different automation identity.
        if (!itemViewModels.TryGetValue(item, out InventoryItemViewModel? viewModel))
        {
            viewModel = new InventoryItemViewModel(item, itemViewModels.Count);
            itemViewModels.Add(item, viewModel);
        }

        return viewModel;
    }

    private async void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedContainer is null)
        {
            return;
        }

        AddItemCategoryComboBox.SelectedIndex = 0;
        AddItemSearchBox.Text = string.Empty;
        AddItemAmountNumberBox.Value = 1;
        AddItemIntegrityNumberBox.Value = 100;
        // Excellent is the least surprising default for a save editor and also satisfies the
        // Petrol Can invariant without a special opening state.
        AddItemQualityRatingControl.Value = 3;
        AddItemQualityRatingControl.IsEnabled = true;
        AddItemQualityHintTextBlock.Text = string.Empty;
        AddItemDialog.IsPrimaryButtonEnabled = false;
        RefreshAddItemOptions();
        AddItemDialog.XamlRoot = XamlRoot;
        _ = await AddItemDialog.ShowAsync();
    }

    private void AddItemCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshAddItemOptions();

    private void AddItemSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            RefreshAddItemOptions();
        }
    }

    private void RefreshAddItemOptions()
    {
        if (AddItemListView is null || AddItemCategoryComboBox is null || AddItemSearchBox is null)
        {
            // AddItemCategoryComboBox raises SelectionChanged while InitializeComponent is still
            // constructing the dialog's later named elements.
            return;
        }

        InventoryCategory? category = AddItemCategoryComboBox.SelectedItem is ComboBoxItem { Tag: string tag }
            ? ParseCategory(tag)
            : null;
        string query = AddItemSearchBox.Text.Trim();

        addItemOptions.Clear();
        foreach (InventoryItemDefinition definition in ItemCatalog.All)
        {
            bool matchesCategory = category is null || definition.Category == category;
            bool matchesQuery = query.Length == 0
                || definition.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || definition.DefinitionKey.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (matchesCategory && matchesQuery)
            {
                addItemOptions.Add(definition);
            }
        }

        AddItemListView.SelectedItem = null;
        NoAddItemsTextBlock.Visibility = addItemOptions.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        AddItemDialog.IsPrimaryButtonEnabled = false;
    }

    private void AddItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AddItemListView.SelectedItem is not InventoryItemDefinition definition)
        {
            AddItemDialog.IsPrimaryButtonEnabled = false;
            return;
        }

        AddItemDialog.IsPrimaryButtonEnabled = true;
        AddItemQualityRatingControl.Value = 3;
        bool isFixedQuality = definition.MinimumQuality >= 2;
        AddItemQualityRatingControl.IsEnabled = !isFixedQuality;
        AddItemQualityHintTextBlock.Text = isFixedQuality
            ? "Petrol Can quality is fixed at 3 stars by the game."
            : string.Empty;
    }

    private void AddItemDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (selectedContainer is null ||
            AddItemListView.SelectedItem is not InventoryItemDefinition definition)
        {
            args.Cancel = true;
            return;
        }

        int amount = Math.Max(1, int.CreateSaturating(AddItemAmountNumberBox.Value));
        int integrity = Math.Clamp(int.CreateSaturating(AddItemIntegrityNumberBox.Value), 0, 100);
        int quality = Math.Clamp(int.CreateSaturating(AddItemQualityRatingControl.Value) - 1, 0, 2);
        quality = Math.Max(quality, definition.MinimumQuality);

        selectedContainer.Items.Add(new InventoryItem
        {
            // Only editable values belong in the model; SaveWriter owns the verified default XML
            // fields required to materialize a new stack.
            DefinitionKey = definition.DefinitionKey,
            Amount = amount,
            Integrity = integrity,
            Quality = quality,
        });

        RefreshSelectedContainer();
    }

    private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedContainer is null ||
            sender is not Button { CommandParameter: InventoryItemViewModel viewModel } ||
            !selectedContainer.Items.Contains(viewModel.Item))
        {
            return;
        }

        string unitLabel = viewModel.Item.Amount == 1 ? "unit" : "units";
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Title = "Delete item?",
            Content = $"Remove {viewModel.Item.Amount} {unitLabel} of {viewModel.DisplayName} from {selectedContainer.Name}?",
            PrimaryButtonText = "Delete item",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _ = selectedContainer.Items.Remove(viewModel.Item);
        RefreshSelectedContainer();
    }

    private void IncreaseAllQualityButton_Click(object sender, RoutedEventArgs e)
    {
        SetContainerItemsToExcellent(boundInventory?.Storage);
        SetContainerItemsToExcellent(boundInventory?.Overflow);
    }

    private void SetContainerItemsToExcellent(InventoryContainer? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (InventoryItem item in container.Items)
        {
            if (itemViewModels.TryGetValue(item, out InventoryItemViewModel? viewModel))
            {
                // Visible/cached wrappers must notify their RatingControls; stacks never realized
                // by the repeater have no presentation state to update.
                viewModel.SetExcellentQuality();
            }
            else if (item.Quality != 2)
            {
                item.Quality = 2;
            }
        }
    }

    private void StoredWaterNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (isPopulating || boundInventory is not { HasStoredWater: true } || double.IsNaN(args.NewValue))
        {
            return;
        }

        int value = Math.Max(0, int.CreateSaturating(args.NewValue));
        if (value != boundInventory.StoredWater)
        {
            boundInventory.StoredWater = value;
        }

        SetNormalizedValue(sender, value);
    }

    private static InventoryCategory? ParseCategory(string tag) =>
        Enum.TryParse(tag, ignoreCase: false, out InventoryCategory category) ? category : null;

    private static void SetNormalizedValue(NumberBox numberBox, int value)
    {
        // NumberBox can display a fractional intermediate even after the integer model accepted
        // the edit; write the canonical value back only when the control differs.
        if (numberBox.Value != value)
        {
            numberBox.Value = value;
        }
    }
}
