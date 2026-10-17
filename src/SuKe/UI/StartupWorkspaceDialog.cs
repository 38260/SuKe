using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using SuKe.Localization;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.UI;

/// <summary>
/// Shown on startup when <see cref="StartupBehavior.AskUser"/> is configured.
/// Lets the user pick a workspace to restore (or skip into the main interface).
/// </summary>
/// <remarks>
/// Each row previews what the workspace will bring back: the application icons of the windows it
/// restores and the screen previews captured when it was saved. The previews are shown inline —
/// opening them is not required to see them — and double-clicking a preview opens it full size
/// instead of restoring the workspace.
/// </remarks>
internal sealed class StartupWorkspaceDialog : FluentWindow
{
    private const int MaximumAppIcons = 12;
    private const int ThumbnailWidth  = 260;
    private const double ThumbnailMaxHeight = 150;

    private readonly System.Windows.Controls.ListBox _listBox;
    private readonly WorkspacePreviewStore _previews;

    /// <summary>The workspace the user chose, or <c>null</c> if they clicked Skip / closed.</summary>
    public WorkspaceSnapshot? SelectedWorkspace { get; private set; }

    /// <summary>
    /// <c>true</c> when the user explicitly clicked Skip. Closing the window with the title-bar
    /// button leaves this <c>false</c>, so only Skip navigates onwards.
    /// </summary>
    public bool SkipRequested { get; private set; }

    public StartupWorkspaceDialog(IReadOnlyList<WorkspaceSnapshot> workspaces, WorkspacePreviewStore previews)
    {
        _previews = previews;

        Title                      = "SuKe — Restore Workspace";
        Width                      = 620;
        SizeToContent              = SizeToContent.Height;
        MaxHeight                  = 700;
        ResizeMode                 = ResizeMode.NoResize;
        WindowStartupLocation     = WindowStartupLocation.CenterScreen;
        ExtendsContentIntoTitleBar = true;
        ShowInTaskbar              = true;
        Topmost                    = true;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());

        var titleBar = new TitleBar
        {
            Title        = "Restore Workspace",
            ShowMinimize = false,
            ShowMaximize = false,
        };
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        var content = new StackPanel { Margin = new Thickness(20, 14, 20, 20) };
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        content.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text       = "Choose a workspace to restore:",
            FontSize   = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin     = new Thickness(0, 0, 0, 12),
        });

        _listBox = new System.Windows.Controls.ListBox
        {
            MaxHeight   = 400,
            Margin      = new Thickness(0, 0, 0, 16),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
        };

        foreach (var ws in workspaces.OrderByDescending(w => w.SavedAt))
        {
            var row = new StackPanel();

            // The workspace name is user data: never let the localization walker touch it.
            var nameText = new System.Windows.Controls.TextBlock
            {
                Text       = ws.Name,
                FontSize   = 13,
                FontWeight = FontWeights.Medium,
                Foreground = Res("TextFillColorPrimaryBrush"),
            };
            Loc.SetSkip(nameText, true);

            var metaText = new System.Windows.Controls.TextBlock
            {
                Text       = Loc.Format("{0} windows  \u2022  saved {1}",
                    ws.Entries.Count, ws.SavedAt.ToLocalTime().ToString("g")),
                FontSize   = 11,
                Foreground = Res("TextFillColorTertiaryBrush"),
                Margin     = new Thickness(0, 2, 0, 0),
            };

            row.Children.Add(nameText);
            row.Children.Add(metaText);
            row.Children.Add(BuildApplicationIcons(ws));
            row.Children.Add(BuildScreenPreviews(ws));

            var item = new System.Windows.Controls.ListBoxItem
            {
                Tag     = ws,
                Padding = new Thickness(12, 8, 12, 8),
                Content = row,
            };
            item.MouseDoubleClick += (_, _) => DoRestore();
            _listBox.Items.Add(item);
        }

        if (_listBox.Items.Count > 0) _listBox.SelectedIndex = 0;
        content.Children.Add(_listBox);

        // ── Buttons ─────────────────────────────────────────────────────
        var btnRow = new StackPanel
        {
            Orientation         = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
        };

        var skipBtn = new Wpf.Ui.Controls.Button
        {
            Content = "Skip",
            Margin  = new Thickness(0, 0, 8, 0),
        };
        skipBtn.Click += (_, _) => { SkipRequested = true; DialogResult = false; };

        var restoreBtn = new Wpf.Ui.Controls.Button
        {
            Content    = "Restore",
            Appearance = ControlAppearance.Primary,
        };
        restoreBtn.Click += (_, _) => DoRestore();

        btnRow.Children.Add(skipBtn);
        btnRow.Children.Add(restoreBtn);
        content.Children.Add(btnRow);

        Content = root;
    }

    private void DoRestore()
    {
        if (_listBox.SelectedItem is System.Windows.Controls.ListBoxItem { Tag: WorkspaceSnapshot ws })
        {
            SelectedWorkspace = ws;
            DialogResult = true;
        }
    }

    /// <summary>
    /// One badge per distinct application the workspace restores, so the user can see at a glance
    /// whether it is a Chrome workspace, an Explorer workspace, a terminal workspace, and so on.
    /// </summary>
    private static UIElement BuildApplicationIcons(WorkspaceSnapshot workspace)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int shown = 0;

        foreach (WorkspaceEntry entry in workspace.Entries)
        {
            string path = entry.ExecutablePath;
            string key  = !string.IsNullOrWhiteSpace(path) ? path : entry.ProcessName;
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key)) continue;
            if (shown++ >= MaximumAppIcons) break;

            string label = ApplicationLabel(entry);
            FrameworkElement badge = ApplicationIconHelper.CreateBadge(
                string.IsNullOrWhiteSpace(path) ? null : path, label);
            badge.Margin = new Thickness(0, 2, 5, 2);
            // Application names come from the user's machine, not from interface copy.
            Loc.SetSkip(badge, true);
            panel.Children.Add(badge);
        }

        return panel;
    }

    /// <summary>
    /// The screen previews captured when the workspace was saved, rendered inline. Double-clicking
    /// one opens the full-size viewer instead of restoring the workspace.
    /// </summary>
    private UIElement BuildScreenPreviews(WorkspaceSnapshot workspace)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };

        IReadOnlyList<ScreenPreview> images;
        try
        {
            images = _previews.Load(workspace);
        }
        catch (Exception)
        {
            // A damaged preview archive must never block the startup prompt.
            return panel;
        }

        foreach (ScreenPreview image in images)
        {
            BitmapSource? bitmap = DecodeThumbnail(image.Png);
            if (bitmap is null) continue;

            var frame = new Border
            {
                Margin          = new Thickness(0, 0, 8, 0),
                Padding         = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush     = Res("CardStrokeColorDefaultBrush"),
                Background      = Res("CardStrokeColorDefaultBrush"),
                Child           = new System.Windows.Controls.Image
                {
                    Source                = bitmap,
                    MaxWidth              = ThumbnailWidth,
                    MaxHeight             = ThumbnailMaxHeight,
                    Stretch               = Stretch.Uniform,
                    HorizontalAlignment   = System.Windows.HorizontalAlignment.Left,
                    UseLayoutRounding     = true,
                },
            };

            // Border is not a Control, so the control-level double-click event is unavailable here.
            // Counting clicks on the button-down event lets the handler respond to a single click and
            // stop the event from bubbling into the list item, which would restore the workspace.
            // 放大查看需要原始像素，所以在点击时才重新解码，列表里仍是廉价缩略图。
            int screenIndex = image.ScreenIndex;
            frame.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                OpenViewer(workspace, screenIndex);
            };
            frame.ToolTip = ScreenPreviewText.ZoomHint;
            frame.Cursor = System.Windows.Input.Cursors.Hand;

            panel.Children.Add(frame);
        }

        return panel;
    }

    /// <summary>按原始像素重新解码整份预览，再打开放大查看器。</summary>
    /// <remarks>
    /// 列表里的位图是按缩略图宽度解码的，直接拿去放大就是糊的；查看器打开即铺满屏幕，
    /// 因此这里重新读一次 zip。读取失败保持原有策略：不打开查看器，也不影响启动流程。
    /// </remarks>
    private void OpenViewer(WorkspaceSnapshot workspace, int screenIndex)
    {
        IReadOnlyList<ScreenPreview> images;
        try
        {
            images = _previews.Load(workspace);
        }
        catch (Exception)
        {
            return;
        }

        var pages = new List<ScreenPreviewPage>(images.Count);
        int selectedPage = 0;
        foreach (ScreenPreview image in images)
        {
            if (ScreenPreviewImage.Decode(image.Png) is not BitmapSource full) continue;
            if (image.ScreenIndex == screenIndex) selectedPage = pages.Count;
            pages.Add(new ScreenPreviewPage(full, ScreenPreviewText.ScreenLabel(image.ScreenIndex),
                MonitorSubtitle(workspace, image.ScreenIndex)));
        }

        if (pages.Count == 0) return;
        new ScreenPreviewViewerWindow(pages, selectedPage, this).ShowDialog();
    }

    /// <summary>Monitor name and resolution for one captured screen, when it is still resolvable.</summary>
    private static string? MonitorSubtitle(WorkspaceSnapshot workspace, int screenIndex)
    {
        MonitorInfo? monitor = workspace.Monitors.FirstOrDefault(item => item.Index == screenIndex);
        return monitor is null
            ? null
            : $"{monitor.FriendlyName} · {monitor.WidthPixels} × {monitor.HeightPixels}";
    }

    private static BitmapSource? DecodeThumbnail(byte[] png)
    {
        try
        {
            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(png))
            {
                bitmap.BeginInit();
                bitmap.CacheOption      = BitmapCacheOption.OnLoad;
                // 同一位图用于缩略图和放大窗口，避免最大化后只能看 260px 小图。
                bitmap.DecodePixelWidth = 1600;
                bitmap.StreamSource     = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ApplicationLabel(WorkspaceEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.ExecutablePath))
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(entry.ExecutablePath);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            catch (ArgumentException)
            {
                // Fall through to the process name.
            }
        }
        return string.IsNullOrWhiteSpace(entry.ProcessName) ? "Application" : entry.ProcessName;
    }

    private static System.Windows.Media.Brush Res(string key) =>
        (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[key];
}
