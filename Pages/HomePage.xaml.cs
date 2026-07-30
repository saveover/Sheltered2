// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// One tile in the home page's "What You Can Edit" grid.
/// </summary>
/// <param name="Name">Editor name, matching the navigation item it points at.</param>
/// <param name="Summary">One line on what that editor covers.</param>
/// <param name="Icon">Glyph shown beside the name. An <see cref="IconSource"/> rather than an
/// <c>IconElement</c> because a template instantiates it once per item, and an element can only
/// live at one place in the tree.</param>
public sealed record EditorFeature(string Name, string Summary, IconSource Icon);

/// <summary>
/// The home page is displayed when the application starts.
/// </summary>
public sealed partial class HomePage : Page
{
    /// <summary>Where Sheltered 2 keeps its saves, as shown on the page.</summary>
    private const string SaveFolder = @"%userprofile%\AppData\LocalLow\Unicube\Sheltered2";

    /// <summary>Tiles bound to the "What You Can Edit" grid, in navigation order.</summary>
    public IReadOnlyList<EditorFeature> Features { get; } =
    [
        new("Characters", "Stats, skills, traits", new SymbolIconSource { Symbol = Symbol.People }),
        new("Pets", "Health, happiness, stats", Glyph("")),
        new("Inventory", "Items, quantities", Glyph("")),
        new("Crafting", "Materials, recipes", new SymbolIconSource { Symbol = Symbol.Repair }),
        new("Factions", "Relationships, reputation", new SymbolIconSource { Symbol = Symbol.Flag }),
    ];

    /// <summary>A Segoe Fluent glyph, sized to match what the SymbolIconSources beside it render at.</summary>
    private static FontIconSource Glyph(string glyph) => new() { Glyph = glyph, FontSize = 20 };

    /// <summary>Drives the copy-to-checkmark swap on the copy button.</summary>
    private readonly CopyIconFeedback _copyFeedback = new();

    /// <summary>The copy button's resting tooltip, taken from the markup so the wording lives in
    /// one place even though the copy swaps it out for a moment.</summary>
    private readonly object _copyTooltip;

    public HomePage()
    {
        InitializeComponent();

        _copyTooltip = ToolTipService.GetToolTip(CopyPathButton);

        // Keep the Save button in step with the shared load state, so it stays enabled even
        // if this page is recreated after a file was already loaded.
        App.CurrentSaveData.DirtyStateChanged += CurrentSaveData_DirtyStateChanged;
        UpdateSaveButtonState();
    }

    private async void LoadFileButton_Click(object sender, RoutedEventArgs e)
    {
        LoadFileButton.IsEnabled = false;
        SaveFileButton.IsEnabled = false;
        LoadFileTextBlock.Text = "Selecting a file...";

        try
        {
            string? filePath = await FileHelper.PickFileAsync(CancellationToken.None);
            if (filePath is null)
            {
                LoadFileTextBlock.Text = "No file selected.";
                return;
            }

            string fileName = Path.GetFileName(filePath);
            LoadFileTextBlock.Text = $"Loading {fileName}...";
            string decryptedContent = await FileHelper.LoadAndDecryptSaveFileAsync(filePath);
            ParsedSave parsed = SaveParser.Parse(decryptedContent);

            // Raises SaveDataChanged, which unlocks navigation and refreshes the editor pages.
            App.CurrentSaveData.Load(filePath, decryptedContent, parsed);

            LoadFileTextBlock.Text = $"File '{fileName}' loaded successfully. You can now navigate to other pages to edit your save.";
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
            UpdateSaveButtonState();
        }
    }

    private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSession saveData = App.CurrentSaveData;
        if (!saveData.IsLoaded || saveData.SourceFilePath is not string sourceFilePath)
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
            string updatedXml = SaveWriter.ApplyEdits(
                saveData.DecryptedContent,
                saveData.Characters,
                saveData.Pets,
                saveData.Inventory);

            if (string.Equals(updatedXml, saveData.DecryptedContent, StringComparison.Ordinal))
            {
                saveData.CommitSavedContent(updatedXml);
                LoadFileTextBlock.Text = "There are no changes to save.";
                return;
            }

            if (SaveSettings.ConfirmBeforeSaving &&
                !await ConfirmSaveAsync(sourceFilePath))
            {
                LoadFileTextBlock.Text = "Save cancelled.";
                return;
            }

            await FileHelper.EncryptAndSaveSaveFileAsync(sourceFilePath, updatedXml);
            saveData.CommitSavedContent(updatedXml);

            LoadFileTextBlock.Text =
                $"Saved '{Path.GetFileName(sourceFilePath)}'. A timestamped backup was created in '{BackupSettings.FolderPath}'.";
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Error saving file: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"SaveFile error: {ex}");
        }
        finally
        {
            UpdateSaveButtonState();
            LoadFileButton.IsEnabled = true;
        }
    }

    private void CurrentSaveData_DirtyStateChanged(object? sender, EventArgs e) => UpdateSaveButtonState();

    private void UpdateSaveButtonState() =>
        SaveFileButton.IsEnabled =
            App.CurrentSaveData.IsLoaded && App.CurrentSaveData.HasUnsavedChanges;

    private async System.Threading.Tasks.Task<bool> ConfirmSaveAsync(string sourceFilePath)
    {
        CheckBox neverShowAgainCheckBox = new()
        {
            Content = "Never show again",
            Margin = new Thickness(0, 12, 0, 0),
        };
        StackPanel content = new() { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = $"This will overwrite '{Path.GetFileName(sourceFilePath)}'.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = $"A backup will be created in '{BackupSettings.FolderPath}'.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(neverShowAgainCheckBox);

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Save these changes?",
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        if (neverShowAgainCheckBox.IsChecked == true)
        {
            SaveSettings.ConfirmBeforeSaving = false;
        }

        return true;
    }

    /// <summary>
    /// Copies the save folder path to the clipboard, then swaps the button's copy glyph for a
    /// checkmark until the feedback settles.
    /// </summary>
    private void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // The page shows the path unexpanded, but the clipboard should carry a real one.
            DataPackage dataPackage = new();
            dataPackage.SetText(Environment.ExpandEnvironmentVariables(SaveFolder));
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Failed to copy path: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Copy path error: {ex}");
            return;
        }

        ToolTipService.SetToolTip(CopyPathButton, "Path copied!");
        _copyFeedback.Play(CopyPathButton, () => ToolTipService.SetToolTip(CopyPathButton, _copyTooltip));
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
