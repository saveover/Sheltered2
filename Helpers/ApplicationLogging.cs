// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SaveOver.Sheltered2.Helpers;

/// <summary>
/// Configures the application-wide <see cref="ILoggerFactory"/> and its rolling file provider.
/// </summary>
internal static class ApplicationLogging
{
    private const int MaxRetainedLogs = 10;
    private const long MaxLogFileSize = 5L * 1024 * 1024;

    /// <summary>Gets the folder containing the retained application logs.</summary>
    internal static string LogDirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveOver",
        "Sheltered2",
        "Logs");

    /// <summary>
    /// Creates an <see cref="ILoggerFactory"/> backed by rolling, privacy-filtered text files.
    /// Failure falls back to a no-op factory so diagnostics can never prevent application startup.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification =
        "Logging initialization must never prevent the application from starting.")]
    internal static ILoggerFactory CreateLoggerFactory()
    {
        try
        {
            _ = Directory.CreateDirectory(LogDirectoryPath);
            SelfLog.Enable(message => Debug.WriteLine(message));

            Serilog.ILogger providerLogger = new LoggerConfiguration()
                .MinimumLevel.Is(
#if DEBUG
                    LogEventLevel.Debug)
#else
                    LogEventLevel.Information)
#endif
                .Enrich.FromLogContext()
                .WriteTo.File(
                    new PrivacyTextFormatter(),
                    Path.Combine(LogDirectoryPath, "SaveOver-.log"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: MaxLogFileSize,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: MaxRetainedLogs,
                    shared: true)
                .CreateLogger();

            return Microsoft.Extensions.Logging.LoggerFactory.Create(
                builder => builder.AddSerilog(providerLogger, dispose: true));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not initialize application logging: {ex}");
            return NullLoggerFactory.Instance;
        }
    }
}

/// <summary>
/// Removes local paths from the final rendered event before the file sink persists it.
/// </summary>
internal sealed partial class PrivacyTextFormatter : ITextFormatter
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: " +
        "{Message:lj}{NewLine}{Exception}";

    private static readonly (string Path, string Replacement)[] RedactedPaths =
    [
        (AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "<APPDIR>"),
        (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
        (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%"),
    ];

    private readonly MessageTemplateTextFormatter formatter = new(OutputTemplate, CultureInfo.InvariantCulture);

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        using StringWriter rendered = new(CultureInfo.InvariantCulture);
        formatter.Format(logEvent, rendered);
        output.Write(Sanitize(rendered.ToString()));
    }

    private static string Sanitize(string value)
    {
        string sanitized = value;
        foreach ((string path, string replacement) in RedactedPaths)
        {
            if (!string.IsNullOrEmpty(path))
            {
                sanitized = sanitized.Replace(path, replacement, StringComparison.OrdinalIgnoreCase);
            }
        }

        return AbsoluteWindowsPathRegex().Replace(sanitized, "<PATH>");
    }

    [GeneratedRegex(@"(?<![\w])(?:[A-Za-z]:\\|\\\\)[^\r\n""']+", RegexOptions.CultureInvariant)]
    private static partial Regex AbsoluteWindowsPathRegex();
}
