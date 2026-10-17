using System.Text.Json.Nodes;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

public class AppearanceDefaultsTests
{
    [Fact]
    public void New_settings_start_in_light_mode()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var settings = new SettingsService(Path.Combine(directory.Path, "settings.json"), storage);
        Assert.Equal(AppThemeMode.Light, new AppSettings().ThemeMode);
        Assert.Equal(AppThemeMode.Light, settings.Settings.ThemeMode);
    }

    [Fact]
    public void A_document_without_a_theme_field_uses_light_mode()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        string path = Path.Combine(directory.Path, "settings.json");
        var settings = new SettingsService(path, storage);
        settings.Save();
        JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.True(document.AsObject().Remove("themeMode"));
        File.WriteAllText(path, document.ToJsonString());
        var reopened = new SettingsService(path, storage);
        Assert.Null(reopened.LastLoadError);
        Assert.Equal(AppThemeMode.Light, reopened.Settings.ThemeMode);
    }

    [Theory]
    [InlineData(AppThemeMode.Dark)]
    [InlineData(AppThemeMode.FollowSystem)]
    public void Explicit_saved_choices_are_preserved(AppThemeMode mode)
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        string path = Path.Combine(directory.Path, "settings.json");
        var settings = new SettingsService(path, storage);
        settings.Settings.ThemeMode = mode;
        settings.Save();
        Assert.Equal(mode, new SettingsService(path, storage).Settings.ThemeMode);
    }
}
