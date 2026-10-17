using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SuKe.UI;

/// <summary>按原始字号统一缩放文字，保留标题层级，不缩放图片和图标。</summary>
internal static class TypographyService
{
    private sealed class FontState(double size)
    {
        internal double OriginalSize { get; } = size;
    }

    private static readonly ConditionalWeakTable<DependencyObject, FontState> Fonts = new();
    private static bool _installed;
    internal static double Scale { get; private set; } = 1;

    internal static void Initialize(double scale)
    {
        if (!_installed)
        {
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnLoaded), true);
            _installed = true;
        }
        SetScale(scale);
    }

    internal static void SetScale(double scale)
    {
        Scale = double.IsFinite(scale) ? Math.Clamp(scale, 0.8, 1.5) : 1;
        if (Application.Current is not { } app) return;
        foreach (Window window in app.Windows) Apply(window);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Apply((DependencyObject)sender);

    internal static void Apply(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        Walk(root, visited);
    }

    private static void Walk(DependencyObject node, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(node)) return;
        ApplyFont(node);
        if (node is FrameworkElement { ContextMenu: { } menu }) Walk(menu, visited);
        if (node is Visual)
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
                Walk(VisualTreeHelper.GetChild(node, index), visited);
        foreach (object child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dependency) Walk(dependency, visited);
    }

    private static void ApplyFont(DependencyObject node)
    {
        if (node is not (Control or TextBlock or TextElement)) return;
        var family = (FontFamily)node.GetValue(TextElement.FontFamilyProperty);
        // 字体图标采用文字渲染，但并不是用户要调整的文字。
        if (node is Wpf.Ui.Controls.SymbolIcon or Wpf.Ui.Controls.FontIcon ||
            family.Source.Contains("Icons", StringComparison.OrdinalIgnoreCase) ||
            family.Source.Contains("MDL2", StringComparison.OrdinalIgnoreCase)) return;

        DependencyProperty property = TextElement.FontSizeProperty;
        if (!Fonts.TryGetValue(node, out FontState? state))
        {
            ValueSource source = DependencyPropertyHelper.GetValueSource(node, property);
            // 继承值和模板传递值已随上级变化，不能再乘一次比例。
            if (source.BaseValueSource is BaseValueSource.Inherited or BaseValueSource.ParentTemplate
                or BaseValueSource.ParentTemplateTrigger) return;
            state = new FontState((double)node.GetValue(property));
            Fonts.Add(node, state);
        }
        double size = state.OriginalSize * Scale;
        if (Math.Abs((double)node.GetValue(property) - size) > 0.00001)
            node.SetCurrentValue(property, size);
    }
}
