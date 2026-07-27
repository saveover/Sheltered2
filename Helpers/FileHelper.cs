// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.Windows.Storage.Pickers;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// High-level helpers for picking, loading, validating and saving Sheltered 2 save files.
/// </summary>
internal static class FileHelper
{
    private const string ExpectedHeader = "<root>";
    private const string ExpectedFooter = "</root>";
    private const long MaxFileSize = 25L * 1024 * 1024; // 25 MB
    private const int BackupCopyBufferSize = 80 * 1024;
    private const string BackupFileSuffix = "_backup_";
    private const string BackupDateFormat = "yyyyMMdd_HHmmss";
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Opens a file picker so the user can select a save file.
    /// </summary>
    internal static async Task<string?> PickFileAsync(CancellationToken cancellationToken = default)
    {
        FileOpenPicker picker = new(App.StartupWindow!.AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            FileTypeFilter = { ".dat" },
        };

        try
        {
            PickFileResult? result = await picker.PickSingleFileAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            return result?.Path;
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
    internal static async Task<string> LoadAndDecryptSaveFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fileName = Path.GetFileName(filePath);
        FileInfo fileInfo = new(filePath);
        if (fileInfo.Length is 0 or > MaxFileSize)
        {
            throw new InvalidDataException($"The file '{fileName}' is not a valid save file.");
        }

        try
        {
            // Decrypt exactly once, then validate the resulting text.
            byte[] decryptedData = await XorCipherHelper.LoadAndDecryptAsync(filePath, cancellationToken).ConfigureAwait(false);
            string content = StrictUtf8.GetString(decryptedData);

            return !HasValidSignature(content.AsSpan().Trim())
                ? throw new InvalidDataException($"The file '{fileName}' is not a valid Sheltered 2 save file.")
                : content;
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException($"The file '{fileName}' is not valid UTF-8.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            throw new IOException($"An error occurred while loading or decrypting '{filePath}'.", ex);
        }
    }

    /// <summary>
    /// Encrypts <paramref name="content"/> and writes it back to <paramref name="filePath"/>,
    /// optionally creating a timestamped backup first. The write is staged to a temporary
    /// file and then swapped into place so a crash mid-write cannot corrupt the save.
    /// </summary>
    internal static async Task EncryptAndSaveSaveFileAsync(
        string filePath,
        string content,
        bool createBackup = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(content);

        if (createBackup)
        {
            await CreateBackupAsync(filePath, cancellationToken).ConfigureAwait(false);
        }

        byte[] encryptedBytes = Encoding.UTF8.GetBytes(content);
        XorCipherHelper.Transform(encryptedBytes, encryptedBytes, cancellationToken);

        string destinationPath = filePath;
        string directory = Path.GetDirectoryName(destinationPath)
            ?? throw new IOException($"Could not determine the directory for '{destinationPath}'.");
        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(tempPath, encryptedBytes, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Replace the destination with the fully written temp file in a single move.
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"An error occurred while encrypting or saving '{destinationPath}'.", ex);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    /// <summary>
    /// Creates a timestamped backup copy of the specified file in the same directory.
    /// </summary>
    /// <exception cref="IOException">The backup could not be created, so the save was not changed.</exception>
    internal static async Task CreateBackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        string directory = Path.GetDirectoryName(filePath)
            ?? throw new IOException($"Could not determine the directory for '{filePath}'.");
        string timestamp = DateTime.Now.ToString(BackupDateFormat, CultureInfo.InvariantCulture);
        string backupBaseName = $"{Path.GetFileNameWithoutExtension(filePath)}{BackupFileSuffix}{timestamp}";
        string extension = Path.GetExtension(filePath);

        try
        {
            for (int copyNumber = 1; ; copyNumber++)
            {
                string collisionSuffix = copyNumber == 1 ? string.Empty : $" ({copyNumber})";
                string backupPath = Path.Combine(directory, $"{backupBaseName}{collisionSuffix}{extension}");
                if (await TryCreateBackupAsync(filePath, backupPath, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Could not create a backup of '{filePath}'. The save was not changed.", ex);
        }
    }

    private static async Task<bool> TryCreateBackupAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        bool destinationCreated = false;

        try
        {
            await using FileStream destination = new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BackupCopyBufferSize,
                FileOptions.Asynchronous);
            destinationCreated = true;

            await using FileStream source = new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BackupCopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, BackupCopyBufferSize, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (
            !destinationCreated &&
            ex is IOException or UnauthorizedAccessException &&
            (File.Exists(destinationPath) || Directory.Exists(destinationPath)))
        {
            return false;
        }
        catch
        {
            if (destinationCreated)
            {
                TryDeleteFile(destinationPath);
            }

            throw;
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
    /// the removal cannot be performed (for example, if the file is locked).
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; ignore failures.
        }
    }
}
