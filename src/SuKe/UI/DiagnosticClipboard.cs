using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SuKe.UI;

/// <summary>
/// Copies text to the clipboard with a short bounded retry.
/// </summary>
/// <remarks>
/// <para>
/// <c>Clipboard.SetText</c> raises <see cref="COMException"/> with <c>CLIPBRD_E_CANT_OPEN</c>
/// whenever another process currently has the clipboard open. Clipboard managers, remote-desktop
/// sessions, password managers, and other applications open it routinely, so the failure is
/// usually transient and a retry a few tens of milliseconds later succeeds. This is a clipboard
/// availability problem and has nothing to do with the content being copied or with restore
/// results.
/// </para>
/// <para>
/// <c>SetDataObject(text, copy: true)</c> is used instead of <c>SetText</c> so the text survives
/// after this process exits. Failures are reported as a category string so callers can log them
/// without leaking clipboard contents.
/// </para>
/// </remarks>
internal static class DiagnosticClipboard
{
    private const int Attempts = 4;
    private const int RetryDelayMilliseconds = 60;

    /// <summary>Attempts the copy. Returns false and sets <paramref name="errorCategory"/> on failure.</summary>
    internal static bool TryCopy(string? text, out string errorCategory)
    {
        errorCategory = "";
        if (string.IsNullOrEmpty(text))
        {
            errorCategory = "clipboard_empty_text";
            return false;
        }

        for (int attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, true);
                return true;
            }
            catch (Exception ex) when (ex is COMException or ExternalException or ThreadStateException)
            {
                errorCategory = Categorize(ex);
                if (attempt == Attempts)
                    return false;
            }

            // Yield briefly so an owning process can release the clipboard.
            Thread.Sleep(RetryDelayMilliseconds);
        }

        return false;
    }

    private static string Categorize(Exception exception) => exception switch
    {
        ThreadStateException => "clipboard_wrong_thread_state",
        COMException => "clipboard_unavailable",
        _ => "clipboard_error"
    };
}
