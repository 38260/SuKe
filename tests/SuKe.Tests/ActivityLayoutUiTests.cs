using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using SuKe.Models;

namespace SuKe.Tests;

/// <summary>使用活动页实际行模板，验证保存标签与详情按钮在真实布局中的位置。</summary>
[Collection("WPF presentation")]
public class ActivityLayoutUiTests
{
    [Theory]
    [InlineData(640, 0.8, "Light")]
    [InlineData(640, 1.5, "Dark")]
    [InlineData(1000, 1.0, "Light")]
    [InlineData(1000, 1.5, "Dark")]
    public void Category_and_details_share_a_row_and_align_across_different_titles(double width, double scale, string theme)
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            string root = FindRepositoryRoot();
            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XElement template = XDocument.Load(Path.Combine(root, "src/SuKe/UI/Shell/ActivityPage.xaml"))
                .Descendants(presentation + "DataTemplate").First();
            XElement actionStyles = XDocument.Load(Path.Combine(root, "src/SuKe/UI/Shell/ReferenceStyles.xaml")).Root!;
            actionStyles.Elements(presentation + "Style")
                .Single(style => (string?)style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "WaPrimaryAction")
                .Attribute("BasedOn")!.Remove();
            // 事件仍由生产页面负责；此宿主只实例化原样布局，不代替详情操作的行为验证。
            foreach (XElement element in template.Descendants()) element.Attribute("Click")?.Remove();
            string resources = $$"""
                <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
                    xmlns:shell="clr-namespace:SuKe.UI.Shell;assembly=SuKe">
                    <ResourceDictionary.MergedDictionaries>
                        <ui:ThemesDictionary Theme="{{theme}}"/>
                        <ui:ControlsDictionary/>
                        <ResourceDictionary Source="pack://application:,,,/SuKe;component/UI/Shell/ShellStyles.xaml"/>
                        <ResourceDictionary Source="pack://application:,,,/SuKe;component/UI/Shell/Theme.{{theme}}.xaml"/>
                    </ResourceDictionary.MergedDictionaries>
                    {{string.Join("\n", actionStyles.Elements())}}
                    {{template}}
                </ResourceDictionary>
                """;
            var dictionary = (ResourceDictionary)XamlReader.Parse(resources);
            // 独立宿主没有 Application 资源作用域，显式接回生产样式依赖的同一 WPF-UI 基类样式。
            ((Style)dictionary["WaPrimaryAction"]).BasedOn = (Style)dictionary[typeof(Wpf.Ui.Controls.Button)];
            var items = new ItemsControl { ItemTemplate = (DataTemplate)dictionary["ActivityRowTemplate"] };
            Grid.SetIsSharedSizeScope(items, true);
            foreach ((string title, string label) in new[]
                     {
                         ("工作区已保存：设计", "保存"),
                         ("恢复结果：一个很长的工作区名称需要换行时仍保持操作区域对齐", "恢复"),
                         ("显示器布局变化", "系统"),
                     })
                items.Items.Add(new { Title = title, Meta = "09:30 · 8 个窗口", Summary = "", SummaryVisibility = Visibility.Collapsed,
                    CategoryLabel = label, Category = WorkspaceActivityCategory.Saved,
                    Icon = Wpf.Ui.Controls.SymbolRegular.Save24,
                    ConnectorAboveVisibility = Visibility.Visible, ConnectorBelowVisibility = Visibility.Visible });
            var window = new Window { Content = items, Resources = dictionary, Width = width, Height = 680, ShowInTaskbar = false };
            try
            {
                window.Show();
                SmoothScrollUiTests.Pump(50);
                foreach (TextBlock text in Walk(items).OfType<TextBlock>()) text.FontSize *= scale;
                foreach (Wpf.Ui.Controls.Button button in Walk(items).OfType<Wpf.Ui.Controls.Button>()) button.FontSize *= scale;
                window.UpdateLayout();
                SmoothScrollUiTests.Pump(50);
                var buttons = Walk(items).OfType<Wpf.Ui.Controls.Button>()
                    .Where(button => AutomationProperties.GetName(button) == "Activity details").ToArray();
                Assert.Equal(3, buttons.Length);
                var labels = Walk(items).OfType<TextBlock>().Where(text => text.Text is "保存" or "恢复" or "系统").ToArray();
                Assert.Equal(3, labels.Length);
                double? rightEdge = null;
                double? labelLeft = null;
                for (int index = 0; index < buttons.Length; index++)
                {
                    Rect button = Bounds(buttons[index], window);
                    Rect label = Bounds((FrameworkElement)labels[index].Parent, window);
                    Assert.True(Math.Abs(label.Top - button.Top) < 1, $"Category top {label.Top}, details top {button.Top}");
                    Assert.True(Math.Abs(label.Bottom - button.Bottom) < 1);
                    Assert.True(label.Right + 8 <= button.Left);
                    Assert.True(button.Right <= window.ActualWidth);
                    if (rightEdge is not null) Assert.Equal(rightEdge.Value, button.Right, 1);
                    if (labelLeft is not null) Assert.Equal(labelLeft.Value, label.Left, 1);
                    rightEdge = button.Right;
                    labelLeft = label.Left;
                }
                if (width == 1000 && scale == 1)
                {
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    string outputDirectory = Path.Combine(root, "out/scroll-ui-acceptance/screenshots");
                    Directory.CreateDirectory(outputDirectory);
                    using var file = File.Create(Path.Combine(outputDirectory, "activity-alignment.png"));
                    encoder.Save(file);
                }
            }
            finally { window.Close(); }
        });
    }

    private static Rect Bounds(FrameworkElement element, Visual parent) =>
        element.TransformToAncestor(parent).TransformBounds(new Rect(element.RenderSize));

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (DependencyObject child in Walk(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SuKe.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("SuKe.sln was not found above the test output.");
    }
}
