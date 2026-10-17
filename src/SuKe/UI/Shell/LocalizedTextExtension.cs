using System.Windows.Markup;
using SuKe.Localization;

namespace SuKe.UI.Shell;

/// <summary>在模板实例化时解析界面文案，不依赖 Loaded 遍历时机。</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocalizedTextExtension : MarkupExtension
{
    /// <summary>创建指定源文案的本地化标记。</summary>
    public LocalizedTextExtension(string text) => Text = text;

    /// <summary>英文源文案；用户数据不经过此标记。</summary>
    public string Text { get; }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Text);
}
