// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Projects persisted helper policies into controls without letting control initialization masquerade
/// as user intent. Helpers own behavior; this page only coordinates pickers, warnings, and feedback.
/// </summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>Drives the copy-to-checkmark swap on the clone command's copy button.</summary>
    private readonly CopyIconFeedback _copyFeedback = new();
    private readonly ILogger<SettingsPage> logger = App.LoggerFactory.CreateLogger<SettingsPage>();
    // Loaded can run again on this cached page. Keep the warning latched until the unsafe setting
    // changes, otherwise ordinary navigation can produce repeated modal interruptions.
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
        ResumeLastSaveToggleSwitch.IsOn = SaveSettings.RememberLastOpenedSave;
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
                    // Never persist an unsafe choice even briefly beyond this handler; reset first,
                    // then explain why the requested folder was refused.
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
            logger.LogError(ex, "Could not select a backup folder.");
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
            _ = Directory.CreateDirectory(BackupSettings.FolderPath);
            if (!await Launcher.LaunchFolderPathAsync(BackupSettings.FolderPath))
            {
                BackupFolderTextBlock.Text = "Windows could not open the backup folder.";
            }
        }
        catch (Exception ex)
        {
            BackupFolderTextBlock.Text = $"Could not open the backup folder: {ex.Message}";
            logger.LogError(ex, "Could not open the backup folder.");
        }
    }

    private async void OpenApplicationLogsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _ = Directory.CreateDirectory(ApplicationLogging.LogDirectoryPath);
            if (!await Launcher.LaunchFolderPathAsync(ApplicationLogging.LogDirectoryPath))
            {
                ApplicationLogsDescriptionTextBlock.Text = "Windows could not open the application logs folder.";
                logger.LogWarning("Windows declined the request to open the application logs folder.");
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            ApplicationLogsDescriptionTextBlock.Text = $"Could not open the application logs folder: {ex.Message}";
            logger.LogError(ex, "Could not open the application logs folder.");
        }
    }

    private void ResetBackupFolderButton_Click(object sender, RoutedEventArgs e)
    {
        BackupSettings.ResetFolder();
        RefreshBackupSettings();
    }

    private void BackupRetentionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackupRetentionComboBox.SelectedValue is string tag &&
            int.TryParse(tag, out int retentionCount))
        {
            BackupSettings.RetentionCount = retentionCount;
        }
    }

    private void SaveConfirmationToggleSwitch_Toggled(object sender, RoutedEventArgs e) =>
        SaveSettings.ConfirmBeforeSaving = SaveConfirmationToggleSwitch.IsOn;

    private void ResumeLastSaveToggleSwitch_Toggled(object sender, RoutedEventArgs e) =>
        SaveSettings.RememberLastOpenedSave = ResumeLastSaveToggleSwitch.IsOn;

    private void RefreshBackupSettings()
    {
        BackupFolderTextBlock.Text = BackupSettings.FolderPath;
        BackupRetentionComboBox.IsEnabled = !BackupSettings.IsGameSaveFolder;
        if (!BackupSettings.IsGameSaveFolder)
        {
            // Warn again if the user later switches back into the Steam Cloud folder.
            cloudFolderDialogShown = false;
        }

        BackupRetentionComboBox.SelectedValue =
            BackupSettings.RetentionCount.ToString(CultureInfo.InvariantCulture);
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Re-read mutable settings on cached-page entry because HomePage dialogs can change save
        // confirmation without visiting Settings.
        SaveConfirmationToggleSwitch.IsOn = SaveSettings.ConfirmBeforeSaving;
        ResumeLastSaveToggleSwitch.IsOn = SaveSettings.RememberLastOpenedSave;

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
            RequestedTheme = ActualTheme,
            Title = "Choose a folder outside Steam Cloud",
            Content =
                "Steam Cloud manages the Sheltered 2 save folder. Backups stored there can be synchronized as game saves and restored if deleted, potentially leading to an unlimited accumulation of saved files. The backup folder has now been reset to the default SaveOver location.",
            CloseButtonText = "Got it",
            DefaultButton = ContentDialogButton.Close,
        };
        _ = await dialog.ShowAsync();
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

        int buildMetadata = informational.IndexOf('+', StringComparison.Ordinal);
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
            logger.LogWarning(ex, "Could not copy the repository clone command.");
            return;
        }

        _copyFeedback.Play(button);
    }

    /// <summary>
    /// Ticks the item matching the theme in force. Done before the handler can run, so opening the
    /// page doesn't count as the user choosing a theme.
    /// </summary>
    private void SelectStoredTheme() => ThemeComboBox.SelectedValue = ThemeHelper.RootTheme.ToString();

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedValue is string tag
            && Enum.TryParse(tag, out ElementTheme theme))
        {
            ThemeHelper.RootTheme = theme;
        }
    }
}
