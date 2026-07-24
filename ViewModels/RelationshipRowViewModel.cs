// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using SaveOver.Sheltered2.Models;
using System;

namespace SaveOver.Sheltered2.ViewModels;

/// <summary>
/// One relationship row: the other member's display name and the level, bound two-way to
/// a <c>NumberBox</c>. Wraps the model's <see cref="Relationship"/> directly so edits land
/// on the right entry.
/// </summary>
public sealed partial class RelationshipRowViewModel(Relationship relationship, string name) : ObservableModel
{
    public const int MinLevel = -100;
    public const int MaxLevel = 100;

    public string Name { get; } = name;

    public string AutomationName { get; } = $"Relationship with {name}";

    // Exposed as double so it binds straight to NumberBox.Value.
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
