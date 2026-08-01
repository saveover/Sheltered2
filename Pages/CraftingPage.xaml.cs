// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml.Controls;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Keeps the announced navigation destination stable while crafting support is still intentionally
/// absent from the save model; presenting an empty shell is safer than guessing recipe invariants.
/// </summary>
public sealed partial class CraftingPage : Page
{
    public CraftingPage() => InitializeComponent();
}
