// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Reflection;
using Windows.ApplicationModel.DataTransfer;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// App preferences. One setting for now - the light/dark theme - plus the version, which is the
/// first thing anyone filing a bug gets asked for.
/// </summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>Drives the copy-to-checkmark swap on the clone command's copy button.</summary>
    private readonly CopyIconFeedback _copyFeedback = new();

    public SettingsPage()
    {
        InitializeComponent();

        SelectStoredTheme();

        Assembly assembly = Assembly.GetExecutingAssembly();
        VersionTextBlock.Text = ReadableVersion(assembly);
        CopyrightTextBlock.Text = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
    }

    /// <summary>
    /// The version as a person would quote it. The informational version is preferred because it
    /// carries the release number rather than the assembly's four-part one, but a source-linked
    /// build appends "+" and the full commit hash, which is not something to put in a header.
    /// </summary>
    private static string ReadableVersion(Assembly assembly)
    {
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (informational is null)
        {
            return assembly.GetName().Version?.ToString() ?? string.Empty;
        }

        int buildMetadata = informational.IndexOf('+');
        return buildMetadata < 0 ? informational : informational[..buildMetadata];
    }

    /// <summary>
    /// Copies the clone command, reading it from the markup so the two can't drift apart.
    /// </summary>
    private void CopyCloneCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        try
        {
            DataPackage package = new();
            package.SetText(CloneCommandTextBlock.Text);
            Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Copy clone command error: {ex}");
            return;
        }

        _copyFeedback.Play(button);
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
