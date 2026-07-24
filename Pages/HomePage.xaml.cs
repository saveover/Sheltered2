// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaveOver.Sheltered2.Helpers;
using System;
using System.IO;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// The home page is displayed when the application starts.
/// </summary>
public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();

        // Keep the Save button in step with the shared load state, so it stays enabled even
        // if this page is recreated after a file was already loaded.
        SaveFileButton.IsEnabled = App.CurrentSaveData.IsLoaded;
    }

    private async void LoadFileButton_Click(object sender, RoutedEventArgs e)
    {
        LoadFileButton.IsEnabled = false;
        LoadFileTextBlock.Text = "Selecting a file...";

        try
        {
            StorageFile? file = await FileHelper.PickFileAsync(CancellationToken.None);
            if (file is null)
            {
                LoadFileTextBlock.Text = "No file selected.";
                return;
            }

            LoadFileTextBlock.Text = $"Loading {file.Name}...";
            string decryptedContent = await FileHelper.LoadAndDecryptSaveFileAsync(file);
            ParsedSave parsed = SaveParser.Parse(decryptedContent);

            // Raises SaveDataChanged, which unlocks navigation and refreshes the editor pages.
            App.CurrentSaveData.Load(file, decryptedContent, parsed);

            LoadFileTextBlock.Text = $"File '{file.Name}' loaded successfully. You can now navigate to other pages to edit your save.";
            SaveFileButton.IsEnabled = true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            LoadFileTextBlock.Text = $"Error loading file: {ex.Message}";
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Unexpected error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"LoadFile error: {ex}");
        }
        finally
        {
            LoadFileButton.IsEnabled = true;
        }
    }

    private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSession saveData = App.CurrentSaveData;
        if (!saveData.IsLoaded || saveData.SourceFile is not StorageFile sourceFile)
        {
            LoadFileTextBlock.Text = "Load a save file before saving.";
            return;
        }

        SaveFileButton.IsEnabled = false;
        LoadFileButton.IsEnabled = false;
        LoadFileTextBlock.Text = "Saving...";

        try
        {
            // A timestamped backup is created first and the write itself is atomic.
            string updatedXml = SaveWriter.ApplyEdits(saveData.DecryptedContent, saveData.Characters, saveData.Pets);
            await FileHelper.EncryptAndSaveSaveFileAsync(sourceFile, updatedXml);

            LoadFileTextBlock.Text =
                $"Saved '{sourceFile.Name}'. A timestamped backup was created alongside it.";
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Error saving file: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"SaveFile error: {ex}");
        }
        finally
        {
            SaveFileButton.IsEnabled = true;
            LoadFileButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Copies the save file path to the clipboard.
    /// </summary>
    private void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Expand the environment variable to the actual path
            string expandedPath = Environment.ExpandEnvironmentVariables(@"%userprofile%\AppData\LocalLow\Unicube\Sheltered2");

            DataPackage dataPackage = new();
            dataPackage.SetText(expandedPath);
            Clipboard.SetContent(dataPackage);

            // Show checkmark feedback
            ShowCopySuccessFeedback();
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Failed to copy path: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Copy path error: {ex}");
        }
    }

    /// <summary>
    /// Shows visual feedback when the path is copied by cross-fading the copy icon to a
    /// checkmark and back. Both icons overlap and are driven by opacity (the checkmark
    /// starts at Opacity 0), so we run the storyboards rather than toggle Visibility -
    /// setting Visibility left the checkmark visible-but-transparent, i.e. invisible.
    /// </summary>
    private void ShowCopySuccessFeedback()
    {
        ShowCheckmarkStoryboard.Begin();
        ToolTipService.SetToolTip(CopyPathButton, "Path copied!");

        // Cross-fade back to the copy icon after 2 seconds.
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (s, args) =>
        {
            ShowCopyIconStoryboard.Begin();
            ToolTipService.SetToolTip(CopyPathButton, "Copy path to clipboard");
            timer.Stop();
        };
        timer.Start();
    }

    /// <summary>
    /// Handles the Support Development button click by navigating to the Donate page.
    /// </summary>
    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.StartupWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToPageByTag("Donate");
        }
    }
}