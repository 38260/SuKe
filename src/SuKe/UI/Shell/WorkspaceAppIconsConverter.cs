using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SuKe.Models;

namespace SuKe.UI.Shell;

/// <summary>
/// Projects one workspace row into the distinct application icons that workspace will bring back.
/// </summary>
/// <remarks>
/// Icon extraction touches the file system, so the badges come from <see cref="ApplicationIconHelper"/>,
/// which caches and freezes one source per executable path. Identity is the executable, packaged app id,
/// or process name — never a window title, so the same app opened twice still yields a single badge.
/// </remarks>
internal sealed class WorkspaceAppIconsConverter : IValueConverter
{
    /// <summary>Distinct apps drawn before the row collapses the remainder into a counter.</summary>
    public const int MaximumIcons = 8;

    private const double BadgeSize = 28;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not WorkspaceRow row
            ? Array.Empty<FrameworkElement>()
            : Build(row.Source);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IReadOnlyList<FrameworkElement> Build(WorkspaceSnapshot workspace)
    {
        var badges = new List<FrameworkElement>(MaximumIcons + 1);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int overflow = 0;

        foreach (WorkspaceEntry entry in workspace.Entries)
        {
            string label = Describe(entry);
            string identity = Identify(entry);
            if (!seen.Add(identity.Length > 0 ? identity : label)) continue;

            if (badges.Count >= MaximumIcons)
            {
                overflow++;
                continue;
            }

            badges.Add(ApplicationIconHelper.CreateBadge(entry.ExecutablePath, label, BadgeSize));
        }

        if (overflow > 0)
            badges.Add(BuildOverflowBadge(overflow));

        return badges;
    }

    /// <summary>Stable grouping key for one captured window.</summary>
    private static string Identify(WorkspaceEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.ExecutablePath)) return entry.ExecutablePath;
        if (!string.IsNullOrWhiteSpace(entry.AppUserModelId)) return entry.AppUserModelId;
        return entry.ProcessName ?? "";
    }

    /// <summary>Tooltip text for one badge, preferring the human app name over a raw process name.</summary>
    private static string Describe(WorkspaceEntry entry)
    {
        if (entry.IsWebApp && !string.IsNullOrWhiteSpace(entry.WebAppName)) return entry.WebAppName;
        if (!string.IsNullOrWhiteSpace(entry.ProcessName)) return entry.ProcessName;
        if (!string.IsNullOrWhiteSpace(entry.ExecutablePath))
            return Path.GetFileNameWithoutExtension(entry.ExecutablePath);
        if (!string.IsNullOrWhiteSpace(entry.AppUserModelId))
            return entry.AppUserModelId.Split('!', 2)[0].Split('_')[0];
        return Localization.Loc.T("Application");
    }

    private static FrameworkElement BuildOverflowBadge(int count)
    {
        // Matches the corner radius ApplicationIconHelper uses for its own letter fallback so a
        // truncated row never looks like two different badge shapes.
        var border = new Border
        {
            Width = BadgeSize,
            Height = BadgeSize,
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            ToolTip = Localization.Loc.Format("{0} more", count),
        };
        border.Child = new TextBlock
        {
            Text = $"+{count}",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        return border;
    }
}
