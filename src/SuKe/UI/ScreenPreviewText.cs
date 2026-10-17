using SuKe.Localization;

namespace SuKe.UI;

/// <summary>
/// Interface copy used by the saved-screen-preview surfaces.
/// </summary>
/// <remarks>
/// Every value is resolved through <see cref="Loc"/> at call time rather than cached, so the
/// preview windows follow the persisted interface language instead of only the environment
/// override that existed when this type was introduced.
/// </remarks>
internal static class ScreenPreviewText
{
    internal static string SaveWarning => Loc.T(
        "Workspace saved, but some screen previews could not be captured.");

    internal static string PreviewAction => Loc.T("View saved screen screenshots");
    internal static string MissingPreview => Loc.T(
        "No screen screenshots are available for this workspace.");
    internal static string PreviewTitle => Loc.T("Saved Screen Preview");
    internal static string Close => Loc.T("Close");
    internal static string ScreenLabel(int index) => Loc.Format("Screen {0}", index + 1);
    internal static string ZoomHint => Loc.T("Click a preview to enlarge it.");
}
