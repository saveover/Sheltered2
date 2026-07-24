// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System.Collections.Generic;

namespace SaveOver.Sheltered2.ViewModels;

/// <summary>
/// Groups the skills of one tier for display as a titled section in the skill tree.
/// </summary>
public sealed class SkillTierViewModel(int tier, IReadOnlyList<SkillSlotViewModel> skills)
{
    /// <summary>Gets the tier number (1-3).</summary>
    public int Tier { get; } = tier;

    /// <summary>Gets the section title, e.g. "Tier 1".</summary>
    public string Title { get; } = $"Tier {tier}";

    /// <summary>Gets the skills belonging to this tier.</summary>
    public IReadOnlyList<SkillSlotViewModel> Skills { get; } = skills;
}
