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
using Windows.Storage;

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
    private bool startupResumePromptHandled;

    public HomePage()
    {
        InitializeComponent();

        _copyTooltip = ToolTipService.GetToolTip(CopyPathButton);

        // Keep the Save button in step with the shared load state, so it stays enabled even
        // if this page is recreated after a file was already loaded.
        App.CurrentSaveData.DirtyStateChanged += CurrentSaveData_DirtyStateChanged;
        UpdateSaveButtonState();
        Loaded += HomePage_Loaded;
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

            if (App.CurrentSaveData.HasUnsavedChanges &&
                !await ConfirmDiscardChangesAsync(filePath))
            {
                LoadFileTextBlock.Text = "Load cancelled. Your unsaved changes are still open.";
                return;
            }

            await LoadSaveAsync(filePath);
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

    private void SaveDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.None;
        if (!LoadFileButton.IsEnabled ||
            !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Open this save";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
        LoadFileButton.BorderThickness = new Thickness(2);
        e.Handled = true;
    }

    private void SaveDropZone_DragLeave(object sender, DragEventArgs e) =>
        LoadFileButton.BorderThickness = new Thickness(1);

    private async void SaveDropZone_Drop(object sender, DragEventArgs e)
    {
        LoadFileButton.BorderThickness = new Thickness(1);
        e.Handled = true;

        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                LoadFileTextBlock.Text = "Drop one Sheltered 2 .dat save file.";
                return;
            }

            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            if (items.Count != 1 ||
                items[0] is not StorageFile file ||
                !string.Equals(file.FileType, ".dat", StringComparison.OrdinalIgnoreCase))
            {
                LoadFileTextBlock.Text = "Only one Sheltered 2 .dat save file can be opened at a time.";
                return;
            }

            if (App.CurrentSaveData.HasUnsavedChanges &&
                !await ConfirmDiscardChangesAsync(file.Path))
            {
                LoadFileTextBlock.Text = "Load cancelled. Your unsaved changes are still open.";
                return;
            }

            LoadFileButton.IsEnabled = false;
            SaveFileButton.IsEnabled = false;
            await LoadSaveAsync(file.Path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            LoadFileTextBlock.Text = $"Error loading dropped file: {ex.Message}";
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Unexpected error loading dropped file: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Drop save error: {ex}");
        }
        finally
        {
            LoadFileButton.IsEnabled = true;
            UpdateSaveButtonState();
        }
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        if (startupResumePromptHandled || App.CurrentSaveData.IsLoaded)
        {
            return;
        }

        startupResumePromptHandled = true;
        if (!SaveSettings.RememberLastOpenedSave)
        {
            return;
        }

        string? lastSavePath = SaveSettings.LastOpenedSavePath;
        if (string.IsNullOrWhiteSpace(lastSavePath))
        {
            return;
        }

        if (!File.Exists(lastSavePath))
        {
            SaveSettings.LastOpenedSavePath = null;
            LoadFileTextBlock.Text = "The previously opened save file could not be found.";
            return;
        }

        StackPanel dialogContent = new() { Spacing = 10 };
        dialogContent.Children.Add(new TextBlock
        {
            Text =
                "SaveOver remembers the last save you opened so you can return to editing " +
                "without finding the file again.",
            TextWrapping = TextWrapping.Wrap,
        });
        dialogContent.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(lastSavePath),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsTextSelectionEnabled = true,
        });
        dialogContent.Children.Add(new TextBlock
        {
            Text = lastSavePath,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Opacity = 0.7,
        });
        dialogContent.Children.Add(new TextBlock
        {
            Text = "Reopening the file will not change it. Nothing is written until you save an edit.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
        });

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Title = "Continue where you left off?",
            Content = dialogContent,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        LoadFileButton.IsEnabled = false;
        SaveFileButton.IsEnabled = false;
        try
        {
            await LoadSaveAsync(lastSavePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            SaveSettings.LastOpenedSavePath = null;
            LoadFileTextBlock.Text = $"Could not reopen the previous save: {ex.Message}";
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Unexpected error reopening the previous save: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Resume save error: {ex}");
        }
        finally
        {
            LoadFileButton.IsEnabled = true;
            UpdateSaveButtonState();
        }
    }

    private async System.Threading.Tasks.Task LoadSaveAsync(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        LoadFileTextBlock.Text = $"Loading {fileName}...";

        string decryptedContent = await FileHelper.LoadAndDecryptSaveFileAsync(filePath);
        ParsedSave parsed = SaveParser.Parse(decryptedContent);

        // Raises SaveDataChanged, which unlocks navigation and refreshes the editor pages.
        App.CurrentSaveData.Load(filePath, decryptedContent, parsed);
        if (SaveSettings.RememberLastOpenedSave)
        {
            SaveSettings.LastOpenedSavePath = filePath;
        }

        LoadFileTextBlock.Text =
            $"File '{fileName}' loaded successfully. You can now navigate to other pages to edit your save.";
    }

    private void CurrentSaveData_DirtyStateChanged(object? sender, EventArgs e) => UpdateSaveButtonState();

    private void UpdateSaveButtonState()
    {
        SaveSession session = App.CurrentSaveData;
        SaveFileButton.IsEnabled = session.IsLoaded && session.HasUnsavedChanges;

        Style accentButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
        SaveFileButton.Style = session.IsLoaded ? accentButtonStyle : null;

        if (!session.IsLoaded)
        {
            WorkspaceTitleTextBlock.Text = "Choose a save file";
            return;
        }

        if (session.HasUnsavedChanges)
        {
            WorkspaceTitleTextBlock.Text = "Changes ready to save";
        }
        else
        {
            WorkspaceTitleTextBlock.Text = "Save loaded";
        }
    }

    private async System.Threading.Tasks.Task<bool> ConfirmDiscardChangesAsync(string replacementFilePath)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Title = "Discard unsaved changes?",
            Content =
                $"Loading '{Path.GetFileName(replacementFilePath)}' will replace the save currently open in the editor.",
            PrimaryButtonText = "Discard and load",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

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
            RequestedTheme = ActualTheme,
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

    private async void OpenSaveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string saveFolderPath = Environment.ExpandEnvironmentVariables(SaveFolder);
            if (!Directory.Exists(saveFolderPath))
            {
                LoadFileTextBlock.Text =
                    "The Sheltered 2 save folder does not exist yet. Start the game once to create it.";
                return;
            }

            if (!await Windows.System.Launcher.LaunchFolderPathAsync(saveFolderPath))
            {
                LoadFileTextBlock.Text = "Windows could not open the Sheltered 2 save folder.";
            }
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Could not open the Sheltered 2 save folder: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Open save folder error: {ex}");
        }
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
