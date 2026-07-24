// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// High-level helpers for picking, loading, validating and saving Sheltered 2 save files.
/// </summary>
internal static class FileHelper
{
    private const string ExpectedHeader = "<root>";
    private const string ExpectedFooter = "</root>";
    private const ulong MaxFileSize = 25UL * 1024 * 1024; // 25 MB
    private const string BackupFileSuffix = "_backup_";
    private const string BackupDateFormat = "yyyyMMdd_HHmmss";

    /// <summary>
    /// Opens a file picker so the user can select a save file.
    /// </summary>
    internal static async Task<StorageFile?> PickFileAsync(CancellationToken cancellationToken = default)
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            FileTypeFilter = { ".dat" },
        };

        // A WinUI 3 desktop app must associate the picker with the app window's HWND.
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.StartupWindow!));

        try
        {
            return await picker.PickSingleFileAsync().AsTask(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            throw new InvalidOperationException("An error occurred while opening the file picker.", ex);
        }
    }

    /// <summary>
    /// Loads, decrypts and validates the specified save file, returning its XML content.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// Thrown when the file is empty, too large, or is not a valid Sheltered 2 save file.
    /// </exception>
    internal static async Task<string> LoadAndDecryptSaveFileAsync(StorageFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        BasicProperties properties = await file.GetBasicPropertiesAsync();
        if (properties.Size is 0 or > MaxFileSize)
        {
            throw new InvalidDataException($"The file '{file.Name}' is not a valid save file.");
        }

        try
        {
            // Decrypt exactly once, then validate the resulting text.
            byte[] decryptedData = await XorCipherHelper.LoadAndDecryptAsync(file.Path, cancellationToken).ConfigureAwait(false);
            string content = Encoding.UTF8.GetString(decryptedData);

            return !HasValidSignature(content.AsSpan().Trim())
                ? throw new InvalidDataException($"The file '{file.Name}' is not a valid Sheltered 2 save file.")
                : content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            throw new IOException($"An error occurred while loading or decrypting '{file.Path}'.", ex);
        }
    }

    /// <summary>
    /// Encrypts <paramref name="content"/> and writes it back to <paramref name="file"/>,
    /// optionally creating a timestamped backup first. The write is staged to a temporary
    /// file and then swapped into place so a crash mid-write cannot corrupt the save.
    /// </summary>
    internal static async Task EncryptAndSaveSaveFileAsync(
        StorageFile file,
        string content,
        bool createBackup = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(content);

        if (createBackup)
        {
            _ = await CreateBackupAsync(file, cancellationToken).ConfigureAwait(false);
        }

        byte[] encryptedBytes = XorCipherHelper.Transform(Encoding.UTF8.GetBytes(content), cancellationToken);

        string destinationPath = file.Path;
        string tempPath = destinationPath + ".tmp";

        try
        {
            await File.WriteAllBytesAsync(tempPath, encryptedBytes, cancellationToken).ConfigureAwait(false);

            // Replace the destination with the fully written temp file in a single move.
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteFile(tempPath);
            throw new IOException($"An error occurred while encrypting or saving '{destinationPath}'.", ex);
        }
    }

    /// <summary>
    /// Creates a timestamped backup copy of the specified file in the same directory.
    /// Returns the backup file, or <see langword="null"/> if the backup could not be made.
    /// </summary>
    internal static async Task<StorageFile?> CreateBackupAsync(StorageFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            StorageFolder folder = await file.GetParentAsync();
            string timestamp = DateTime.Now.ToString(BackupDateFormat, CultureInfo.InvariantCulture);
            string backupFileName =
                $"{Path.GetFileNameWithoutExtension(file.Name)}{BackupFileSuffix}{timestamp}{Path.GetExtension(file.Name)}";

            return await file.CopyAsync(folder, backupFileName, NameCollisionOption.GenerateUniqueName)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether decrypted content carries the expected XML header and footer.
    /// </summary>
    private static bool HasValidSignature(ReadOnlySpan<char> decryptedContent) =>
        decryptedContent.StartsWith(ExpectedHeader, StringComparison.Ordinal) &&
        decryptedContent.EndsWith(ExpectedFooter, StringComparison.Ordinal);

    /// <summary>
    /// Attempts to delete the temporary staging file from <see cref="EncryptAndSaveSaveFileAsync"/> 
    /// at the given <paramref name="path"/> as a best-effort cleanup. 
    /// Any IO- or permission-related failures are caught and ignored so callers do not fail if 
    /// the removal cannot be performed (for example, if the file is deadlocked).
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; ignore failures.
        }
    }
}
