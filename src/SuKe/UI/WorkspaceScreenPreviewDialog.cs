using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SuKe.Models;
using SuKe.Services;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using TitleBar = Wpf.Ui.Controls.TitleBar;

namespace SuKe.UI;

/// <summary>按屏幕展示命名工作区保存时的本地截图。</summary>
internal sealed class WorkspaceScreenPreviewDialog : FluentWindow
{
    internal WorkspaceScreenPreviewDialog(WorkspaceSnapshot workspace, WorkspacePreviewStore previews)
    {
        Title = ScreenPreviewText.PreviewTitle;
        Width = 960;
        Height = 700;
        MinWidth = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ExtendsContentIntoTitleBar = true;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBar = new TitleBar
        {
            Title = ScreenPreviewText.PreviewTitle,
            ShowMinimize = false,
            ShowMaximize = true
        };
        root.Children.Add(titleBar);

        var panels = new StackPanel { Margin = new Thickness(20, 12, 20, 16) };
        var pages = new System.Collections.Generic.List<ScreenPreviewPage>();
        int displayed = 0;
        foreach (ScreenPreview preview in previews.Load(workspace))
        {
            try
            {
                // 这里给的是放大查看用的位图，必须保留原始像素：列表里缩略图和查看器共用同一份解码结果。
                if (ScreenPreviewImage.Decode(preview.Png) is not BitmapSource bitmap) continue;

                panels.Children.Add(new TextBlock
                {
                    Text = ScreenPreviewText.ScreenLabel(preview.ScreenIndex),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, displayed == 0 ? 0 : 18, 0, 8)
                });
                var view = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    MaxWidth = 900,
                    MaxHeight = 540,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                // Enlarging stays available from the dialog itself, so a detail that is unreadable at
                // list scale does not require closing and reopening anything.
                view.Cursor = Cursors.Hand;
                view.ToolTip = ScreenPreviewText.ZoomHint;
                int selectedPage = pages.Count;
                pages.Add(new ScreenPreviewPage(bitmap, ScreenPreviewText.ScreenLabel(preview.ScreenIndex),
                    MonitorSubtitle(workspace, preview.ScreenIndex)));
                view.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                    new ScreenPreviewViewerWindow(pages, selectedPage, this).ShowDialog();
                };
                panels.Children.Add(view);
                displayed++;
            }
            catch (Exception)
            {
                // 损坏的单张截图不影响其余屏幕，也不影响恢复操作。
            }
        }

        if (displayed == 0)
            panels.Children.Add(new TextBlock { Text = ScreenPreviewText.MissingPreview });

        var scroll = new ScrollViewer
        {
            Content = panels,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var close = new Wpf.Ui.Controls.Button
        {
            Content = ScreenPreviewText.Close,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 20, 16),
            MinWidth = 88,
            IsDefault = true
        };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 2);
        root.Children.Add(close);

        Content = root;
    }

    /// <summary>Monitor name and resolution for one captured screen, when it is still resolvable.</summary>
    private static string? MonitorSubtitle(WorkspaceSnapshot workspace, int screenIndex)
    {
        MonitorInfo? monitor = workspace.Monitors.FirstOrDefault(item => item.Index == screenIndex);
        return monitor is null
            ? null
            : $"{monitor.FriendlyName} · {monitor.WidthPixels} × {monitor.HeightPixels}";
    }
}
