// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SaveOver.Sheltered2.Models;
using System;

namespace SaveOver.Sheltered2.ViewModels;

/// <summary>
/// Presentation and edit bridge for one existing inventory stack. The game model remains the
/// source of truth; numeric setters normalise only a value the user has changed.
/// </summary>
public sealed partial class InventoryItemViewModel : ObservableObject
{
    // The game's 0-based enum (Poor, Good, Excellent) is shown as one to three stars.
    // Unexpected raw values remain unset in the UI and untouched until the user edits them.
    private const double UnsetRating = -1d;

    public InventoryItemViewModel(InventoryItem item, int automationIndex = 0)
    {
        Item = item;
        AutomationIndex = automationIndex;

        if (ItemCatalog.Find(item.DefinitionKey) is { } definition)
        {
            Icon = new BitmapImage(new Uri(definition.ImageAssetPath));
        }
    }

    /// <summary>The underlying stack, retained by document order for safe save write-back.</summary>
    public InventoryItem Item { get; }

    private int AutomationIndex { get; }

    /// <summary>Null deliberately selects a neutral icon so unknown defKeys remain non-misleading.</summary>
    public ImageSource? Icon { get; }

    public Visibility IconVisibility => Icon is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility FallbackIconVisibility => Icon is null ? Visibility.Visible : Visibility.Collapsed;

    public string DisplayName => Item.DisplayName;

    public string DefinitionKey => Item.DefinitionKey;

    public string CategoryLabel => Item.CategoryLabel;

    public string QualityLabel => IsQualityReadOnly
        ? "Excellent · fixed at 3 stars by the game"
        : Item.QualityLabel;

    public string AmountAutomationName => $"{DisplayName} amount";

    public string IntegrityAutomationName => $"{DisplayName} integrity";

    public string QualityAutomationName => IsQualityReadOnly
        ? $"{DisplayName} quality, fixed at three stars by the game"
        : $"{DisplayName} quality";

    public string DeleteAutomationName => $"Delete {DisplayName}";

    public string AmountAutomationId => $"InventoryItem{AutomationIndex}AmountNumberBox";

    public string IntegrityAutomationId => $"InventoryItem{AutomationIndex}IntegrityNumberBox";

    public string QualityAutomationId => $"InventoryItem{AutomationIndex}QualityRatingControl";

    public string DeleteAutomationId => $"InventoryItem{AutomationIndex}DeleteButton";

    public bool IsQualityReadOnly =>
        ItemCatalog.Find(DefinitionKey)?.MinimumQuality >= 2;

    public bool IsQualityEditable => !IsQualityReadOnly;

    /// <summary>
    /// Keeps NumberBox's double-valued binding at the presentation boundary so the save model
    /// never acquires fractional stack amounts.
    /// </summary>
    public double Amount
    {
        get => Item.Amount;
        set => SetAmount(value);
    }

    /// <summary>Normalizes an edited percentage without touching an unusual unedited source value.</summary>
    public double Integrity
    {
        get => Item.Integrity;
        set => SetIntegrity(value);
    }

    /// <summary>
    /// Offsets the game's zero-based enum for RatingControl and folds catalog minimums into the
    /// displayed value so Petrol Can can never appear to accept an invalid rating.
    /// </summary>
    public double QualityRatingValue
    {
        get
        {
            int minimumQuality = ItemCatalog.Find(DefinitionKey)?.MinimumQuality ?? 0;
            int effectiveQuality = Math.Max(Item.Quality, minimumQuality);
            return effectiveQuality is >= 0 and <= 2 ? effectiveQuality + 1 : UnsetRating;
        }
        set => SetQuality(value);
    }

    public void SetExcellentQuality()
    {
        // Route visible items through the view model so the two-way RatingControl receives the
        // notification; off-screen items can be changed directly by the page.
        if (Item.Quality != 2)
        {
            Item.Quality = 2;
            OnPropertyChanged(nameof(QualityRatingValue));
            OnPropertyChanged(nameof(QualityLabel));
        }
    }

    private void SetAmount(double requestedValue)
    {
        if (double.IsNaN(requestedValue))
        {
            return;
        }

        int value = Math.Max(0, int.CreateSaturating(requestedValue));
        bool changed = Item.Amount != value;
        if (changed)
        {
            Item.Amount = value;
        }

        // A NumberBox may send 1.5 while the stored value is already 1. Notify in that case so
        // the control snaps back to its integer representation instead of visually diverging.
        if (changed || requestedValue != value)
        {
            OnPropertyChanged(nameof(Amount));
        }
    }

    private void SetIntegrity(double requestedValue)
    {
        if (double.IsNaN(requestedValue))
        {
            return;
        }

        int value = Math.Clamp(int.CreateSaturating(requestedValue), 0, 100);
        bool changed = Item.Integrity != value;
        if (changed)
        {
            Item.Integrity = value;
        }

        if (changed || requestedValue != value)
        {
            OnPropertyChanged(nameof(Integrity));
        }
    }

    private void SetQuality(double requestedValue)
    {
        if (double.IsNaN(requestedValue))
        {
            return;
        }

        int minimumQuality = ItemCatalog.Find(DefinitionKey)?.MinimumQuality ?? 0;
        int value = requestedValue < 1
            ? 0
            : Math.Clamp(int.CreateSaturating(requestedValue) - 1, 0, 2);
        value = Math.Max(value, minimumQuality);
        bool changed = Item.Quality != value;
        if (changed)
        {
            Item.Quality = value;
        }

        // A canonical star click already left RatingControl.Value at value + 1. Avoid
        // reassigning Value from inside its own two-way update, which can displace focus.
        double normalizedRating = value + 1d;
        if (requestedValue != normalizedRating)
        {
            OnPropertyChanged(nameof(QualityRatingValue));
        }

        if (changed)
        {
            OnPropertyChanged(nameof(QualityLabel));
        }
    }
}
