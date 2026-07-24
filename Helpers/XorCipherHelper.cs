// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Implements the repeating-key XOR transform that Sheltered 2 uses to obfuscate its save
/// files.
/// </summary>
/// <remarks>
/// This is obfuscation, not encryption. The key ships with the game and is required to
/// read or write the on-disk format, so it is embedded here by necessity; it provides no
/// confidentiality and must not be treated as a secret. XOR is its own inverse, so the
/// same <see cref="Transform(ReadOnlySpan{byte}, CancellationToken)"/> both decrypts and
/// encrypts.
/// </remarks>
internal static class XorCipherHelper
{
    // 17-byte repeating key used by Sheltered 2.
    private static readonly ImmutableArray<byte> XorKey =
    [
        0xAC, 0x73, 0xFE, 0xF2, 0xAA, 0xBA, 0x6D, 0xAB, 0x30,
        0x3A, 0x8B, 0xA7, 0xDE, 0x0D, 0x15, 0x21, 0x4A,
    ];

    // How often to observe cancellation while transforming. A power of two so the check
    // can be a cheap bit-mask rather than a modulo. Files are usually small (<= 5 MB),
    // so checking every 64 KB keeps the loop responsive without measurable overhead.
    private const int CancellationCheckInterval = 64 * 1024;

    /// <summary>
    /// Reads a file from disk and returns its XOR-transformed (decrypted) bytes.
    /// </summary>
    internal static async Task<byte[]> LoadAndDecryptAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{filePath}' was not found.", filePath);
        }

        byte[] fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

        // XOR is symmetric, so we can transform the buffer we just read in place.
        Transform(fileBytes, fileBytes, cancellationToken);
        return fileBytes;
    }

    /// <summary>
    /// Returns a new array containing the XOR transform of <paramref name="input"/>.
    /// </summary>
    internal static byte[] Transform(ReadOnlySpan<byte> input, CancellationToken cancellationToken = default)
    {
        if (input.IsEmpty)
        {
            return [];
        }

        byte[] output = new byte[input.Length];
        Transform(input, output, cancellationToken);
        return output;
    }

    /// <summary>
    /// Writes the XOR transform of <paramref name="input"/> into <paramref name="output"/>.
    /// The two spans may refer to the same buffer for an in-place transform.
    /// </summary>
    internal static void Transform(ReadOnlySpan<byte> input, Span<byte> output, CancellationToken cancellationToken = default)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("The output buffer is smaller than the input.", nameof(output));
        }

        ReadOnlySpan<byte> key = XorKey.AsSpan();
        int keyLength = key.Length;

        for (int i = 0; i < input.Length; i++)
        {
            if ((i & (CancellationCheckInterval - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            output[i] = (byte)(input[i] ^ key[i % keyLength]);
        }
    }
}
