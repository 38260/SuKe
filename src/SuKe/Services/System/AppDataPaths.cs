using System.IO;

namespace SuKe.Services;

/// <summary>新安装使用项目名称；已有数据原地沿用，不搬动或覆盖用户文件。</summary>
internal static class AppDataPaths
{
    internal const string ProductName = "SuKe";
    internal const string LegacyProductName = "WindowAnchor";

    internal static string RoamingDirectory { get; } = ResolveDataDirectory(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    internal static string ResolveDataDirectory(string parentDirectory)
    {
        string preferred = Path.Combine(parentDirectory, ProductName);
        string legacy = Path.Combine(parentDirectory, LegacyProductName);
        bool hasCurrentData = File.Exists(Path.Combine(preferred, "settings.json")) ||
            Directory.Exists(Path.Combine(preferred, "workspaces"));
        return hasCurrentData || !Directory.Exists(legacy) ? preferred : legacy;
    }
}
