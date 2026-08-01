// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System.Collections.Generic;

namespace SaveOver.Sheltered2.ViewModels;

/// <summary>
/// Gives the nested XAML template a stable section object instead of teaching the view how to group
/// and order catalog entries itself.
/// </summary>
public sealed class SkillTierViewModel(int tier, IReadOnlyList<SkillSlotViewModel> skills)
{
    public int Tier { get; } = tier;

    public string Title { get; } = $"Tier {tier}";

    public IReadOnlyList<SkillSlotViewModel> Skills { get; } = skills;
}
