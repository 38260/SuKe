using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SuKe.UI;

/// <summary>
/// Resolves the friendly application name shown next to an icon in the restore preview.
/// </summary>
/// <remarks>
/// Reads the executable's version resource ("Visual Studio Code", "Windows 资源管理器") so the
/// preview never has to hard-code brand names. Results are cached per path because version
/// metadata is a file-system read; anything unreadable falls back to the caller's identity label.
/// </remarks>
internal static class ApplicationNameHelper
{
    private static readonly Dictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Friendly name when the executable exposes one, otherwise <paramref name="fallbackLabel"/>.</summary>
    public static string DisplayName(string? executablePath, string fallbackLabel)
    {
        string? resolved = TryGetDisplayName(executablePath);
        return string.IsNullOrWhiteSpace(resolved) ? fallbackLabel : resolved;
    }

    /// <summary>Version-resource description, or <c>null</c> when it cannot be read.</summary>
    public static string? TryGetDisplayName(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;

        lock (Cache)
        {
            if (Cache.TryGetValue(executablePath, out string? cached)) return cached;

            string? resolved = Read(executablePath);
            Cache[executablePath] = resolved;
            return resolved;
        }
    }

    private static string? Read(string executablePath)
    {
        try
        {
            if (!File.Exists(executablePath)) return null;
            string description = FileVersionInfo.GetVersionInfo(executablePath).FileDescription?.Trim() ?? "";
            return description.Length == 0 ? null : description;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
