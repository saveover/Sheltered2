// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.Extensions.Logging;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
    private const long MaxFileSize = 25L * 1024 * 1024; // 25 MB
    private const int BackupCopyBufferSize = 80 * 1024;
    private const string BackupFileSuffix = "_backup_";
    private const string BackupDateFormat = "yyyyMMdd_HHmmss";
    private static readonly ILogger Logger = App.LoggerFactory.CreateLogger(typeof(FileHelper).FullName!);
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
    /// Opens a folder picker so the user can choose where backups are stored.
    /// </summary>
    internal static async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        FolderPicker picker = new(App.StartupWindow!.AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            CommitButtonText = "Select folder",
            SettingsIdentifier = "BackupFolder",
        };

        try
        {
            PickFolderResult? result = await picker.PickSingleFolderAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            return result?.Path;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            throw new InvalidOperationException("An error occurred while opening the folder picker.", ex);
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
            // Decrypt exactly once; SaveParser performs structural XML validation.
            byte[] decryptedData = await XorCipherHelper.LoadAndDecryptAsync(filePath, cancellationToken).ConfigureAwait(false);
            return StrictUtf8.GetString(decryptedData);
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

            // The destination already exists because saves are loaded before they are edited.
            // File.Replace expresses the intended same-volume staged replacement and preserves
            // destination metadata instead of treating the operation as an ordinary move.
            File.Replace(tempPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: false);
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
    /// Creates a timestamped backup copy of the specified file in the configured backup directory.
    /// </summary>
    /// <exception cref="IOException">The backup could not be created, so the save was not changed.</exception>
    internal static async Task CreateBackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        string directory = BackupSettings.FolderPath;
        string timestamp = DateTime.Now.ToString(BackupDateFormat, CultureInfo.InvariantCulture);
        string backupBaseName = $"{Path.GetFileNameWithoutExtension(filePath)}{BackupFileSuffix}{timestamp}";
        string extension = Path.GetExtension(filePath);

        try
        {
            _ = Directory.CreateDirectory(directory);

            for (int copyNumber = 1; ; copyNumber++)
            {
                string collisionSuffix = copyNumber == 1 ? string.Empty : $" ({copyNumber})";
                string backupPath = Path.Combine(directory, $"{backupBaseName}{collisionSuffix}{extension}");
                if (await TryCreateBackupAsync(filePath, backupPath, cancellationToken).ConfigureAwait(false))
                {
                    PruneBackups(filePath, directory);
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Could not create a backup of '{filePath}'. The save was not changed.", ex);
        }
    }

    /// <summary>
    /// Removes only backups created for this exact source-save name. Cleanup is deliberately
    /// disabled in the game's Steam Cloud directory so deleted files cannot be downloaded again
    /// and repeatedly deleted on subsequent saves.
    /// </summary>
    private static void PruneBackups(string sourcePath, string backupDirectory)
    {
        int retentionCount = BackupSettings.RetentionCount;
        if (retentionCount == 0 || BackupSettings.IsGameSaveFolder)
        {
            return;
        }

        string sourceStem = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = Path.GetExtension(sourcePath);
        string prefix = $"{sourceStem}{BackupFileSuffix}";

        try
        {
            FileInfo[] backups = [.. new DirectoryInfo(backupDirectory)
                .EnumerateFiles($"{prefix}*{extension}", SearchOption.TopDirectoryOnly)
                .Where(file => IsRecognizedBackup(file, prefix, extension))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)];

            foreach (FileInfo backup in backups.Skip(retentionCount))
            {
                try
                {
                    backup.Delete();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.LogWarning(ex, "Could not prune an old backup.");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(ex, "Could not enumerate backups for retention cleanup.");
        }
    }

    private static bool IsRecognizedBackup(FileInfo file, string prefix, string extension)
    {
        string fileName = file.Name;
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = fileName[prefix.Length..^extension.Length];
        if (suffix.Length < BackupDateFormat.Length ||
            !DateTime.TryParseExact(
                suffix[..BackupDateFormat.Length],
                BackupDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return false;
        }

        ReadOnlySpan<char> collision = suffix.AsSpan(BackupDateFormat.Length);
        return collision.IsEmpty ||
               collision.StartsWith(" (", StringComparison.Ordinal) &&
                collision.EndsWith(')') &&
                int.TryParse(collision[2..^1], NumberStyles.None, CultureInfo.InvariantCulture, out int copyNumber) &&
                copyNumber >= 2;
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
            Logger.LogWarning(ex, "Could not remove a temporary staging file.");
        }
    }
}
