using System;
using System.IO;
using System.Text;

namespace SaveOver.Sheltered2.Helpers;

internal static class CrashLogger
{
    private static readonly object SyncRoot = new();

    internal static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveOver",
        "Sheltered2",
        "Logs",
        "crashes.log");

    internal static void Write(string source, Exception exception)
    {
        try
        {
            StringBuilder entry = new()
            {
                Capacity = 512,
            };

            entry.AppendLine("--------------------------------------------------------------------------------");
            entry.Append("UTC: ").AppendLine(DateTimeOffset.UtcNow.ToString("O"));
            entry.Append("Source: ").AppendLine(source);
            entry.Append("Version: ").AppendLine(typeof(App).Assembly.GetName().Version?.ToString());
            entry.Append("OS: ").AppendLine(Environment.OSVersion.VersionString);
            entry.Append("Framework: ").AppendLine(Environment.Version.ToString());
            entry.AppendLine(exception.ToString());

            lock (SyncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, entry.ToString());
            }
        }
        catch
        {
            // Crash reporting must never replace the original failure.
        }
    }
}
