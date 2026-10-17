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

namespace SuKe.UI;

/// <summary>
/// Displays an immutable restore plan and derives an approved copy when entries are disabled.
/// Matching and resource discovery never run in this window.
/// </summary>
internal sealed class RestorePlanPreviewDialog : FluentWindow
{
    private RestorePlan _workingPlan;
    private readonly Dictionary<int, System.Windows.Controls.CheckBox> _entryControls = new();
    private readonly Wpf.Ui.Controls.Button _restoreButton;
    private readonly System.Windows.Controls.TextBlock _selectionSummary;
    private readonly Border _blockingNotice;
    private readonly StackPanel _globalActionsPanel;
    private readonly Dictionary<int, StackPanel> _entryActionPanels = new();
    private readonly Dictionary<int, System.Windows.Controls.TextBlock> _entryOutcomeLabels = new();
    private readonly Dictionary<int, Border> _entryCards = new();
    private readonly Dictionary<(int EntryIndex, long WindowHandle), System.Windows.Controls.RadioButton> _candidateControls = new();
    private readonly Dictionary<int, System.Windows.Controls.CheckBox> _rememberControls = new();
    private readonly bool _isWorkspaceSwitch;
    private readonly bool _isPreviewOnly;
    private readonly System.Windows.Controls.CheckBox _selectAll;
    private bool _updatingSelection;

    internal RestorePlan? ApprovedPlan { get; private set; }
    internal IReadOnlyList<WindowMatchHint> ApprovedMatchHints { get; private set; } =
        Array.Empty<WindowMatchHint>();

    internal RestorePlanPreviewDialog(RestorePlan previewPlan, bool isWorkspaceSwitch = false)
    {
        _workingPlan = previewPlan ?? throw new ArgumentNullException(nameof(previewPlan));
        _isWorkspaceSwitch = isWorkspaceSwitch;
        _isPreviewOnly = previewPlan.Mode == RestoreModeKind.PreviewOnly;
        RestorePlanPreview preview = RestorePlanPreviewBuilder.Build(_workingPlan);

        string operation = Loc.T(_isPreviewOnly ? "preview" : isWorkspaceSwitch ? "switch" : "restore");
        string variantLabel = string.IsNullOrWhiteSpace(previewPlan.LayoutVariantName)
            ? ""
            : $" ({previewPlan.LayoutVariantName})";
        Title = Loc.Format("Review {0}: {1}{2}", operation, preview.WorkspaceName, variantLabel);
        Width = 780;
        Height = 720;
        MinWidth = 640;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;
        ShowInTaskbar = false;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        KeyboardNavigation.SetTabNavigation(root, KeyboardNavigationMode.Continue);

        var titleBar = new TitleBar
        {
            Title = Loc.Format("Review {0}: {1}{2}", operation, preview.WorkspaceName, variantLabel),
            ShowMinimize = false,
            ShowMaximize = false
        };
        AutomationProperties.SetName(titleBar, Loc.Format("{0} plan preview for {1}", operation, preview.WorkspaceName));
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        var body = new Grid { Margin = new Thickness(22, 14, 22, 20) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var heading = new System.Windows.Controls.TextBlock
        {
            Text = _isPreviewOnly
                ? "Preview this workspace without making changes"
                : isWorkspaceSwitch
                ? "Review the destination workspace"
                : "Review what SuKe will change",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("TextFillColorPrimaryBrush", Brushes.Black)
        };
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
        body.Children.Add(heading);

        var introduction = new System.Windows.Controls.TextBlock
        {
            Text = isWorkspaceSwitch
                ? Loc.Format(
                    "Mode: {0} · {1} saved entries. Matching destination windows stay open; unrelated windows will be asked to close after approval. Clear an entry to exclude it.",
                    Loc.T(ModeLabel(preview.Mode)),
                    preview.Entries.Count)
                : Loc.Format(
                    "Mode: {0} · {1} saved entries. Clear an entry to remove its actions from the approved plan.",
                    Loc.T(ModeLabel(preview.Mode)),
                    preview.Entries.Count),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 12),
            Foreground = Brush("TextFillColorSecondaryBrush", Brushes.DimGray)
        };
        Grid.SetRow(introduction, 1);
        body.Children.Add(introduction);

        var notices = new StackPanel();
        Border destructiveNotice = Notice(
            _isPreviewOnly
                ? "Preview only: no applications, windows, or browser sessions will be changed."
                : isWorkspaceSwitch
                ? "Switching can show save prompts for unrelated open windows; SuKe never force-closes them."
                : Loc.T(preview.DestructiveSummary),
            !_isPreviewOnly && (isWorkspaceSwitch || preview.DestructiveActionCount > 0)
                ? Brush("SystemFillColorCautionBrush", Brushes.DarkOrange)
                : Brush("SystemFillColorSuccessBrush", Brushes.SeaGreen),
            !_isPreviewOnly && (isWorkspaceSwitch || preview.DestructiveActionCount > 0)
                ? Brush("SystemFillColorCautionBackgroundBrush", Brushes.LemonChiffon)
                : Brush("SystemFillColorSuccessBackgroundBrush", Brushes.Honeydew));
        AutomationProperties.SetName(
            destructiveNotice,
            Loc.Format("Destructive action summary. {0}", Loc.T(preview.DestructiveSummary)));
        notices.Children.Add(destructiveNotice);

        _blockingNotice = Notice(
            "Blocking errors must be resolved or their entries disabled before restore.",
            Brush("SystemFillColorCriticalBrush", Brushes.Firebrick),
            Brush("SystemFillColorCriticalBackgroundBrush", Brushes.MistyRose));
        _blockingNotice.Margin = new Thickness(0, 8, 0, 0);
        _blockingNotice.Visibility = preview.BlockingErrorCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        AutomationProperties.SetName(
            _blockingNotice,
            "Blocking errors are present. Disable affected entries before restoring.");
        notices.Children.Add(_blockingNotice);
        Grid.SetRow(notices, 2);
        body.Children.Add(notices);

        _selectAll = new System.Windows.Controls.CheckBox
        {
            Content = Loc.T("Select all"), Margin = new Thickness(0, 10, 0, 2),
        };
        AutomationProperties.SetName(_selectAll, "Select all restore entries");
        _selectAll.Click += (_, _) =>
        {
            var selectable = _entryControls.Values.Where(control => control.IsEnabled).ToArray();
            bool select = selectable.Any(control => control.IsChecked != true);
            _updatingSelection = true;
            try
            {
                foreach (var control in selectable) control.IsChecked = select;
            }
            finally { _updatingSelection = false; }
            RefreshApprovalState();
        };
        notices.Children.Add(_selectAll);

        var entryPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 10) };
        int tabIndex = 0;
        foreach (RestorePlanPreviewEntry entry in preview.Entries)
        {
            Border card = BuildEntryCard(entry, tabIndex++);
            entryPanel.Children.Add(card);
        }
        var scroll = new ScrollViewer
        {
            Content = entryPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false
        };
        AutomationProperties.SetName(scroll, "Restore plan entries");
        Grid.SetRow(scroll, 3);
        body.Children.Add(scroll);

        _globalActionsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(_globalActionsPanel, 4);
        body.Children.Add(_globalActionsPanel);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _selectionSummary = new System.Windows.Controls.TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("TextFillColorSecondaryBrush", Brushes.DimGray)
        };
        AutomationProperties.SetLiveSetting(_selectionSummary, AutomationLiveSetting.Polite);
        footer.Children.Add(_selectionSummary);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        var cancelButton = new Wpf.Ui.Controls.Button
        {
            Content = "Cancel",
            IsCancel = true,
            Margin = new Thickness(0, 0, 8, 0),
            TabIndex = tabIndex++,
            Visibility = _isPreviewOnly ? Visibility.Collapsed : Visibility.Visible
        };
        AutomationProperties.SetName(cancelButton, "Cancel restore");
        cancelButton.Click += (_, _) =>
        {
            ApprovedPlan = null;
            DialogResult = false;
        };

        _restoreButton = new Wpf.Ui.Controls.Button
        {
            Content = _isPreviewOnly
                ? "Close preview"
                : isWorkspaceSwitch ? "Switch to selected" : "Restore selected",
            Appearance = ControlAppearance.Primary,
            IsDefault = true,
            TabIndex = tabIndex
        };
        AutomationProperties.SetName(
            _restoreButton,
            isWorkspaceSwitch
                ? "Approve plan and switch to selected entries"
                : "Approve plan and restore selected entries");
        AutomationProperties.SetHelpText(
            _restoreButton,
            isWorkspaceSwitch
                ? "Preserves approved destination windows, closes unrelated windows safely, then executes this exact plan."
                : "Executes the exact actions shown in this preview after stale-plan validation.");
        _restoreButton.Click += (_, _) =>
        {
            if (_isPreviewOnly)
            {
                ApprovedPlan = null;
                DialogResult = false;
                return;
            }
            ApprovedPlan = DeriveCurrentPlan();
            if (!ApprovedPlan.CanExecute || ApprovedPlan.Actions.Count == 0) return;
            ApprovedMatchHints = _rememberControls
                .Where(item => item.Value.IsChecked == true)
                .Select(item => ApprovedPlan.Entries.Single(entry =>
                    entry.EntryIndex == item.Key))
                .Where(entry => entry.SelectedMatch is
                    { CanRememberChoice: true, IdentityHint: not null })
                .Select(entry => new WindowMatchHint
                {
                    WorkspaceId = ApprovedPlan.WorkspaceId,
                    EntryId = entry.EntryId,
                    Identity = entry.SelectedMatch!.IdentityHint!
                })
                .ToArray();
            DialogResult = true;
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(_restoreButton);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 5);
        body.Children.Add(footer);

        Content = root;
        RefreshApprovalState();
        Loaded += (_, _) =>
            _entryControls.Values.FirstOrDefault(control => control.IsEnabled)?.Focus();
    }

    private Border BuildEntryCard(RestorePlanPreviewEntry entry, int tabIndex)
    {
        Brush borderBrush = entry.IsBlocking
            ? Brush("SystemFillColorCriticalBrush", Brushes.Firebrick)
            : entry.Outcome == RestorePreviewOutcomeKind.Ambiguous
                ? Brush("SystemFillColorCautionBrush", Brushes.DarkOrange)
                : Brush("CardStrokeColorDefaultBrush", Brushes.LightGray);
        var card = new Border
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(entry.IsBlocking ? 2 : 1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Background = Brush("CardBackgroundFillColorDefaultBrush", Brushes.White)
        };
        _entryCards[entry.EntryIndex] = card;
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var checkBox = new System.Windows.Controls.CheckBox
        {
            IsChecked = entry.IsInitiallyEnabled,
            IsEnabled = entry.Outcome is not (RestorePreviewOutcomeKind.Skipped or
                RestorePreviewOutcomeKind.Cancelled),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 10, 0),
            TabIndex = tabIndex,
            Tag = entry.EntryIndex
        };
        AutomationProperties.SetName(checkBox, Loc.Format("Include {0}", entry.DisplayName));
        AutomationProperties.SetHelpText(checkBox, Loc.T(entry.AccessibilityLabel));
        checkBox.Checked += (_, _) => RefreshApprovalState();
        checkBox.Unchecked += (_, _) => RefreshApprovalState();
        _entryControls[entry.EntryIndex] = checkBox;
        row.Children.Add(checkBox);

        var details = new StackPanel();
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new System.Windows.Controls.TextBlock
        {
            Text = entry.DisplayName,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("TextFillColorPrimaryBrush", Brushes.Black)
        };
        header.Children.Add(name);
        var outcome = new System.Windows.Controls.TextBlock
        {
            Text = Loc.T(entry.OutcomeLabel),
            FontWeight = FontWeights.SemiBold,
            Foreground = OutcomeBrush(entry.Outcome),
            Margin = new Thickness(12, 0, 0, 0)
        };
        AutomationProperties.SetName(outcome, Loc.Format("Outcome: {0}", Loc.T(entry.OutcomeLabel)));
        _entryOutcomeLabels[entry.EntryIndex] = outcome;
        Grid.SetColumn(outcome, 1);
        header.Children.Add(outcome);
        details.Children.Add(header);

        details.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = Loc.TLabel(entry.TargetLabel),
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 4),
            Foreground = Brush("TextFillColorTertiaryBrush", Brushes.Gray)
        });
        details.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = Loc.Format("Policy: {0}", Loc.T(entry.PolicyLabel)),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = Brush("TextFillColorTertiaryBrush", Brushes.Gray)
        });
        if (entry.Candidates.Count > 0)
            details.Children.Add(BuildCandidatePanel(entry));
        var actionPanel = new StackPanel();
        _entryActionPanels[entry.EntryIndex] = actionPanel;
        details.Children.Add(actionPanel);

        Grid.SetColumn(details, 1);
        row.Children.Add(details);
        card.Child = row;
        AutomationProperties.SetName(card, Loc.T(entry.AccessibilityLabel));
        return card;
    }

    private FrameworkElement BuildCandidatePanel(RestorePlanPreviewEntry entry)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 4, 0, 6)
        };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = entry.Outcome == RestorePreviewOutcomeKind.Ambiguous
                ? "Choose the window that belongs to this entry:"
                : "Selected match evidence:",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("TextFillColorSecondaryBrush", Brushes.DimGray),
            Margin = new Thickness(0, 0, 0, 3)
        });

        foreach (RestorePlanPreviewCandidate candidate in entry.Candidates)
        {
            FrameworkElement content = CandidateContent(candidate);
            if (entry.Outcome != RestorePreviewOutcomeKind.Ambiguous)
            {
                panel.Children.Add(content);
                continue;
            }

            bool usedByAnother = _workingPlan.Entries.Any(item =>
                item.EntryIndex != entry.EntryIndex &&
                item.SelectedMatch?.WindowHandle == candidate.WindowHandle);
            var radio = new System.Windows.Controls.RadioButton
            {
                GroupName = $"restore-match-{entry.EntryIndex}",
                Content = content,
                IsChecked = candidate.IsSelected,
                IsEnabled = !usedByAnother,
                Margin = new Thickness(0, 2, 0, 3),
                Tag = candidate.WindowHandle
            };
            AutomationProperties.SetName(
                radio,
                Loc.Format(
                    "Select {0}. {1}. {2}. {3}",
                    candidate.DisplayTitle,
                    Loc.TLabel(candidate.IdentityLabel),
                    Loc.T(candidate.ConfidenceLabel),
                    string.Join(" ", candidate.Reasons.Select(Loc.T))));
            radio.Checked += (_, _) => SelectCandidate(entry.EntryIndex, candidate);
            _candidateControls[(entry.EntryIndex, candidate.WindowHandle)] = radio;
            panel.Children.Add(radio);
        }

        if (entry.Outcome == RestorePreviewOutcomeKind.Ambiguous)
        {
            bool anyPersistable = entry.Candidates.Any(candidate => candidate.CanRememberChoice);
            var remember = new System.Windows.Controls.CheckBox
            {
                Content = "Remember this choice for this workspace entry",
                IsEnabled = false,
                Margin = new Thickness(23, 2, 0, 0),
                FontSize = 11,
                ToolTip = anyPersistable
                    ? "Uses stable workspace/entry IDs and a composite app identity; HWND and PID are never saved."
                    : "These candidates do not have distinct stable composite identities, so this choice cannot be remembered safely."
            };
            AutomationProperties.SetName(remember, Loc.T("Remember the selected window match"));
            _rememberControls[entry.EntryIndex] = remember;
            panel.Children.Add(remember);
        }
        return panel;
    }

    private static FrameworkElement CandidateContent(RestorePlanPreviewCandidate candidate)
    {
        var content = new StackPanel();
        // The window title is user data and must never be translated by the catalog.
        var candidateTitle = new System.Windows.Controls.TextBlock
        {
            Text = candidate.DisplayTitle,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("TextFillColorPrimaryBrush", Brushes.Black)
        };
        Loc.SetSkip(candidateTitle, true);
        content.Children.Add(candidateTitle);
        content.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"{Loc.TLabel(candidate.IdentityLabel)} · {Loc.T(candidate.ConfidenceLabel)} · {Loc.TLabel(candidate.ScoreLabel)}",
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("TextFillColorTertiaryBrush", Brushes.Gray)
        });
        foreach (string reason in candidate.Reasons.Take(3))
        {
            content.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = Loc.Format("Evidence: {0}", Loc.T(reason)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("TextFillColorTertiaryBrush", Brushes.Gray)
            });
        }
        return content;
    }

    private void SelectCandidate(int entryIndex, RestorePlanPreviewCandidate candidate)
    {
        try
        {
            _workingPlan = RestorePlanner.ResolveAmbiguousMatch(
                _workingPlan,
                entryIndex,
                candidate.WindowHandle);
            if (_rememberControls.TryGetValue(entryIndex, out var remember))
            {
                remember.IsEnabled = candidate.CanRememberChoice;
                if (!candidate.CanRememberChoice)
                    remember.IsChecked = false;
            }
            RefreshApprovalState();
        }
        catch (InvalidOperationException)
        {
            if (_candidateControls.TryGetValue((entryIndex, candidate.WindowHandle), out var radio))
                radio.IsChecked = false;
        }
    }

    private void RefreshApprovalState()
    {
        if (_updatingSelection || _restoreButton is null || _selectionSummary is null || _blockingNotice is null) return;
        var selectable = _entryControls.Values.Where(control => control.IsEnabled).ToArray();
        int checkedCount = selectable.Count(control => control.IsChecked == true);
        _selectAll.IsEnabled = selectable.Length > 0 && !_isPreviewOnly;
        _selectAll.IsChecked = checkedCount == 0 ? false : checkedCount == selectable.Length ? true : null;
        _selectAll.Content = Loc.T(checkedCount == selectable.Length && selectable.Length > 0 ? "Deselect all" : "Select all");
        RestorePlan approved = DeriveCurrentPlan();
        int selected = approved.Entries.Count(entry =>
            entry.Outcome is not (RestorePlanEntryOutcome.Excluded or RestorePlanEntryOutcome.Cancelled));
        int blockers = approved.BlockingErrors.Count;
        _selectionSummary.Text = _isPreviewOnly
            ? Loc.Format("{0} entries · preview only", approved.Entries.Count)
            : blockers > 0
            ? Loc.Format("{0} selected · {1} blocking error(s)", selected, blockers)
            : Loc.Format("{0} selected · ready to {1}", selected, Loc.T(_isWorkspaceSwitch ? "switch" : "restore"));
        _blockingNotice.Visibility = blockers > 0 ? Visibility.Visible : Visibility.Collapsed;
        _restoreButton.IsEnabled = _isPreviewOnly ||
            (approved.CanExecute && approved.Actions.Count > 0);
        foreach (((int entryIndex, long handle), System.Windows.Controls.RadioButton radio) in _candidateControls)
        {
            RestorePlanEntry? entry = _workingPlan.Entries.SingleOrDefault(item =>
                item.EntryIndex == entryIndex);
            radio.IsChecked = entry?.SelectedMatch?.WindowHandle == handle;
            radio.IsEnabled = !_workingPlan.Entries.Any(entry =>
                entry.EntryIndex != entryIndex &&
                entry.SelectedMatch?.WindowHandle == handle &&
                !_workingPlan.DisabledEntryIndexes.Contains(entry.EntryIndex));
        }
        RenderEntryActions(approved);
        RenderGlobalActions(approved);
    }

    private void RenderEntryActions(RestorePlan approved)
    {
        RestorePlanPreview projection = RestorePlanPreviewBuilder.Build(approved);
        foreach (RestorePlanPreviewEntry entry in projection.Entries)
        {
            if (!_entryActionPanels.TryGetValue(entry.EntryIndex, out StackPanel? panel))
                continue;
            if (_entryOutcomeLabels.TryGetValue(
                    entry.EntryIndex,
                    out System.Windows.Controls.TextBlock? outcome))
            {
                outcome.Text = Loc.T(entry.OutcomeLabel);
                outcome.Foreground = OutcomeBrush(entry.Outcome);
                AutomationProperties.SetName(outcome, Loc.Format("Outcome: {0}", Loc.T(entry.OutcomeLabel)));
            }
            if (_entryCards.TryGetValue(entry.EntryIndex, out Border? card))
            {
                card.BorderBrush = entry.IsBlocking
                    ? Brush("SystemFillColorCriticalBrush", Brushes.Firebrick)
                    : entry.Outcome == RestorePreviewOutcomeKind.Ambiguous
                        ? Brush("SystemFillColorCautionBrush", Brushes.DarkOrange)
                        : Brush("CardStrokeColorDefaultBrush", Brushes.LightGray);
                card.BorderThickness = new Thickness(entry.IsBlocking ? 2 : 1);
            }
            panel.Children.Clear();
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = Loc.T(entry.Explanation),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 3),
                Foreground = Brush("TextFillColorTertiaryBrush", Brushes.Gray)
            });
            foreach (RestorePlanPreviewAction action in entry.Actions)
            {
                var actionText = new System.Windows.Controls.TextBlock
                {
                    Text = Loc.Format("• {0} — {1}", Loc.T(action.Label), Loc.T(action.Explanation)),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = action.IsDestructive
                        ? Brush("SystemFillColorCautionBrush", Brushes.DarkOrange)
                        : Brush("TextFillColorSecondaryBrush", Brushes.DimGray),
                    FontWeight = action.IsDestructive ? FontWeights.SemiBold : FontWeights.Normal
                };
                AutomationProperties.SetName(
                    actionText,
                    Loc.Format("Planned action. {0}. {1}", Loc.T(action.Label), Loc.T(action.Explanation)));
                panel.Children.Add(actionText);
            }
            foreach (string warning in entry.Warnings)
                panel.Children.Add(IssueText(Loc.Format("Warning: {0}", Loc.T(warning)), isBlocking: false));
            foreach (string error in entry.BlockingErrors)
                panel.Children.Add(IssueText(Loc.Format("Blocking: {0}", Loc.T(error)), isBlocking: true));
        }
    }

    private void RenderGlobalActions(RestorePlan approved)
    {
        _globalActionsPanel.Children.Clear();
        foreach (RestorePlanPreviewAction action in
                 RestorePlanPreviewBuilder.Build(approved).GlobalActions)
        {
            var text = new System.Windows.Controls.TextBlock
            {
                Text = Loc.Format("Workspace action · {0}", Loc.T(action.Label)),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = action.IsDestructive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = action.IsDestructive
                    ? Brush("SystemFillColorCautionBrush", Brushes.DarkOrange)
                    : Brush("TextFillColorSecondaryBrush", Brushes.DimGray)
            };
            AutomationProperties.SetName(
                text,
                Loc.Format("Workspace action. {0}. {1}", Loc.T(action.Label), Loc.T(action.Explanation)));
            _globalActionsPanel.Children.Add(text);
        }
    }

    private RestorePlan DeriveCurrentPlan()
    {
        int[] disabled = _entryControls
            .Where(item => item.Value.IsEnabled && item.Value.IsChecked != true)
            .Select(item => item.Key)
            .ToArray();
        return RestorePlanner.DeriveApprovedPlan(_workingPlan, disabled);
    }

    private static Border Notice(string text, Brush border, Brush background) => new()
    {
        BorderBrush = border,
        BorderThickness = new Thickness(1),
        Background = background,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(10, 7, 10, 7),
        Child = new System.Windows.Controls.TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("TextFillColorPrimaryBrush", Brushes.Black)
        }
    };

    private static System.Windows.Controls.TextBlock IssueText(string text, bool isBlocking) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 11,
        Margin = new Thickness(0, 3, 0, 0),
        FontWeight = isBlocking ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = isBlocking
            ? Brush("SystemFillColorCriticalBrush", Brushes.Firebrick)
            : Brush("SystemFillColorCautionBrush", Brushes.DarkOrange)
    };

    private static Brush OutcomeBrush(RestorePreviewOutcomeKind outcome) => outcome switch
    {
        RestorePreviewOutcomeKind.Missing =>
            Brush("SystemFillColorCriticalBrush", Brushes.Firebrick),
        RestorePreviewOutcomeKind.Ambiguous or RestorePreviewOutcomeKind.Adapted =>
            Brush("SystemFillColorCautionBrush", Brushes.DarkOrange),
        RestorePreviewOutcomeKind.Exact =>
            Brush("SystemFillColorSuccessBrush", Brushes.SeaGreen),
        _ => Brush("TextFillColorSecondaryBrush", Brushes.DimGray)
    };

    private static Brush Brush(string resourceKey, Brush fallback) =>
        System.Windows.Application.Current?.Resources.Contains(resourceKey) == true
            ? System.Windows.Application.Current.Resources[resourceKey] as Brush ?? fallback
            : fallback;

    private static string ModeLabel(RestoreModeKind mode) => mode switch
    {
        RestoreModeKind.Resume => "Resume",
        RestoreModeKind.Repair => "Repair",
        RestoreModeKind.MoveExisting => "Move existing",
        RestoreModeKind.LaunchFresh => "Launch fresh",
        RestoreModeKind.ExactSwitch => "Exact switch",
        RestoreModeKind.PreviewOnly => "Preview only",
        RestoreModeKind.Selective => "Selective restore",
        RestoreModeKind.AlignAndMinimize => "Align and minimize others",
        _ => mode.ToString()
    };
}
