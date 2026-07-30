// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Reflection;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// App preferences. One setting for now - the light/dark theme - plus the version, which is the
/// first thing anyone filing a bug gets asked for.
/// </summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>Drives the copy-to-checkmark swap on the clone command's copy button.</summary>
    private readonly CopyIconFeedback _copyFeedback = new();
    private bool cloudFolderDialogShown;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;

        SelectStoredTheme();

        // Straight to the fields, so seeding them doesn't read as the user changing anything.
        NavigationStyleComboBox.SelectedIndex = NavigationStyleHelper.IsTopStyle ? 1 : 0;
        SoundToggleSwitch.IsOn = SoundHelper.IsSoundEnabled;
        SpatialAudioToggleSwitch.IsOn = SoundHelper.IsSpatialAudioEnabled;
        SpatialAudioCard.IsEnabled = SoundHelper.IsSoundEnabled;
        SaveConfirmationToggleSwitch.IsOn = SaveSettings.ConfirmBeforeSaving;
        RefreshBackupSettings();

        Assembly assembly = Assembly.GetExecutingAssembly();
        VersionTextBlock.Text = ReadableVersion(assembly);
        AboutExpander.Description = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
    }

    private async void ReportIssueCard_Click(object sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/saveover/Sheltered2/issues"));

    private void NavigationStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        NavigationStyleHelper.IsTopStyle = NavigationStyleComboBox.SelectedIndex == 1;

    private void SoundToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        SoundHelper.IsSoundEnabled = SoundToggleSwitch.IsOn;

        // Turning sound off takes spatial audio with it, so the row has to follow.
        SpatialAudioCard.IsEnabled = SoundToggleSwitch.IsOn;
        SpatialAudioToggleSwitch.IsOn = SoundHelper.IsSpatialAudioEnabled;
    }

    private void SpatialAudioToggleSwitch_Toggled(object sender, RoutedEventArgs e) =>
        SoundHelper.IsSpatialAudioEnabled = SpatialAudioToggleSwitch.IsOn;

    private async void ChooseBackupFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.IsEnabled = false;

        try
        {
            string? folderPath = await FileHelper.PickFolderAsync();
            if (folderPath is not null)
            {
                BackupSettings.FolderPath = folderPath;
                bool isSteamCloudFolder = BackupSettings.IsGameSaveFolder;
                if (isSteamCloudFolder)
                {
                    BackupSettings.ResetFolder();
                }

                RefreshBackupSettings();
                if (isSteamCloudFolder)
                {
                    await ShowCloudFolderDialogAsync();
                }
            }
        }
        catch (Exception ex)
        {
            BackupFolderTextBlock.Text = $"Could not select a backup folder: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Backup folder picker error: {ex}");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void OpenBackupFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(BackupSettings.FolderPath);
            if (!await Launcher.LaunchFolderPathAsync(BackupSettings.FolderPath))
            {
                BackupFolderTextBlock.Text = "Windows could not open the backup folder.";
            }
        }
        catch (Exception ex)
        {
            BackupFolderTextBlock.Text = $"Could not open the backup folder: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Open backup folder error: {ex}");
        }
    }

    private void ResetBackupFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BackupSettings.ResetFolder();
        RefreshBackupSettings();
    }

    private void BackupRetentionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackupRetentionComboBox.SelectedItem is ComboBoxItem { Tag: string tag } &&
            int.TryParse(tag, out int retentionCount))
        {
            BackupSettings.RetentionCount = retentionCount;
        }
    }

    private void SaveConfirmationToggleSwitch_Toggled(object sender, RoutedEventArgs e) =>
        SaveSettings.ConfirmBeforeSaving = SaveConfirmationToggleSwitch.IsOn;

    private void RefreshBackupSettings()
    {
        BackupFolderTextBlock.Text = BackupSettings.FolderPath;
        BackupRetentionComboBox.IsEnabled = !BackupSettings.IsGameSaveFolder;
        if (!BackupSettings.IsGameSaveFolder)
        {
            // Warn again if the user later switches back into the Steam Cloud folder.
            cloudFolderDialogShown = false;
        }

        string retention = BackupSettings.RetentionCount.ToString();
        foreach (object item in BackupRetentionComboBox.Items)
        {
            if (item is ComboBoxItem { Tag: string tag } && tag == retention)
            {
                BackupRetentionComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        SaveConfirmationToggleSwitch.IsOn = SaveSettings.ConfirmBeforeSaving;

        if (BackupSettings.IsGameSaveFolder)
        {
            BackupSettings.ResetFolder();
            RefreshBackupSettings();
            await ShowCloudFolderDialogAsync();
        }
    }

    private async System.Threading.Tasks.Task ShowCloudFolderDialogAsync()
    {
        if (cloudFolderDialogShown || XamlRoot is null)
        {
            return;
        }

        cloudFolderDialogShown = true;
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Choose a folder outside Steam Cloud",
            Content =
                "Steam Cloud manages the Sheltered 2 save folder. Backups stored there can be synchronized as game saves and restored if deleted, potentially leading to an unlimited accumulation of saved files. The backup folder has now been reset to the default SaveOver location.",
            CloseButtonText = "Got it",
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
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
