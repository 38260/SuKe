using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SuKe.UI.Shell;

/// <summary>复用原图标提取与去重逻辑，匹配参考图的大图标行。</summary>
internal sealed class ReferenceAppIconsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var badges = (IEnumerable<FrameworkElement>)new WorkspaceAppIconsConverter()
            .Convert(value, targetType, parameter, culture);
        foreach (FrameworkElement badge in badges)
        {
            badge.Width = 36;
            badge.Height = 36;
            badge.Margin = new Thickness(0, 0, 18, 0);
            if (badge is Border { Child: TextBlock text }) text.FontSize = 20;
        }
        return badges;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
