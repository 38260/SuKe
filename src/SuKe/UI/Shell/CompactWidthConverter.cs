using System.Globalization;
using System.Windows.Data;

namespace SuKe.UI.Shell;

/// <summary>将窗口宽度投影为布局紧凑状态，不保存额外配置。</summary>
internal sealed class CompactWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width && width < double.Parse((string)parameter, CultureInfo.InvariantCulture);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
