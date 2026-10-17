using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using SuKe.Localization;
using SuKe.Models;
using SuKe.UI.Shell;

namespace SuKe.Tests;

[Collection("WPF presentation")]
public class ActivityDetailsUiTests
{
    [Theory]
    [InlineData("Light", 1.0)]
    [InlineData("Dark", 1.5)]
    public void Detailed_snapshot_scrolls_and_close_stays_visible(string theme, double scale)
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            var entry = Entry() with
            {
                Snapshot = new WorkspaceActivitySnapshot
                {
                    SavedWithFiles = true, BrowserTabCount = 7, ExplorerTabCount = 3, TerminalTabCount = 2,
                    Applications = Enumerable.Range(0, 40).Select(index => new WorkspaceActivityApplication("editor-" + index, 1)).ToArray(),
                    Monitors = [new WorkspaceActivityMonitor(0, "Display", 2560, 1440, 144, true, 40)],
                },
            };
            var dialog = ActivityPage.CreateDetailsWindow(new ActivityRowItem { Entry = entry, Title = "Design" }, null);
            dialog.Resources.MergedDictionaries.Add(Resources(theme));
            dialog.ShowInTaskbar = false;
            try
            {
                dialog.Show();
                SmoothScrollUiTests.Pump(40);
                foreach (TextBlock text in Walk(dialog).OfType<TextBlock>()) text.FontSize *= scale;
                dialog.Width = dialog.MinWidth;
                dialog.Height = dialog.MinHeight;
                dialog.UpdateLayout();
                var scroll = Walk(dialog).OfType<ScrollViewer>().First();
                Assert.True(scroll.ScrollableHeight > 0);
                string textContent = string.Join("\n", Walk(dialog).OfType<TextBlock>().Select(text => text.Text));
                Assert.Contains(Loc.T("Saved content"), textContent);
                Assert.Contains("2560", textContent);
                Assert.Contains("150%", textContent);
                Assert.Contains(Loc.Format("{0}: {1} window(s)", "editor-0", 1), textContent);
                var close = Walk(dialog).OfType<Wpf.Ui.Controls.Button>().Single(button => button.IsCancel);
                Rect bounds = close.TransformToAncestor(dialog).TransformBounds(new Rect(close.RenderSize));
                Assert.True(bounds.Bottom <= dialog.ActualHeight && bounds.Top > 0);
                Assert.DoesNotContain(close, Walk(scroll));
                scroll.ScrollToEnd();
                SmoothScrollUiTests.Pump(30);
                Assert.True(scroll.VerticalOffset > 0);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Old_restore_record_reports_unknown_and_does_not_invent_snapshot_counts()
    {
        SmoothScrollUiTests.RunSta(() =>
        {
            var entry = Entry() with { Kind = WorkspaceActivityKind.Checkpoint, Category = WorkspaceActivityCategory.Restore, Trigger = WorkspaceCheckpointTrigger.Restore };
            var dialog = ActivityPage.CreateDetailsWindow(new ActivityRowItem { Entry = entry, Title = "Restore" }, null);
            var layout = Assert.IsType<DockPanel>(dialog.Content);
            var content = Assert.IsType<StackPanel>(Assert.IsType<ScrollViewer>(layout.Children[^1]).Content);
            string text = string.Join("\n", content.Children.OfType<TextBlock>().Select(block => block.Text));
            Assert.Contains(RestoreOutcomeText.UnknownResultSummary(), text);
            Assert.Contains(Loc.T("The original snapshot is no longer available. Only recorded execution results are shown."), text);
            Assert.DoesNotContain("2560", text);
            dialog.Close();
        });
    }

    internal static ResourceDictionary Resources(string theme = "Light") => (ResourceDictionary)XamlReader.Parse($$"""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="{{theme}}"/><ui:ControlsDictionary/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
        """);

    internal static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (DependencyObject child in Walk(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static WorkspaceActivityEntry Entry() => new()
    {
        Kind = WorkspaceActivityKind.WorkspaceSaved, Category = WorkspaceActivityCategory.Saved,
        OccurredAtLocal = DateTime.Now, WorkspaceName = "Design", WorkspaceId = "workspace-id", WindowCount = 40, MonitorCount = 1,
    };
}
