// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SaveOver.Sheltered2.Models;
using System;

namespace SaveOver.Sheltered2.ViewModels;

/// <summary>
/// One skill in the tree: icon, name, maximum rank, and the invested points bound two-way
/// to a <c>RatingControl</c>. Edits are pushed back through <paramref name="onLevelChanged"/>.
/// </summary>
public sealed partial class SkillSlotViewModel(SkillDefinition definition, int level, Action<SkillDefinition, int>? onLevelChanged = null) : ObservableModel
{
    // RatingControl renders a Value of 0 as one filled star (microsoft-ui-xaml#10348), so
    // 0 points is fed to the control as -1 (unset) and mapped back on the way in.
    private const double UnsetRating = -1d;

    private readonly Action<SkillDefinition, int>? onLevelChanged = onLevelChanged;

    public SkillDefinition Definition { get; } = definition;

    public ImageSource Icon { get; } = new BitmapImage(definition.ImageUri);

    public string Name => Definition.Name;

    public int MaxLevel => Definition.MaxLevel;

    public int Level { get; private set; } = Math.Clamp(level, 0, definition.MaxLevel);

    public double RatingValue
    {
        get => Level == 0 ? UnsetRating : Level;
        set
        {
            int normalized = value < 0 ? 0 : (int)Math.Clamp(value, 0, Definition.MaxLevel);
            if (Level == normalized)
            {
                return;
            }

            Level = normalized;
            OnPropertyChanged();
            onLevelChanged?.Invoke(Definition, Level);
        }
    }
}
