using SuKe.Localization;
using SuKe.Models;

namespace SuKe.Services;

internal enum SelectedWindowFailure
{
    WindowClosed,
    ReadUnavailable,
    IdentityChanged
}

internal sealed class SelectedWindowCaptureException(IReadOnlyList<SelectedWindowFailure> failures)
    : InvalidOperationException(Loc.Format(
        "Selected windows changed or cannot be read ({0}). Refresh the window list and try again.", failures.Count))
{
    internal IReadOnlyList<SelectedWindowFailure> Failures { get; } = failures;
}

/// <summary>只复核采集来源，不改变列表选择，也不操作用户窗口。</summary>
internal static class SelectedWindowValidation
{
    internal static void Validate(IWindowInventory inventory, IReadOnlyList<WindowRecord> windows)
    {
        if (windows.Any(window => !window.CanCapture))
            throw new SelectedWindowCaptureException(windows.Where(window => !window.CanCapture)
                .Select(_ => SelectedWindowFailure.ReadUnavailable).ToArray());
        WindowRecord[] tracked = windows.Where(window => window.SourceWindowHandle != IntPtr.Zero).ToArray();
        if (tracked.Length == 0) return;
        var live = inventory.GetWindowsWithPids(WindowCandidatePolicy.RestoreMatchCandidate);
        var failures = new List<SelectedWindowFailure>();
        foreach (WindowRecord window in tracked)
        {
            if (!inventory.IsWindowAlive(window.SourceWindowHandle))
                failures.Add(SelectedWindowFailure.WindowClosed);
            else if (!live.TryGetValue(window.SourceWindowHandle, out var current))
                failures.Add(SelectedWindowFailure.ReadUnavailable);
            else if (current.Pid != window.SourceProcessId ||
                     !string.Equals(current.Record.ClassName, window.ClassName, StringComparison.Ordinal) ||
                     !string.Equals(current.Record.ExecutablePath, window.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                failures.Add(SelectedWindowFailure.IdentityChanged);
        }
        if (failures.Count > 0) throw new SelectedWindowCaptureException(failures);
    }
}
