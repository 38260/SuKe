using System.Windows.Media;

namespace SuKe.UI;

/// <summary>放大预览中的一张已解码图片和设备说明。</summary>
internal sealed record ScreenPreviewPage(ImageSource Image, string Title, string? Subtitle);

/// <summary>控制图片浏览索引，首尾停止翻页而不循环。</summary>
internal sealed class ScreenPreviewNavigation
{
    internal ScreenPreviewNavigation(int count, int selectedIndex)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        if (selectedIndex < 0 || selectedIndex >= count)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        Count = count;
        Index = selectedIndex;
    }

    internal int Count { get; }
    internal int Index { get; private set; }
    internal bool HasPrevious => Index > 0;
    internal bool HasNext => Index + 1 < Count;

    internal bool Move(int offset)
    {
        long candidate = (long)Index + offset;
        if (candidate < 0 || candidate >= Count) return false;
        Index = (int)candidate;
        return true;
    }
}
