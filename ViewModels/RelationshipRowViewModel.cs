// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using CommunityToolkit.Mvvm.ComponentModel;
using SaveOver.Sheltered2.Models;
using System;

namespace SaveOver.Sheltered2.ViewModels;

/// <summary>
/// Pairs presentation-only member names with an ID-backed relationship. Retaining the original
/// model instance ensures sorting or filtering rows cannot redirect an edit to another member.
/// </summary>
public sealed partial class RelationshipRowViewModel(Relationship relationship, string name) : ObservableObject
{
    public const int MinLevel = -100;
    public const int MaxLevel = 100;

    public string Name { get; } = name;

    public string AutomationName { get; } = $"Relationship with {name}";

    // Normalize only on the editing surface: the model may contain an out-of-range raw value that
    // should survive unless the user chooses to replace it.
    public double Level
    {
        get => relationship.Level;
        set
        {
            int normalized = double.IsNaN(value) ? 0 : (int)Math.Clamp(value, MinLevel, MaxLevel);
            if (relationship.Level == normalized)
            {
                return;
            }

            relationship.Level = normalized;
            OnPropertyChanged();
        }
    }
}
