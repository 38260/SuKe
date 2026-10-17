using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using SuKe.Models;

namespace SuKe.Localization;

/// <summary>
/// Runtime Chinese localization layer for the SuKe user interface.
/// </summary>
/// <remarks>
/// <para>
/// The English source strings in XAML and code are never modified. Instead this layer replaces
/// them on the way to the screen:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="Apply"/> walks a loaded visual/logical tree and rewrites text-bearing properties
/// (<c>TextBlock.Text</c>, <c>ContentControl.Content</c>, menu and headered headers, window and
/// title-bar titles, tooltips, and automation names) using <see cref="LocalizationCatalog"/>.
/// </description></item>
/// <item><description>
/// <see cref="T(string)"/> and <see cref="Format"/> are used by code that composes text at
/// runtime, including interpolated format strings, which the tree walker cannot match.
/// </description></item>
/// </list>
/// <para>
/// Two safeguards keep user data intact. Text that comes from a data binding is never touched,
/// and code-built elements that render user data (window titles, workspace names, tab labels) are
/// opted out with <see cref="SetSkip"/>. Translation is idempotent: lookups are keyed by the
/// English text and no entry maps onto a different key, so a repeated pass cannot drift. A handful
/// of entries are deliberate identity mappings for text that stays English either way — the
/// product name and the separator/skeleton formats such as <c>"{0}  ·  {1}"</c>.
/// </para>
/// <para>
/// Set the environment variable <c>SUKE_UI_LANG</c> to <c>en</c> to keep the original
/// English interface without rebuilding.
/// </para>
/// <para>
/// The language is also a user setting. <see cref="SetLanguage"/> switches the layer at runtime and
/// re-walks every open window, so the change is visible without restarting. Returning to English
/// uses a reverse map derived from the same catalog; because a few English sources legitimately
/// share one Chinese string (a title-case and a sentence-case variant), those strings come back as
/// one deterministic variant until the next start, when the original markup text is used again.
/// </para>
/// </remarks>
internal static class Loc
{
    private const string LanguageEnvironmentVariable = "SUKE_UI_LANG";

    /// <summary>Marks an element (and its subtree) as user data that must not be translated.</summary>
    private static readonly DependencyProperty SkipProperty =
        DependencyProperty.RegisterAttached(
            "Skip",
            typeof(bool),
            typeof(Loc),
            new PropertyMetadata(false));

    /// <summary>Marks an element as already processed so repeated walks stay cheap.</summary>
    private static readonly DependencyProperty LocalizedMarkerProperty =
        DependencyProperty.RegisterAttached(
            "LocalizedMarker",
            typeof(bool),
            typeof(Loc),
            new PropertyMetadata(false));

    private static readonly Regex MonitorSegment =
        new(@"^Monitor (\d+)$", RegexOptions.CultureInvariant);

    private static readonly Regex ScoreSegment =
        new(@"^Score (\d+)$", RegexOptions.CultureInvariant);

    private static readonly Regex BoundsSegment =
        new(@"^(\d+)×(\d+) at (-?\d+),(-?\d+)$", RegexOptions.CultureInvariant);

    private static readonly Regex ResolutionSegment =
        new(@"^(\d+)×(\d+)$", RegexOptions.CultureInvariant);

    private static readonly Regex LocalizedMonitorSegment =
        new(@"^显示器 (\d+)$", RegexOptions.CultureInvariant);

    private static readonly Regex LocalizedScoreSegment =
        new(@"^评分 (\d+)$", RegexOptions.CultureInvariant);

    private static readonly Regex LocalizedBoundsSegment =
        new(@"^(\d+)×(\d+)，位置 (-?\d+),(-?\d+)$", RegexOptions.CultureInvariant);

    private static Dictionary<string, string>? _reverseEntries;

    /// <summary>Raised after the active language changed and open windows were re-localized.</summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// Interface language requested by the user setting. Declared before <see cref="Enabled"/>
    /// because static initializers run in declaration order and the enabled flag reads it.
    /// </summary>
    private static UiLanguage CurrentLanguage { get; set; } = UiLanguage.ChineseSimplified;

    /// <summary>Whether replacement is active. The environment variable wins over the setting.</summary>
    public static bool Enabled { get; private set; } = ResolveEnabled();

    private static bool ResolveEnabled() =>
        !IsEnvironmentEnglish() && CurrentLanguage != UiLanguage.English;

    private static bool IsEnvironmentEnglish()
    {
        string? requested = Environment.GetEnvironmentVariable(LanguageEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(requested)) return false;
        string value = requested.Trim();
        return value.Equals("en", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("en-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies the persisted interface language. The environment variable still overrides it, so a
    /// developer can force English without touching settings.
    /// </summary>
    internal static void SetLanguage(UiLanguage language)
    {
        CurrentLanguage = language;
        bool enabled = ResolveEnabled();
        if (enabled == Enabled) return;
        Enabled = enabled;
        ReapplyOpenWindows();
        LanguageChanged?.Invoke();
    }

    /// <summary>Re-localizes every open window, including elements already processed once.</summary>
    internal static void ReapplyOpenWindows()
    {
        System.Windows.Application? application = System.Windows.Application.Current;
        if (application is null) return;
        foreach (Window window in application.Windows)
            Reapply(window);
    }

    /// <summary>Re-localizes a subtree even when it was already processed in the other language.</summary>
    internal static void Reapply(DependencyObject? root)
    {
        if (root is null) return;
        Walk(root, new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance), force: true);
    }

    /// <summary>
    /// Chinese-to-English table used when the layer turns translation off again. It is derived from
    /// the same catalog so both directions can never drift apart.
    /// </summary>
    /// <remarks>
    /// Entries whose Chinese value is also an English key are excluded: translating through them
    /// would chain two lookups and drift. Where several English sources share one Chinese string,
    /// the ordinal-first source wins so the mapping is deterministic.
    /// </remarks>
    private static Dictionary<string, string> ReverseEntries
    {
        get
        {
            if (_reverseEntries is not null) return _reverseEntries;
            var candidates = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach ((string english, string chinese) in LocalizationCatalog.Entries)
            {
                if (chinese.Length == 0 ||
                    chinese.Equals(english, StringComparison.Ordinal) ||
                    LocalizationCatalog.Entries.ContainsKey(chinese))
                {
                    continue;
                }

                if (!candidates.TryGetValue(chinese, out List<string>? sources))
                {
                    sources = new List<string>();
                    candidates[chinese] = sources;
                }

                sources.Add(english);
            }

            var reverse = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string chinese, List<string> sources) in candidates)
            {
                sources.Sort(StringComparer.Ordinal);
                reverse[chinese] = sources[0];
            }

            return _reverseEntries = reverse;
        }
    }

    /// <summary>Opts an element and its subtree out of translation.</summary>
    public static void SetSkip(DependencyObject element, bool skip) =>
        element.SetValue(SkipProperty, skip);

    /// <summary>Translates an exact English interface string. Unknown strings are returned unchanged.</summary>
    public static string T(string? english)
    {
        if (string.IsNullOrEmpty(english)) return english ?? string.Empty;
        return Lookup(english);
    }

    /// <summary>Resolves one whole string in the direction that is currently active.</summary>
    private static string Lookup(string text) =>
        TryLookup(text, out string localized) ? localized : text;

    private static bool TryLookup(string text, out string localized)
    {
        if (Enabled)
            return LocalizationCatalog.Entries.TryGetValue(text, out localized!);
        return ReverseEntries.TryGetValue(text, out localized!);
    }

    /// <summary>
    /// Translates an English composite format string and formats it with <paramref name="args"/>.
    /// Unknown templates fall back to the English text so formatting never fails.
    /// </summary>
    public static string Format(string englishFormat, params object?[] args)
    {
        if (englishFormat is null) return string.Empty;
        string template = Lookup(englishFormat);
        return string.Format(CultureInfo.CurrentCulture, template, args);
    }

    /// <summary>
    /// Translates a composed label such as <c>Monitor 2 · mapped to primary monitor</c>. Segments
    /// that are user data or machine identifiers are left untouched.
    /// </summary>
    public static string TLabel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
        if (TryLookup(text, out string direct)) return direct;
        if (!text.Contains(" · ", StringComparison.Ordinal)) return text;

        string[] segments = text.Split([" · "], StringSplitOptions.None);
        for (int i = 0; i < segments.Length; i++)
            segments[i] = TranslateSegment(segments[i]);
        return string.Join(" · ", segments);
    }

    private static string TranslateSegment(string segment)
    {
        string trimmed = segment.Trim();
        if (trimmed.Length == 0) return segment;
        if (TryLookup(trimmed, out string localized))
            return segment.Replace(trimmed, localized, StringComparison.Ordinal);

        if (Enabled)
        {
            Match match = MonitorSegment.Match(trimmed);
            if (match.Success) return segment.Replace(trimmed, $"显示器 {match.Groups[1].Value}", StringComparison.Ordinal);

            match = ScoreSegment.Match(trimmed);
            if (match.Success) return segment.Replace(trimmed, $"评分 {match.Groups[1].Value}", StringComparison.Ordinal);

            match = BoundsSegment.Match(trimmed);
            if (match.Success)
            {
                return segment.Replace(
                    trimmed,
                    $"{match.Groups[1].Value}×{match.Groups[2].Value}，位置 {match.Groups[3].Value},{match.Groups[4].Value}",
                    StringComparison.Ordinal);
            }
        }
        else
        {
            // Inverse of the segment forms this class generates, so a live switch back to English
            // can restore them.
            Match match = LocalizedMonitorSegment.Match(trimmed);
            if (match.Success) return segment.Replace(trimmed, $"Monitor {match.Groups[1].Value}", StringComparison.Ordinal);

            match = LocalizedScoreSegment.Match(trimmed);
            if (match.Success) return segment.Replace(trimmed, $"Score {match.Groups[1].Value}", StringComparison.Ordinal);

            match = LocalizedBoundsSegment.Match(trimmed);
            if (match.Success)
            {
                return segment.Replace(
                    trimmed,
                    $"{match.Groups[1].Value}×{match.Groups[2].Value} at {match.Groups[3].Value},{match.Groups[4].Value}",
                    StringComparison.Ordinal);
            }
        }

        Match resolution = ResolutionSegment.Match(trimmed);
        if (resolution.Success)
            return segment.Replace(trimmed, $"{resolution.Groups[1].Value}×{resolution.Groups[2].Value}", StringComparison.Ordinal);

        return segment;
    }

    /// <summary>
    /// Registers the application-wide hooks that localize every window, context menu, and tooltip
    /// as soon as it is loaded.
    /// </summary>
    /// <remarks>
    /// <see cref="MenuItem"/> gets its own registration because menu items frequently live in
    /// subtrees that are realized after their owning menu was loaded — dynamically populated
    /// submenus and popup-hosted menus — and would otherwise never be visited.
    /// </remarks>
    public static void Install()
    {
        // Registered even while translation is off: the language is a user setting, so the hooks
        // must already be in place when the user switches to Chinese during this session.
        RoutedEventHandler handler = (sender, _) => Apply(sender as DependencyObject);
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, handler);
        EventManager.RegisterClassHandler(typeof(ContextMenu), FrameworkElement.LoadedEvent, handler);
        EventManager.RegisterClassHandler(typeof(MenuItem), FrameworkElement.LoadedEvent, handler);
        EventManager.RegisterClassHandler(typeof(ToolTip), FrameworkElement.LoadedEvent, handler);
        // 工作区列表刷新后才创建模板，单靠窗口 Loaded 无法覆盖这些新控件。
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Primitives.ButtonBase), FrameworkElement.LoadedEvent, handler);
    }

    /// <summary>
    /// Localizes every text-bearing property in the subtree rooted at <paramref name="root"/>.
    /// Already-processed elements are skipped, so repeated calls stay cheap.
    /// </summary>
    public static void Apply(DependencyObject? root)
    {
        if (root is null) return;
        Walk(root, new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance), force: false);
    }

    private static void Walk(DependencyObject node, HashSet<DependencyObject> visited, bool force)
    {
        if (!visited.Add(node)) return;
        if (node is DependencyObject skipNode && IsSkip(skipNode)) return;

        LocalizeElement(node, force);

        int visualCount = 0;
        try
        {
            visualCount = VisualTreeHelper.GetChildrenCount(node);
        }
        catch (InvalidOperationException)
        {
            // Not a Visual; only logical children can be walked.
        }

        // Both child collections are snapshotted before descending. Visiting a child can rewrite
        // its parent's content — localizing a Run notifies the owning text container, and a
        // TextBlock's logical children *are* its inline collection — and mutating a collection
        // while it is being enumerated throws.
        var visualChildren = new List<DependencyObject>(visualCount);
        for (int i = 0; i < visualCount; i++)
            visualChildren.Add(VisualTreeHelper.GetChild(node, i));
        foreach (DependencyObject child in visualChildren)
            Walk(child, visited, force);

        var logicalChildren = new List<object?>();
        foreach (object? child in LogicalTreeHelper.GetChildren(node))
            logicalChildren.Add(child);
        foreach (object? child in logicalChildren)
        {
            if (child is DependencyObject dependencyObject)
                Walk(dependencyObject, visited, force);
        }
    }

    private static bool IsSkip(DependencyObject node) =>
        node is DependencyObject target && target.ReadLocalValue(SkipProperty) is bool skip && skip;

    private static void LocalizeElement(DependencyObject node, bool force)
    {
        if (node is FrameworkElement element)
        {
            if (!force &&
                element.ReadLocalValue(LocalizedMarkerProperty) is bool already && already)
            {
                return;
            }

            element.SetValue(LocalizedMarkerProperty, true);
        }

        switch (node)
        {
            case Window window:
                window.Title = T(window.Title);
                break;
            case Wpf.Ui.Controls.TitleBar titleBar:
                titleBar.Title = T(titleBar.Title);
                break;
            case TextBlock textBlock:
                LocalizeTextBlock(textBlock);
                break;
            case Run run:
                if (!IsBound(run, Run.TextProperty))
                    run.Text = T(run.Text);
                break;
        }

        if (node is ContentControl contentControl &&
            contentControl.Content is string contentText &&
            !IsBound(contentControl, ContentControl.ContentProperty))
        {
            contentControl.Content = T(contentText);
        }

        if (node is HeaderedContentControl headeredContent &&
            headeredContent.Header is string headeredText &&
            !IsBound(headeredContent, HeaderedContentControl.HeaderProperty))
        {
            headeredContent.Header = T(headeredText);
        }

        if (node is HeaderedItemsControl headeredItems &&
            headeredItems.Header is string itemsHeader &&
            !IsBound(headeredItems, HeaderedItemsControl.HeaderProperty))
        {
            headeredItems.Header = T(itemsHeader);
        }

        if (node is FrameworkElement tooltipOwner &&
            tooltipOwner.ToolTip is string tooltip &&
            !IsBound(tooltipOwner, FrameworkElement.ToolTipProperty))
        {
            tooltipOwner.ToolTip = T(tooltip);
        }

        string? name = AutomationProperties.GetName(node);
        if (!string.IsNullOrWhiteSpace(name))
            AutomationProperties.SetName(node, T(name));

        string? helpText = AutomationProperties.GetHelpText(node);
        if (!string.IsNullOrWhiteSpace(helpText))
            AutomationProperties.SetHelpText(node, T(helpText));
    }

    private static bool IsBound(DependencyObject node, DependencyProperty property) =>
        BindingOperations.GetBindingExpressionBase(node, property) is not null;

    /// <summary>
    /// Localizes a <see cref="TextBlock"/> without disturbing which content model it uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TextBlock.Text</c> and <c>TextBlock.Inlines</c> are alternative content models: assigning
    /// <c>Text</c> rewrites the whole inline collection. A block built from child inlines reports an
    /// empty <c>Text</c>, so blindly assigning to <c>Text</c> would clear those inlines and render
    /// the block blank. When explicit inlines are present the runs are translated in place instead.
    /// </para>
    /// <para>
    /// Blocks whose content came from the <c>Text</c> property — every case in this project's XAML —
    /// always carry exactly one inline and keep the original path, so their handling is unchanged.
    /// Bound text is never touched on either path.
    /// </para>
    /// </remarks>
    private static void LocalizeTextBlock(TextBlock textBlock)
    {
        bool bound = IsBound(textBlock, TextBlock.TextProperty);
        bool hasExplicitInlines = textBlock.Inlines.Count > 1
            || (textBlock.Inlines.Count == 1 && textBlock.Text.Length == 0);

        if (hasExplicitInlines)
        {
            if (bound) return;
            // Snapshot: assigning Run.Text notifies the text container, so enumerating the live
            // inline collection here would throw.
            var inlines = new Inline[textBlock.Inlines.Count];
            textBlock.Inlines.CopyTo(inlines, 0);
            foreach (Inline inline in inlines)
            {
                if (inline is Run inlineRun && !IsBound(inlineRun, Run.TextProperty))
                    inlineRun.Text = T(inlineRun.Text);
            }

            return;
        }

        if (!bound) textBlock.Text = T(textBlock.Text);
    }
}
