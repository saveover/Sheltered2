// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml.Controls;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Keeps the announced navigation destination stable while faction support is still intentionally
/// absent from the save model; no XML is exposed until faction identity rules are verified.
/// </summary>
public sealed partial class FactionsPage : Page
{
    public FactionsPage() => InitializeComponent();
}
