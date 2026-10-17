using SuKe.Localization;
using SuKe.Services;

namespace SuKe.UI;

internal static class WindowCaptureText
{
    internal static string Summary(WindowCaptureDiagnostics? diagnostics, int listed, bool failed = false)
    {
        if (failed || diagnostics?.EnumerationFailed == true)
            return Loc.T("Window enumeration failed. Refresh the list and try again.");
        if (diagnostics is null) return Loc.Format("Listed windows: {0}", listed);
        return Loc.Format("Discovered {0}; saveable {1}; filtered {2}; limited {3}; failed {4}. Desktop unknown {5}; monitor unknown {6}.",
            diagnostics.Discovered, diagnostics.Savable, diagnostics.Filtered, diagnostics.Limited,
            diagnostics.Failed, diagnostics.UnknownDesktop, diagnostics.UnknownMonitor);
    }
}
