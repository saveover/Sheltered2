// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Reflection;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// App preferences. One setting for now - the light/dark theme - plus the version, which is the
/// first thing anyone filing a bug gets asked for.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        SelectStoredTheme();

        Assembly assembly = Assembly.GetExecutingAssembly();
        VersionTextBlock.Text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;
        CopyrightTextBlock.Text = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
    }

    /// <summary>
    /// Ticks the item matching the theme in force. Done before the handler can run, so opening the
    /// page doesn't count as the user choosing a theme.
    /// </summary>
    private void SelectStoredTheme()
    {
        string current = ThemeHelper.RootTheme.ToString();

        foreach (object item in ThemeComboBox.Items)
        {
            if (item is ComboBoxItem { Tag: string tag } && tag == current)
            {
                ThemeComboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem { Tag: string tag }
            && Enum.TryParse(tag, out ElementTheme theme))
        {
            ThemeHelper.RootTheme = theme;
        }
    }
}
