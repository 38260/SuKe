using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = Wpf.Ui.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Path = System.Windows.Shapes.Path;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SuKe.UI;

/// <summary>
/// Displays an immutable restore plan grouped by application and derives an approved copy when
/// windows are cleared. Matching and resource discovery never run in this window.
/// </summary>
/// <remarks>
/// Grouping is presentation only: every checkbox still selects one saved window entry, and the
/// approved plan is derived from those same entries, so per-entry ids, browser sessions and
/// placement targets survive the regrouping. Diagnostics are collapsed by default and remain
/// available behind each row's disclosure.
/// </remarks>
internal sealed class RestorePlanPreviewDialog : FluentWindow
{
    private const double AppIconSize = 28;
    private const double RowIndent = 30;

    private const string ChevronDown = "M 0 0 L 4 4 L 8 0";
    private const string ChevronUp = "M 0 4 L 4 0 L 8 4";

    private readonly Brush _accent;
    private readonly Brush _expandedBackground;
    private readonly Brush _runningColor;
    private readonly Brush _blockedColor;
    private readonly Brush _cautionColor;
    private readonly Brush _dividerBrush;
    private readonly Brush _secondaryText;
    private readonly Brush _tertiaryText;
    private readonly Brush _primaryText;

    private readonly bool _isWorkspaceSwitch;
    private readonly bool _isPreviewOnly;

    private RestorePlan _workingPlan;
    private readonly RestorePlanPreview _initialPreview;
    private readonly IReadOnlyList<RestorePlanPreviewAppGroup> _groups;

    private readonly Dictionary<int, CheckBox> _entryControls = new();
    private readonly Dictionary<int, TextBlock> _entryStatuses = new();
    private readonly Dictionary<string, CheckBox> _groupControls = new();
    private readonly Dictionary<string, TextBlock> _groupStatuses = new();
    private readonly Dictionary<string, Path> _groupChevrons = new();
    private readonly Dictionary<string, StackPanel> _groupDetailPanels = new();
    private readonly Dictionary<string, FrameworkElement> _groupDetailHosts = new();
    private readonly HashSet<string> _groupExpanded = new();
    private readonly HashSet<string> _groupDetailsBuilt = new();
    private readonly Dictionary<string, bool> _groupAllSelected = new();
    private readonly Dictionary<(int EntryIndex, long WindowHandle), RadioButton> _candidateControls = new();
    private readonly Dictionary<int, CheckBox> _rememberControls = new();

    private readonly CheckBox _selectAll;
    private readonly TextBlock _summaryText;
    private readonly StackPanel _noticePanel;
    private readonly Button _primaryButton;

    private bool _updatingSelection;
    private bool _allWindowsSelected;

    internal RestorePlan? ApprovedPlan { get; private set; }
    internal IReadOnlyList<WindowMatchHint> ApprovedMatchHints { get; private set; } =
        Array.Empty<WindowMatchHint>();

    internal RestorePlanPreviewDialog(RestorePlan previewPlan, bool isWorkspaceSwitch = false)
    {
        _workingPlan = previewPlan ?? throw new ArgumentNullException(nameof(previewPlan));
        _isWorkspaceSwitch = isWorkspaceSwitch;
        _isPreviewOnly = previewPlan.Mode == RestoreModeKind.PreviewOnly;

        bool dark = PrefersDarkPalette();
        _accent = Solid(dark ? "#9675F5" : "#7050DF");
        _expandedBackground = Solid(dark ? "#292733" : "#F7F6FC");
        _runningColor = Solid(dark ? "#5FD08A" : "#1F8A4C");
        _blockedColor = Solid(dark ? "#FF99A4" : "#C42B1C");
        _cautionColor = Resource("SystemFillColorCautionBrush", Solid(dark ? "#F0B354" : "#9A5B00"));
        _dividerBrush = Solid(dark ? "#363540" : "#EBEBF2");
        _secondaryText = Solid(dark ? "#B9B6C5" : "#666575");
        _tertiaryText = Solid(dark ? "#9995A7" : "#858291");
        _primaryText = Solid(dark ? "#F2F0F7" : "#242232");

        _initialPreview = RestorePlanPreviewBuilder.Build(PrepareRestorePlan(_workingPlan));
        _groups = _initialPreview.Groups;

        Title = Loc.Format("Restore \"{0}\"", _initialPreview.WorkspaceName);
        Width = 740;
        Height = Math.Min(820, SystemParameters.WorkArea.Height - 48);
        MinWidth = 600;
        MinHeight = 520;
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
        Background = Solid(dark ? "#222129" : "#FAFAFD");
        Foreground = _primaryText;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;
        ShowInTaskbar = false;

        UseDialogAccent();

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBar = new TitleBar
        {
            Title = "溯刻 · SuKe",
            ShowMinimize = false,
            ShowMaximize = false
        };
        AutomationProperties.SetName(
            titleBar,
            Loc.Format("Restore plan preview for {0}", _initialPreview.WorkspaceName));
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        var body = new Grid { Margin = new Thickness(24, 0, 24, 0) };
        for (int row = 0; row < 8; row++)
        {
            body.RowDefinitions.Add(row == 4
                ? new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                : new RowDefinition { Height = GridLength.Auto });
        }
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var headerMeta = new TextBlock
        {
            Text = Loc.Format("{0} apps · {1} windows", _initialPreview.AppCount, _initialPreview.WindowCount),
            FontSize = 12,
            Foreground = _secondaryText,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 4, 0, 0)
        };
        AutomationProperties.SetName(
            headerMeta,
            Loc.Format("{0} applications, {1} saved windows",
                _initialPreview.AppCount, _initialPreview.WindowCount));
        body.Children.Add(BuildHeader(headerMeta));

        _selectAll = new CheckBox
        {
            Content = Loc.T("Select all windows"),
            IsThreeState = true,
            FontSize = 13,
            Margin = new Thickness(12, 0, 0, 12)
        };
        AutomationProperties.SetName(_selectAll, Loc.T("Select all restore entries"));
        _selectAll.Click += (_, _) =>
        {
            if (_updatingSelection) return;
            ApplySelectionToAll(!_allWindowsSelected);
        };
        Grid.SetRow(_selectAll, 2);
        body.Children.Add(_selectAll);

        var list = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
        int tabIndex = 0;
        foreach (RestorePlanPreviewAppGroup group in _groups)
        {
            AddGroup(list, group, ref tabIndex);
            list.Children.Add(Divider(new Thickness(0, 0, 0, 0)));
        }
        if (list.Children.Count > 0) list.Children.RemoveAt(list.Children.Count - 1);

        var scroll = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Padding = new Thickness(0, 0, 6, 0)
        };
        AutomationProperties.SetName(scroll, Loc.T("Restore plan entries"));
        var listSurface = new Border
        {
            Background = Resources["RestoreSurface"] as Brush,
            BorderBrush = _dividerBrush, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 2, 6, 2),
            Child = scroll,
        };
        Grid.SetRow(listSurface, 4);
        body.Children.Add(listSurface);

        _noticePanel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        Grid.SetRow(_noticePanel, 5);
        body.Children.Add(_noticePanel);

        _summaryText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = _secondaryText,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 12, 0)
        };
        AutomationProperties.SetLiveSetting(_summaryText, AutomationLiveSetting.Polite);

        _primaryButton = new Button
        {
            Appearance = ControlAppearance.Primary,
            IsDefault = true,
            Style = (Style)Resources["RestorePrimaryButton"],
            MinWidth = 136,
            TabIndex = tabIndex + 1
        };
        AutomationProperties.SetName(
            _primaryButton,
            _isWorkspaceSwitch
                ? "Approve plan and switch to selected entries"
                : "Approve plan and restore selected entries");
        _primaryButton.Click += (_, _) => Approve();
        body.Children.Add(BuildFooter(_summaryText, _primaryButton, tabIndex));

        Content = root;

        foreach (RestorePlanPreviewAppGroup group in _groups.Where(group => group.WindowCount > 1))
            ExpandGroup(group, expand: true);

        RefreshApprovalState();
        Loaded += (_, _) =>
            _entryControls.Values.FirstOrDefault(control => control.IsEnabled)?.Focus();
    }

    // ── Layout ────────────────────────────────────────────────────────────────────────────────

    private FrameworkElement BuildHeader(TextBlock meta)
    {
        var header = new Grid { Margin = new Thickness(0, 16, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = Loc.Format("Restore \"{0}\"", _initialPreview.WorkspaceName),
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = _primaryText,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        header.Children.Add(title);
        Grid.SetColumn(meta, 1);
        header.Children.Add(meta);
        return header;
    }

    private FrameworkElement BuildFooter(TextBlock summary, Button primary, int tabIndex)
    {
        var footer = new Grid { Margin = new Thickness(0, 16, 0, 20) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(summary);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button
        {
            Content = Loc.T("Cancel"),
            Appearance = ControlAppearance.Secondary,
            IsCancel = true,
            Style = (Style)Resources["RestoreSecondaryButton"],
            MinWidth = 86,
            Margin = new Thickness(0, 0, 8, 0),
            TabIndex = tabIndex,
            Visibility = _isPreviewOnly ? Visibility.Collapsed : Visibility.Visible
        };
        AutomationProperties.SetName(cancel, "Cancel restore");
        cancel.Click += (_, _) =>
        {
            ApprovedPlan = null;
            DialogResult = false;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(primary);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);

        var host = new StackPanel();
        host.Children.Add(footer);
        Grid.SetRow(host, 7);
        return host;
    }

    private Border Divider(Thickness margin) => new()
    {
        Height = 1,
        Background = _dividerBrush,
        Margin = margin
    };

    private static void AddRow(Grid grid, UIElement element, int row)
    {
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    private void AddGroup(StackPanel host, RestorePlanPreviewAppGroup group, ref int tabIndex)
    {
        var container = new StackPanel { Margin = new Thickness(0, 1, 0, 1) };
        string appName = ApplicationNameHelper.DisplayName(group.ExecutablePath, group.AppLabel);
        RestorePlanPreviewEntry primary = group.Entries[0];

        var row = new Grid { MinHeight = 50, Background = Brushes.Transparent };
        for (int column = 0; column < 5; column++)
        {
            row.ColumnDefinitions.Add(column == 2
                ? new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                : new ColumnDefinition { Width = GridLength.Auto });
        }

        var check = new CheckBox
        {
            IsThreeState = group.WindowCount > 1,
            IsChecked = primary.IsInitiallyEnabled,
            IsEnabled = IsSelectable(primary),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            TabIndex = tabIndex++,
            Tag = primary.EntryIndex
        };
        AutomationProperties.SetName(check, Loc.Format("Include every {0} window", appName));
        check.Checked += (_, _) => RefreshFromUserSelection();
        check.Unchecked += (_, _) => RefreshFromUserSelection();
        check.Click += (_, _) =>
        {
            if (_updatingSelection || group.WindowCount == 1) return;
            ApplySelection(group, !_groupAllSelected.GetValueOrDefault(group.AppKey));
        };
        _groupControls[group.AppKey] = check;
        // A single-window application selects exactly one entry, so the group box *is* that entry.
        if (group.WindowCount == 1) _entryControls[primary.EntryIndex] = check;
        row.Children.Add(check);

        FrameworkElement badge = ApplicationIconHelper.CreateBadge(
            group.ExecutablePath, appName, AppIconSize);
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.Margin = new Thickness(0, 0, 12, 0);
        AutomationProperties.SetName(badge, $"Application: {appName}");
        Grid.SetColumn(badge, 1);
        row.Children.Add(badge);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock
        {
            Text = appName,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = _primaryText,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Loc.SetSkip(name, true);
        texts.Children.Add(name);

        var subtitle = new TextBlock
        {
            Text = group.WindowCount > 1
                ? Loc.Format("{0} windows", group.WindowCount)
                : Loc.TLabel(primary.WindowTitleLabel),
            FontSize = 12,
            Foreground = _tertiaryText,
            Margin = new Thickness(0, 1, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Loc.SetSkip(subtitle, true);
        texts.Children.Add(subtitle);
        Grid.SetColumn(texts, 2);
        row.Children.Add(texts);

        var status = new TextBlock
        {
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 6, 0),
            TextAlignment = TextAlignment.Right
        };
        _groupStatuses[group.AppKey] = status;
        Grid.SetColumn(status, 3);
        row.Children.Add(status);

        var chevron = new Path
        {
            Data = Geometry.Parse(ChevronDown),
            Stroke = _tertiaryText,
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _groupChevrons[group.AppKey] = chevron;
        var toggle = new Button
        {
            Content = chevron,
            Appearance = ControlAppearance.Transparent,
            Style = (Style)Resources["RestoreQuietButton"],
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            MinWidth = 0,
            Focusable = false,
            Cursor = Cursors.Hand,
            TabIndex = tabIndex++,
            ToolTip = Loc.T("Expand")
        };
        AutomationProperties.SetName(toggle, $"Expand or collapse {appName}");
        toggle.Click += (_, _) => ExpandGroup(group, !_groupExpanded.Contains(group.AppKey));
        Grid.SetColumn(toggle, 4);
        row.Children.Add(toggle);

        AutomationProperties.SetName(container, Loc.TLabel(primary.AccessibilityLabel));
        container.Children.Add(new Border { Style = (Style)Resources["RestoreRow"], Child = row });

        var detailPanel = new StackPanel();
        var detailHost = new Border
        {
            Background = _expandedBackground,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(RowIndent, 5, 12, 6),
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed,
            Child = detailPanel
        };
        _groupDetailPanels[group.AppKey] = detailPanel;
        _groupDetailHosts[group.AppKey] = detailHost;
        container.Children.Add(detailHost);

        host.Children.Add(container);
    }

    // ── Selection ────────────────────────────────────────────────────────────────────────────

    private void ApplySelection(RestorePlanPreviewAppGroup group, bool select)
    {
        _updatingSelection = true;
        try
        {
            foreach (RestorePlanPreviewEntry entry in group.Entries)
            {
                if (_entryControls.TryGetValue(entry.EntryIndex, out CheckBox? control) && control.IsEnabled)
                    control.IsChecked = select;
            }
        }
        finally
        {
            _updatingSelection = false;
        }
        RefreshApprovalState();
    }

    private void ApplySelectionToAll(bool select)
    {
        _updatingSelection = true;
        try
        {
            foreach (CheckBox control in _entryControls.Values.Where(control => control.IsEnabled))
                control.IsChecked = select;
        }
        finally
        {
            _updatingSelection = false;
        }
        RefreshApprovalState();
    }

    // ── Expand / collapse ────────────────────────────────────────────────────────────────────

    private void ExpandGroup(RestorePlanPreviewAppGroup group, bool expand)
    {
        if (expand) _groupExpanded.Add(group.AppKey);
        else _groupExpanded.Remove(group.AppKey);

        if (_groupDetailHosts.TryGetValue(group.AppKey, out FrameworkElement? host))
            host.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        if (_groupChevrons.TryGetValue(group.AppKey, out Path? chevron))
            chevron.Data = Geometry.Parse(expand ? ChevronUp : ChevronDown);
        if (expand) BuildGroupDetails(group);
    }

    private void BuildGroupDetails(RestorePlanPreviewAppGroup group)
    {
        if (!_groupDetailPanels.TryGetValue(group.AppKey, out StackPanel? panel)) return;
        if (!_groupDetailsBuilt.Add(group.AppKey)) return;
        panel.Children.Clear();

        bool multiWindow = group.WindowCount > 1;
        foreach (RestorePlanPreviewEntry entry in group.Entries)
        {
            if (multiWindow) panel.Children.Add(BuildWindowRow(entry));
            // An ambiguous window needs a decision, so its candidates stay in the open area.
            if (!entry.KeepRunning && entry.Outcome == RestorePreviewOutcomeKind.Ambiguous &&
                entry.Candidates.Count > 0)
            {
                panel.Children.Add(BuildCandidateBlock(entry));
            }
        }

        var diagnostics = new StackPanel();
        foreach (RestorePlanPreviewEntry entry in group.Entries)
            AppendEntryDiagnostics(entry, diagnostics, multiWindow);

        panel.Children.Add(BuildDisclosure(Loc.T("View details"), diagnostics));
    }

    private FrameworkElement BuildWindowRow(RestorePlanPreviewEntry entry)
    {
        var row = new Grid { MinHeight = 34 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox
        {
            IsChecked = entry.IsInitiallyEnabled,
            IsEnabled = IsSelectable(entry),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Tag = entry.EntryIndex
        };
        AutomationProperties.SetName(check, Loc.Format("Include {0}", entry.DisplayName));
        AutomationProperties.SetHelpText(check, Loc.T(entry.AccessibilityLabel));
        check.Checked += (_, _) => RefreshFromUserSelection();
        check.Unchecked += (_, _) => RefreshFromUserSelection();
        _entryControls[entry.EntryIndex] = check;
        row.Children.Add(check);

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(entry.WindowTitleLabel)
                ? Loc.TLabel(entry.DisplayName)
                : Loc.TLabel(entry.WindowTitleLabel),
            FontSize = 13,
            Foreground = _primaryText,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 8, 3)
        };
        Loc.SetSkip(title, true);
        Grid.SetColumn(title, 1);
        row.Children.Add(title);

        var status = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right
        };
        _entryStatuses[entry.EntryIndex] = status;
        Grid.SetColumn(status, 2);
        row.Children.Add(status);
        return row;
    }

    // ── Details ──────────────────────────────────────────────────────────────────────────────

    private FrameworkElement BuildCandidateBlock(RestorePlanPreviewEntry entry)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
        panel.Children.Add(Muted(Loc.T("Choose the matching window"), _cautionColor, 11,
            new Thickness(0, 0, 0, 2)));

        foreach (RestorePlanPreviewCandidate candidate in entry.Candidates)
            panel.Children.Add(BuildCandidateRow(entry, candidate));

        var remember = new CheckBox
        {
            Content = Loc.T("Remember the selected window match"),
            IsEnabled = entry.Candidates.Any(candidate =>
                candidate.IsSelected && candidate.CanRememberChoice),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 2)
        };
        AutomationProperties.SetName(remember, Loc.T("Remember the selected window match"));
        _rememberControls[entry.EntryIndex] = remember;
        panel.Children.Add(remember);
        return panel;
    }

    private FrameworkElement BuildCandidateRow(RestorePlanPreviewEntry entry,
        RestorePlanPreviewCandidate candidate)
    {
        bool usedByAnother = _workingPlan.Entries.Any(item =>
            item.EntryIndex != entry.EntryIndex &&
            item.SelectedMatch?.WindowHandle == candidate.WindowHandle);

        var radio = new RadioButton
        {
            GroupName = $"restore-match-{entry.EntryIndex}",
            Content = new TextBlock
            {
                Text = Loc.TLabel(candidate.DisplayTitle),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = _primaryText
            },
            IsChecked = candidate.IsSelected,
            IsEnabled = !usedByAnother,
            Margin = new Thickness(0, 2, 0, 2),
            Tag = candidate.WindowHandle
        };
        AutomationProperties.SetName(
            radio,
            Loc.Format("Select {0}. {1}", candidate.DisplayTitle, Loc.T(candidate.ConfidenceLabel)));
        radio.Checked += (_, _) => SelectCandidate(entry.EntryIndex, candidate);
        _candidateControls[(entry.EntryIndex, candidate.WindowHandle)] = radio;
        return radio;
    }

    /// <summary>
    /// One collapsed block per application keeps every diagnostic that used to be printed inline
    /// without putting it in the way of the default window list.
    /// </summary>
    private void AppendEntryDiagnostics(RestorePlanPreviewEntry entry, StackPanel host, bool multiWindow)
    {
        if (multiWindow)
        {
            host.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(entry.WindowTitleLabel)
                    ? entry.DisplayName
                    : entry.WindowTitleLabel,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _secondaryText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, host.Children.Count == 0 ? 2 : 6, 0, 1)
            });
        }

        string? blocking = entry.BlockingErrors.FirstOrDefault();
        if (blocking is not null)
        {
            host.Children.Add(Muted(Loc.Format("Not restorable: {0}", Loc.T(blocking)),
                _blockedColor, 10.5, new Thickness(0, 1, 0, 1)));
        }
        else if (entry.Outcome == RestorePreviewOutcomeKind.Missing)
        {
            host.Children.Add(Muted(Loc.T("Saved resource is no longer available."),
                _blockedColor, 10.5, new Thickness(0, 1, 0, 1)));
        }

        host.Children.Add(Muted(Loc.Format("Target: {0}", Loc.TLabel(entry.TargetLabel)),
            _tertiaryText, 10.5, new Thickness(0, 1, 0, 1)));
        host.Children.Add(Muted(Loc.Format("Policy: {0}", Loc.T(entry.PolicyLabel)),
            _tertiaryText, 10.5, new Thickness(0, 0, 0, 1)));

        if (entry.KeepRunning)
        {
            host.Children.Add(Muted(
                Loc.T("Kept as it is; nothing will be launched, moved or reopened."),
                _runningColor, 10.5, new Thickness(0, 0, 0, 1)));
        }

        foreach (RestorePlanPreviewAction action in entry.KeepRunning
                     ? Array.Empty<RestorePlanPreviewAction>()
                     : entry.Actions)
        {
            host.Children.Add(Muted(
                Loc.Format("• {0} — {1}", Loc.T(action.Label), Loc.T(action.Explanation)),
                _tertiaryText, 10.5, new Thickness(0, 0, 0, 1)));
        }

        foreach (string warning in entry.Warnings)
        {
            host.Children.Add(Muted(Loc.Format("Warning: {0}", Loc.T(warning)),
                _cautionColor, 10.5, new Thickness(0, 0, 0, 1)));
        }

        foreach (RestorePlanPreviewCandidate candidate in entry.Candidates)
        {
            host.Children.Add(Muted(
                Loc.Format("{0} · {1} · {2} · {3}",
                    candidate.DisplayTitle,
                    Loc.TLabel(candidate.IdentityLabel),
                    Loc.T(candidate.ConfidenceLabel),
                    Loc.TLabel(candidate.ScoreLabel)),
                _tertiaryText, 10, new Thickness(3, 2, 0, 0)));
            foreach (string reason in candidate.Reasons.Take(3))
            {
                host.Children.Add(Muted(Loc.Format("Evidence: {0}", Loc.T(reason)),
                    _tertiaryText, 10, new Thickness(13, 0, 0, 0)));
            }
        }
    }

    // ── Approval ─────────────────────────────────────────────────────────────────────────────

    private void SelectCandidate(int entryIndex, RestorePlanPreviewCandidate candidate)
    {
        try
        {
            _workingPlan = RestorePlanner.ResolveAmbiguousMatch(
                _workingPlan, entryIndex, candidate.WindowHandle);
            if (_rememberControls.TryGetValue(entryIndex, out CheckBox? remember))
            {
                remember.IsEnabled = candidate.CanRememberChoice;
                if (!candidate.CanRememberChoice) remember.IsChecked = false;
            }
            RefreshApprovalState();
        }
        catch (InvalidOperationException)
        {
            if (_candidateControls.TryGetValue((entryIndex, candidate.WindowHandle), out RadioButton? radio))
                radio.IsChecked = false;
        }
    }

    private void Approve()
    {
        if (_isPreviewOnly)
        {
            ApprovedPlan = null;
            DialogResult = false;
            return;
        }

        ApprovedPlan = DeriveCurrentPlan();
        if (!ApprovedPlan.CanExecute ||
            (ApprovedPlan.Actions.Count == 0 && !ApprovedPlan.Entries.Any(entry => entry.KeepRunning)))
        {
            return;
        }

        ApprovedMatchHints = _rememberControls
            .Where(item => item.Value.IsChecked == true)
            .Select(item => ApprovedPlan.Entries.Single(entry => entry.EntryIndex == item.Key))
            .Where(entry => entry.SelectedMatch is { CanRememberChoice: true, IdentityHint: not null })
            .Select(entry => new WindowMatchHint
            {
                WorkspaceId = ApprovedPlan.WorkspaceId,
                EntryId = entry.EntryId,
                Identity = entry.SelectedMatch!.IdentityHint!
            })
            .ToArray();
        DialogResult = true;
    }

    private void RefreshFromUserSelection()
    {
        if (_updatingSelection) return;
        RefreshApprovalState();
    }

    private void RefreshApprovalState()
    {
        if (_updatingSelection || _summaryText is null) return;

        RestorePlan approved = DeriveCurrentPlan();
        RestorePlanPreview projection = RestorePlanPreviewBuilder.Build(approved);

        _updatingSelection = true;
        try
        {
            foreach (RestorePlanPreviewAppGroup group in _groups)
            {
                int selectable = 0;
                int selected = 0;
                foreach (RestorePlanPreviewEntry entry in group.Entries)
                {
                    if (!_entryControls.TryGetValue(entry.EntryIndex, out CheckBox? control)) continue;
                    if (control.IsEnabled) selectable++;
                    if (control.IsEnabled && control.IsChecked == true) selected++;
                }

                bool all = selectable > 0 && selected == selectable;
                _groupAllSelected[group.AppKey] = all;
                if (_groupControls.TryGetValue(group.AppKey, out CheckBox? groupCheck))
                {
                    // The single-window group box is also the entry box; never overwrite it here.
                    if (group.WindowCount > 1)
                        groupCheck.IsChecked = selected == 0 ? false : all ? true : null;
                    groupCheck.IsEnabled = selectable > 0;
                }

                UpdateGroupStatus(group, selected);
            }

            int totalSelectable = 0;
            int totalSelected = 0;
            foreach (CheckBox control in _entryControls.Values)
            {
                if (!control.IsEnabled) continue;
                totalSelectable++;
                if (control.IsChecked == true) totalSelected++;
            }
            _allWindowsSelected = totalSelectable > 0 && totalSelected == totalSelectable;
            _selectAll.IsEnabled = totalSelectable > 0 && !_isPreviewOnly;
            _selectAll.IsChecked = totalSelected == 0 ? false : _allWindowsSelected ? true : null;
        }
        finally
        {
            _updatingSelection = false;
        }

        foreach (RestorePlanPreviewEntry entry in projection.Entries)
            UpdateEntryStatus(entry);

        foreach (((int entryIndex, long handle), RadioButton radio) in _candidateControls)
        {
            RestorePlanEntry? planEntry = _workingPlan.Entries.SingleOrDefault(item =>
                item.EntryIndex == entryIndex);
            radio.IsChecked = planEntry?.SelectedMatch?.WindowHandle == handle;
            radio.IsEnabled = !_workingPlan.Entries.Any(other =>
                other.EntryIndex != entryIndex &&
                other.SelectedMatch?.WindowHandle == handle &&
                !_workingPlan.DisabledEntryIndexes.Contains(other.EntryIndex));
        }

        int restorable = 0;
        int running = 0;
        int blocked = 0;
        foreach (RestorePlanPreviewEntry entry in projection.Entries)
        {
            if (!_entryControls.TryGetValue(entry.EntryIndex, out CheckBox? control) ||
                !control.IsEnabled || control.IsChecked != true) continue;
            if (entry.KeepRunning) running++;
            else if (entry.IsBlocking) blocked++;
            else restorable++;
        }
        int selectedCount = restorable + running + blocked;

        _summaryText.Text = Loc.Format(
            "Selected {0} windows · restoring {1} · skipping {2} already running",
            selectedCount, restorable, running);
        _primaryButton.Content = _isPreviewOnly
            ? Loc.T("Close preview")
            : Loc.Format(
                _isWorkspaceSwitch ? "Switch {0} windows" : "Restore {0} windows", restorable);
        _primaryButton.IsEnabled = _isPreviewOnly ||
            (approved.CanExecute && selectedCount > 0 && (restorable > 0 || running > 0));

        RenderNotices(projection, blocked);
    }

    private void UpdateEntryStatus(RestorePlanPreviewEntry entry)
    {
        if (!_entryStatuses.TryGetValue(entry.EntryIndex, out TextBlock? status)) return;
        bool selected = _entryControls.TryGetValue(entry.EntryIndex, out CheckBox? control) &&
            control.IsEnabled && control.IsChecked == true;
        (string text, Brush brush) = DescribeEntry(entry, selected);
        status.Text = text;
        status.Foreground = brush;
    }

    private (string Text, Brush Brush) DescribeEntry(RestorePlanPreviewEntry entry, bool selected)
    {
        if (entry.KeepRunning) return ("● " + Loc.T("Already running · skipped"), _runningColor);
        if (entry.IsBlocking) return (Loc.T("Not restorable"), _blockedColor);
        if (entry.Outcome == RestorePreviewOutcomeKind.Ambiguous)
            return (Loc.T("Choose the matching window"), _cautionColor);
        if (entry.Outcome is RestorePreviewOutcomeKind.Skipped or RestorePreviewOutcomeKind.Cancelled)
            return (Loc.T("Skipped"), _secondaryText);
        return selected
            ? (Loc.T("To restore"), _accent)
            : (Loc.T("Cleared"), _secondaryText);
    }

    private void UpdateGroupStatus(RestorePlanPreviewAppGroup group, int selectedCount)
    {
        if (!_groupStatuses.TryGetValue(group.AppKey, out TextBlock? status)) return;

        List<RestorePlanPreviewEntry> selected = group.Entries
            .Where(entry => _entryControls.TryGetValue(entry.EntryIndex, out CheckBox? control) &&
                control.IsEnabled && control.IsChecked == true)
            .ToList();
        int running = selected.Count(entry => entry.KeepRunning);
        int blocked = selected.Count(entry => !entry.KeepRunning && entry.IsBlocking);
        int restorable = selected.Count(entry => !entry.KeepRunning && !entry.IsBlocking);

        (string text, Brush brush) = selectedCount == 0
            ? (Loc.T("Not selected"), _secondaryText)
            : blocked > 0 && restorable == 0
                ? (Loc.T("Not restorable"), _blockedColor)
                : restorable == 0
                    ? ("● " + Loc.T("Already running · skipped"), _runningColor)
                    : blocked > 0
                        ? (Loc.Format("Restore {0} · {1} not restorable", restorable, blocked + running), _accent)
                        : group.WindowCount == 1
                            ? (Loc.T("To restore"), _accent)
                            : (Loc.Format("Restore {0} · skip {1}", restorable, running), _accent);
        status.Text = text;
        status.Foreground = brush;
    }

    private void RenderNotices(RestorePlanPreview projection, int blocked)
    {
        _noticePanel.Children.Clear();
        if (_isPreviewOnly)
        {
            _noticePanel.Children.Add(NoticeLine(
                Loc.T("Preview only: no windows or browser sessions will be changed."),
                _secondaryText));
            return;
        }

        if (blocked > 0)
        {
            _noticePanel.Children.Add(NoticeLine(
                Loc.Format("{0} selected windows cannot be restored. Clear them to continue.", blocked),
                _blockedColor));
        }

        bool closesWindows = projection.GlobalActions.Any(action =>
            action.Kind == RestorePreviewActionKind.Close);
        bool minimizes = projection.GlobalActions.Any(action =>
            action.Kind == RestorePreviewActionKind.Minimize);
        if (_isWorkspaceSwitch || closesWindows)
        {
            _noticePanel.Children.Add(NoticeLine(
                Loc.T("Unrelated open windows may ask to save before they close; SuKe never force-closes them."),
                _cautionColor));
        }
        else if (minimizes)
        {
            _noticePanel.Children.Add(NoticeLine(
                Loc.T("Other open windows will be minimized. No window will be closed."),
                _cautionColor));
        }
    }

    // ── Plan derivation ──────────────────────────────────────────────────────────────────────

    private RestorePlan DeriveCurrentPlan()
    {
        int[] disabled = _entryControls
            .Where(item => item.Value.IsEnabled && item.Value.IsChecked != true)
            .Select(item => item.Key)
            .ToArray();
        return PrepareRestorePlan(RestorePlanner.DeriveApprovedPlan(_workingPlan, disabled));
    }

    private RestorePlan PrepareRestorePlan(RestorePlan plan) => _isWorkspaceSwitch || _isPreviewOnly
        ? plan : RestoreApprovalProjector.PreserveRunningEntries(plan);

    private static bool IsSelectable(RestorePlanPreviewEntry entry) =>
        entry.KeepRunning || entry.Outcome is not (RestorePreviewOutcomeKind.Skipped or
            RestorePreviewOutcomeKind.Cancelled);

    /// <summary>
    /// Inline disclosure used instead of an Expander so diagnostics stay quiet: no card border,
    /// no extra chrome, and the content is only built into the visual tree once.
    /// </summary>
    private FrameworkElement BuildDisclosure(string header, UIElement content)
    {
        var body = new StackPanel
        {
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 2, 0, 0)
        };
        body.Children.Add(content);

        var chevron = new Path
        {
            Data = Geometry.Parse(ChevronDown),
            Stroke = _tertiaryText,
            StrokeThickness = 1.2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(chevron);
        row.Children.Add(new TextBlock
        {
            Text = header,
            FontSize = 12,
            Foreground = _tertiaryText,
            VerticalAlignment = VerticalAlignment.Center
        });

        var toggle = new Button
        {
            Content = row,
            Appearance = ControlAppearance.Transparent,
            Style = (Style)Resources["RestoreQuietButton"],
            Padding = new Thickness(0),
            MinWidth = 0,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Left,
            Focusable = true
        };
        AutomationProperties.SetName(toggle, header);
        toggle.Click += (_, _) =>
        {
            bool expand = body.Visibility != Visibility.Visible;
            body.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
            chevron.Data = Geometry.Parse(expand ? ChevronUp : ChevronDown);
        };

        var host = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
        host.Children.Add(toggle);
        host.Children.Add(body);
        return host;
    }

    // ── Small helpers ────────────────────────────────────────────────────────────────────────

    private void UseDialogAccent()
    {
        bool dark = PrefersDarkPalette();
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/SuKe;component/UI/RestorePlanPreviewStyles.xaml"),
        });
        Resources["RestoreAccent"] = _accent;
        Resources["RestoreAccentHover"] = Solid(dark ? "#A78BF9" : "#8061E9");
        Resources["RestoreAccentPressed"] = Solid(dark ? "#8362DC" : "#5D3BCB");
        Resources["RestoreSurface"] = Solid(dark ? "#27262F" : "#FFFFFF");
        Resources["RestoreHover"] = Solid(dark ? "#32303D" : "#F3F1FA");
        Resources["RestorePressed"] = Solid(dark ? "#393544" : "#ECE8F7");
        Resources["RestoreControlBorder"] = Solid(dark ? "#514C60" : "#D9D6E3");
        Resources["RestorePrimaryText"] = _primaryText;
        Resources["RestoreSecondaryText"] = _secondaryText;
        Resources[typeof(CheckBox)] = Resources["RestoreCheckBox"];
        Resources["RadioButtonCheckBackgroundFillChecked"] = _accent;
        Resources["RadioButtonCheckGlyphForeground"] = Brushes.White;
    }

    private TextBlock Muted(string text, Brush brush, double size, Thickness margin) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = brush,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin
    };

    private FrameworkElement NoticeLine(string text, Brush brush)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = brush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        });
        row.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap
        });
        return new Border { Margin = new Thickness(0, 1, 0, 1), Child = row };
    }

    /// <summary>
    /// Decides light or dark from the theme resources actually in scope rather than from the
    /// persisted setting, so the dialog keeps working when a host merges a window-level theme and
    /// when the application follows the system theme.
    /// </summary>
    private static bool PrefersDarkPalette()
    {
        if (Resource("TextFillColorPrimaryBrush", Brushes.Black) is SolidColorBrush text)
        {
            Color color = text.Color;
            return color.R > 0x80 && color.G > 0x80 && color.B > 0x80;
        }
        return ThemeService.IsDark;
    }

    private static Brush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static Brush Resource(string key, Brush fallback) =>
        System.Windows.Application.Current?.Resources.Contains(key) == true
            ? System.Windows.Application.Current.Resources[key] as Brush ?? fallback
            : fallback;
}
