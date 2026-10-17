using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SuKe.Localization;
using SuKe.Models;

namespace SuKe.UI.Shell;

/// <summary>
/// Builds the one-line monitor / window / save summary shown under a workspace card title.
/// </summary>
/// <remarks>
/// The wording is composed here rather than in XAML because Chinese and English put the unit and the
/// number in different orders; the localisation catalog owns the whole phrase.
/// </remarks>
internal sealed class WorkspaceMetaTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is WorkspaceRow row ? Describe(row.Source) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string Describe(WorkspaceSnapshot workspace)
    {
        var parts = new List<string>(4);
        if (workspace.Monitors.Count > 0)
            parts.Add(Loc.Format("{0} monitor(s)", workspace.Monitors.Count));
        parts.Add(Loc.Format("{0} window(s)", workspace.Entries.Count));
        parts.Add(DescribeSavedAt(workspace.SavedAt));
        if (workspace.SavedWithFiles &&
            workspace.Entries.Any(entry => !string.IsNullOrWhiteSpace(entry.FilePath)))
            parts.Add(Loc.T("with files"));

        return string.Join("  ·  ", parts);
    }

    private static string DescribeSavedAt(DateTime savedAt)
    {
        DateTime local = savedAt.ToLocalTime();
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        DateOnly saved = DateOnly.FromDateTime(local);
        if (saved == today) return Loc.T("Saved today");
        if (saved == today.AddDays(-1)) return Loc.T("Saved yesterday");
        return Loc.Format(
            "Saved {0}",
            local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
