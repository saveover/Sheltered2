// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Keeps the advertised editor destination, explanation, and reusable icon source together so the
/// home grid cannot drift from its navigation action or reuse a single-parent IconElement.
/// </summary>
public sealed record EditorFeature(string Name, string Summary, IconSource Icon);

/// <summary>
/// Coordinates the only load/save boundary in the UI. Editor pages mutate the shared in-memory
/// graph; this page alone replaces that graph or commits it to disk, which keeps confirmation,
/// backup, busy-state, and error handling consistent.
/// </summary>
public sealed partial class HomePage : Page
{
    /// <summary>Left unexpanded on screen for portability, but expanded before clipboard or shell use.</summary>
    private const string SaveFolder = @"%userprofile%\AppData\LocalLow\Unicube\Sheltered2";

    /// <summary>One ordered source keeps the home-page promises aligned with shell destinations.</summary>
    public IReadOnlyList<EditorFeature> Features { get; } =
    [
        new("Characters", "Stats, skills, traits", new SymbolIconSource { Symbol = Symbol.People }),
        new("Pets", "Health, happiness, stats", Glyph("")),
        new("Inventory", "Items, quantities", Glyph("")),
        new("Crafting", "Materials, recipes", new SymbolIconSource { Symbol = Symbol.Repair }),
        new("Factions", "Relationships, reputation", new SymbolIconSource { Symbol = Symbol.Flag }),
    ];

    /// <summary>Normalizes glyph sizing because FontIconSource and SymbolIconSource use different defaults.</summary>
    private static FontIconSource Glyph(string glyph) => new() { Glyph = glyph, FontSize = 20 };

    /// <summary>Drives the copy-to-checkmark swap on the copy button.</summary>
    private readonly CopyIconFeedback _copyFeedback = new();
    private readonly ILogger<HomePage> logger = App.LoggerFactory.CreateLogger<HomePage>();

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
            logger.LogWarning(ex, "A selected save file could not be loaded.");
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Unexpected error: {ex.Message}";
            logger.LogError(ex, "An unexpected error occurred while loading a selected save file.");
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
            logger.LogInformation("Save operation started.");
            SetWorkspaceBusy(true);
            // XML rewriting is CPU-bound and may walk a large document. Keep it off the UI thread
            // while the shell lock prevents pages from mutating the shared model mid-snapshot.
            string updatedXml = await Task.Run(() => SaveWriter.ApplyEdits(
                saveData.DecryptedContent,
                saveData.Characters,
                saveData.Pets,
                saveData.Inventory));

            if (string.Equals(updatedXml, saveData.DecryptedContent, StringComparison.Ordinal))
            {
                saveData.CommitSavedContent(updatedXml);
                LoadFileTextBlock.Text = "There are no changes to save.";
                logger.LogInformation("Save operation ended because there were no changes.");
                return;
            }

            if (SaveSettings.ConfirmBeforeSaving &&
                !await ConfirmSaveAsync(sourceFilePath))
            {
                LoadFileTextBlock.Text = "Save cancelled.";
                logger.LogInformation("Save operation cancelled by the user.");
                return;
            }

            await FileHelper.EncryptAndSaveSaveFileAsync(sourceFilePath, updatedXml);
            saveData.CommitSavedContent(updatedXml);
            logger.LogInformation("Save operation completed successfully.");

            LoadFileTextBlock.Text =
                $"Saved '{Path.GetFileName(sourceFilePath)}'. A timestamped backup was created in '{BackupSettings.FolderPath}'.";
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Error saving file: {ex.Message}";
            logger.LogError(ex, "The save operation failed.");
        }
        finally
        {
            SetWorkspaceBusy(false);
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
            logger.LogWarning(ex, "A dropped save file could not be loaded.");
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Unexpected error loading dropped file: {ex.Message}";
            logger.LogError(ex, "An unexpected error occurred while loading a dropped save file.");
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
            logger.LogWarning(ex, "The previously opened save could not be reopened.");
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Unexpected error reopening the previous save: {ex.Message}";
            logger.LogError(ex, "An unexpected error occurred while reopening the previous save.");
        }
        finally
        {
            LoadFileButton.IsEnabled = true;
            UpdateSaveButtonState();
        }
    }

    private async Task LoadSaveAsync(string filePath)
    {
        logger.LogInformation("Save load started.");
        SetWorkspaceBusy(true);
        try
        {
            string fileName = Path.GetFileName(filePath);
            LoadFileTextBlock.Text = $"Loading {fileName}...";

            string decryptedContent = await FileHelper.LoadAndDecryptSaveFileAsync(filePath);
            ParsedSave parsed = await Task.Run(() => SaveParser.Parse(decryptedContent));

            // Publish only after the complete document has parsed so editor pages never observe a
            // partially replaced model graph.
            App.CurrentSaveData.Load(filePath, decryptedContent, parsed);
            if (SaveSettings.RememberLastOpenedSave)
            {
                SaveSettings.LastOpenedSavePath = filePath;
            }

            LoadFileTextBlock.Text =
                $"File '{fileName}' loaded successfully. You can now navigate to other pages to edit your save.";
            logger.LogInformation("Save load completed successfully.");
        }
        finally
        {
            SetWorkspaceBusy(false);
        }
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

        WorkspaceTitleTextBlock.Text = session.HasUnsavedChanges ? "Changes ready to save" : "Save loaded";
    }

    private async Task<bool> ConfirmDiscardChangesAsync(string replacementFilePath)
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

    private async Task<bool> ConfirmSaveAsync(string sourceFilePath)
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
            logger.LogWarning(ex, "Could not copy the save-folder path.");
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
                logger.LogWarning("Windows declined the request to open the save folder.");
            }
        }
        catch (Exception ex)
        {
            LoadFileTextBlock.Text = $"Could not open the Sheltered 2 save folder: {ex.Message}";
            logger.LogWarning(ex, "Could not open the Sheltered 2 save folder.");
        }
    }

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.StartupWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToPageByTag("Donate");
        }
    }

    private static void SetWorkspaceBusy(bool isBusy)
    {
        // The window owns navigation and title-bar commands; routing the lock through it prevents
        // a page switch while a worker is reading or committing the shared graph.
        if (App.StartupWindow is MainWindow mainWindow)
        {
            mainWindow.SetWorkspaceBusy(isBusy);
        }
    }
}
