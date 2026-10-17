using SuKe.Models;

namespace SuKe.Services;

/// <summary>不包含标题、路径或 URL 的窗口采集原因。</summary>
public enum WindowCaptureReason
{
    SystemWindow, HiddenWindow, AuxiliaryWindow, CloakedSurface, SmallWindow,
    IdentityUnavailable, WindowClosed, ReadFailed, CaptureFailed, OwnApplicationWindow
}

/// <summary>一次采集的数量诊断；受限数量可以与可保存数量重叠。</summary>
public sealed class WindowCaptureDiagnostics
{
    /// <summary>发现的顶层窗口数，包含后续被过滤的窗口。</summary>
    public int Discovered { get; internal set; }
    /// <summary>具备保存所需信息的候选数。</summary>
    public int Savable { get; internal set; }
    /// <summary>被窗口策略或本应用过滤的数量。</summary>
    public int Filtered { get; internal set; }
    /// <summary>读取、路径或归属受限的数量，可与可保存数重叠。</summary>
    public int Limited { get; internal set; }
    /// <summary>读取、观察或枚举失败的数量。</summary>
    public int Failed { get; internal set; }
    /// <summary>桌面归属未知的候选数。</summary>
    public int UnknownDesktop { get; internal set; }
    /// <summary>请求显示器分配后仍未识别显示器的候选数。</summary>
    public int UnknownMonitor { get; internal set; }
    /// <summary>整个枚举是否失败，与成功返回空集合不同。</summary>
    public bool EnumerationFailed { get; internal set; }
    /// <summary>各类过滤或读取失败原因的数量。</summary>
    public Dictionary<WindowCaptureReason, int> Reasons { get; } = new();

    internal void AddReason(WindowCaptureReason reason) => Reasons[reason] = Reasons.GetValueOrDefault(reason) + 1;

    internal void Complete(IReadOnlyList<WindowRecord> records, bool monitorsRequested)
    {
        Savable = records.Count(record => record.CanCapture);
        UnknownDesktop = records.Count(record => !Guid.TryParse(record.VirtualDesktopId, out _));
        UnknownMonitor = monitorsRequested ? records.Count(record => string.IsNullOrEmpty(record.MonitorId)) : 0;
        Limited = records.Count(record => !record.CanCapture || record.CaptureLimitation.Length > 0 ||
            !Guid.TryParse(record.VirtualDesktopId, out _) || (monitorsRequested && string.IsNullOrEmpty(record.MonitorId)));
    }

    internal static WindowCaptureReason Exclusion(ObservedWindow window)
    {
        if (window.ObservationFailed) return WindowCaptureReason.ReadFailed;
        if (WindowPolicyEvaluator.IsSystemSurface(window)) return WindowCaptureReason.SystemWindow;
        if (!window.IsVisible) return WindowCaptureReason.HiddenWindow;
        if (window.IsCloaked) return WindowCaptureReason.CloakedSurface;
        if (!WindowPolicyEvaluator.IsPrimaryTaskWindow(window)) return WindowCaptureReason.AuxiliaryWindow;
        WindowBounds? bounds = window.IsMinimized ? window.NormalBounds : window.Bounds;
        if (bounds is { } size && (size.Width < 100 || size.Height < 100)) return WindowCaptureReason.SmallWindow;
        return WindowCaptureReason.IdentityUnavailable;
    }

    internal void Log()
    {
        AppLogger.Info("window.capture_summary", "Window capture inventory completed",
            LogField.Public("discovered", Discovered), LogField.Public("savable", Savable),
            LogField.Public("filtered", Filtered), LogField.Public("limited", Limited),
            LogField.Public("failed", Failed), LogField.Public("unknownDesktop", UnknownDesktop),
            LogField.Public("unknownMonitor", UnknownMonitor), LogField.Public("enumerationFailed", EnumerationFailed));
        foreach (var reason in Reasons)
            AppLogger.Info("window.capture_reason", "Window capture reason count",
                LogField.Public("reason", reason.Key), LogField.Public("count", reason.Value));
    }
}
