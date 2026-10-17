using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SuKe.Localization;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SuKe.UI;

internal sealed partial class ScreenPreviewViewerWindow
{
    private ScreenPreviewPage[] _pages = [];
    private ScreenPreviewNavigation? _navigation;
    private Button? _previous;
    private Button? _next;
    private TextBlock? _pageNumber;

    internal ScreenPreviewViewerWindow(IReadOnlyList<ScreenPreviewPage> pages, int selectedIndex, Window? owner = null)
        : this(SelectedPage(pages, selectedIndex), pages, selectedIndex, owner) { }

    private ScreenPreviewViewerWindow(ScreenPreviewPage selected, IReadOnlyList<ScreenPreviewPage> pages,
        int selectedIndex, Window? owner)
        : this(selected.Image, selected.Title, selected.Subtitle, owner)
    {
        _pages = pages.ToArray();
        _navigation = new ScreenPreviewNavigation(_pages.Length, selectedIndex);
        var root = (Grid)Content;

        // 翻页按钮贴在图片左右两侧，而不是挤在底部工具条里。
        //
        // 位置由最大化这一场景决定：窗口铺满 2560×1440 后，底部按钮落在屏幕最下沿，而图片和指针都在
        // 视野中央；「实际大小」模式下图片还会超出视口并滚动，只有锚定在视口两侧的按钮才既不随图片滚走，
        // 又始终停在鼠标够得到的地方。按钮各占图片两侧一条独立空白列，因此永不遮挡画面内容。
        var stage = new Grid();
        stage.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        stage.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stage.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.Children.Remove(_scroll);
        Grid.SetColumn(_scroll, 1);
        stage.Children.Add(_scroll);

        _previous = SideButton(SymbolRegular.ChevronLeft24, "Previous image", "ScreenPreviewPrevious");
        _next = SideButton(SymbolRegular.ChevronRight24, "Next image", "ScreenPreviewNext");
        _previous.Click += (_, _) => MovePage(-1);
        _next.Click += (_, _) => MovePage(1);
        Grid.SetColumn(_previous, 0);
        stage.Children.Add(_previous);
        Grid.SetColumn(_next, 2);
        stage.Children.Add(_next);

        Grid.SetRow(stage, 2);
        root.Children.Add(stage);

        // 底部工具条保留页码与窗口动作（实际大小 / 关闭）。
        var actions = root.Children.OfType<StackPanel>().Single(panel => Grid.GetRow(panel) == 3);
        root.Children.Remove(actions);
        actions.Margin = new Thickness(0);
        var footer = new DockPanel { Margin = new Thickness(20, 0, 20, 16) };
        DockPanel.SetDock(actions, Dock.Right);
        footer.Children.Add(actions);
        _pageNumber = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0)
        };
        AutomationProperties.SetAutomationId(_pageNumber, "ScreenPreviewPageNumber");
        footer.Children.Add(_pageNumber);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        PreviewKeyDown += (_, e) =>
        {
            int offset = e.Key is Key.Left or Key.PageUp ? -1 : e.Key is Key.Right or Key.PageDown ? 1 : 0;
            if (offset == 0) return;
            e.Handled = true;
            MovePage(offset);
        };
        UpdatePage();
    }

    private static ScreenPreviewPage SelectedPage(IReadOnlyList<ScreenPreviewPage> pages, int index)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (index < 0 || index >= pages.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return pages[index];
    }

    /// <summary>一个贴在图片侧边的翻页按钮。</summary>
    /// <remarks>
    /// 用 WPF-UI 的按钮而不是自绘模板：它自带跟随主题的悬停/按下/不可用状态和焦点视觉，而这里的底色
    /// 必须压在任意一张截图上仍然看得清，交给已经调好对比度的控件外观比自己拼一个更可靠。
    /// </remarks>
    private static Button SideButton(SymbolRegular symbol, string label, string id)
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Content = new SymbolIcon { Symbol = symbol, FontSize = 20 },
            Appearance = ControlAppearance.Secondary,
            Width = 46,
            Height = 46,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Loc.T(label),
            Cursor = Cursors.Hand,
        };
        AutomationProperties.SetName(button, Loc.T(label));
        AutomationProperties.SetAutomationId(button, id);
        return button;
    }

    private void MovePage(int offset)
    {
        if (_navigation?.Move(offset) == true) UpdatePage();
    }

    private void UpdatePage()
    {
        if (_navigation is null) return;
        ScreenPreviewPage page = _pages[_navigation.Index];
        _image.Source = page.Image;
        Title = page.Title;
        var root = (Grid)Content;
        root.Children.OfType<TitleBar>().Single().Title = page.Title;
        TextBlock? subtitle = root.Children.OfType<TextBlock>().FirstOrDefault(text => Grid.GetRow(text) == 1);
        if (subtitle is null)
        {
            subtitle = new TextBlock { FontSize = 12, Margin = new Thickness(20, 10, 20, 0), TextWrapping = TextWrapping.Wrap };
            Loc.SetSkip(subtitle, true);
            Grid.SetRow(subtitle, 1);
            root.Children.Add(subtitle);
        }
        subtitle.Text = page.Subtitle ?? "";
        subtitle.Visibility = string.IsNullOrWhiteSpace(page.Subtitle) ? Visibility.Collapsed : Visibility.Visible;

        // 两侧按钮始终在场，只按位置禁用：首尾不循环，单图时两个方向都不可用。按钮位置不随图片数量
        // 变化，也就不会在切换工作区时让翻页控件忽隐忽现。
        _previous!.IsEnabled = _navigation.HasPrevious;
        _next!.IsEnabled = _navigation.HasNext;
        _pageNumber!.Text = $"{_navigation.Index + 1} / {_navigation.Count}";
        AutomationProperties.SetName(_pageNumber, Loc.Format("Image {0} of {1}", _navigation.Index + 1, _navigation.Count));

        // 每一张都回到「铺满视口」：沿用上一张的滚轮倍数会让不同分辨率的截图看起来大小不一，
        // 而用户翻页的预期是看下一块屏幕的完整画面。
        _zoom = ScreenPreviewZoom.Fit;
        ApplyZoom();
        _scroll.ScrollToHome();
    }
}
