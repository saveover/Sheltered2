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
    public InventoryItemViewModel(InventoryItem item)
    {
        Item = item;

        if (ItemCatalog.Find(item.DefinitionKey) is { } definition)
        {
            Icon = new BitmapImage(definition.ImageUri);
        }
    }

    /// <summary>The underlying stack, retained by document order for safe save write-back.</summary>
    public InventoryItem Item { get; }

    /// <summary>Locally packaged artwork for a catalogued item, if available.</summary>
    public ImageSource? Icon { get; }

    public Visibility IconVisibility => Icon is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility FallbackIconVisibility => Icon is null ? Visibility.Visible : Visibility.Collapsed;

    public string DisplayName => Item.DisplayName;

    public string DefinitionKey => Item.DefinitionKey;

    public string CategoryLabel => Item.CategoryLabel;

    public string QualityStars => Item.QualityStars;

    public string QualityLabel => Item.QualityLabel;

    public string AmountAutomationName => $"{DisplayName} amount";

    public string IntegrityAutomationName => $"{DisplayName} integrity";

    public string QualityAutomationName => $"{DisplayName} quality, one to three stars";

    /// <summary>
    /// A <see cref="double"/> surface for <see cref="Microsoft.UI.Xaml.Controls.NumberBox"/>,
    /// while retaining the game's integer representation.
    /// </summary>
    public double Amount
    {
        get => Item.Amount;
        set => SetAmount(value);
    }

    /// <summary>A non-negative integer integrity value exposed to a <c>NumberBox</c>.</summary>
    public double Integrity
    {
        get => Item.Integrity;
        set => SetIntegrity(value);
    }

    /// <summary>A one-to-three-star quality value exposed to a <c>NumberBox</c>.</summary>
    public double Quality
    {
        get => Item.Quality;
        set => SetQuality(value);
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

        int value = Math.Max(0, int.CreateSaturating(requestedValue));
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

        int value = Math.Clamp(int.CreateSaturating(requestedValue), 1, 3);
        bool changed = Item.Quality != value;
        if (changed)
        {
            Item.Quality = value;
        }

        if (changed || requestedValue != value)
        {
            OnPropertyChanged(nameof(Quality));
            OnPropertyChanged(nameof(QualityStars));
            OnPropertyChanged(nameof(QualityLabel));
        }
    }
}
