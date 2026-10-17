using System.Text.Json.Nodes;
using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

public class TypographySettingsTests
{
    [Fact]
    public void Legacy_settings_default_to_normal_text_and_saved_size_reopens()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        string path = Path.Combine(directory.Path, "settings.json");
        var settings = new SettingsService(path, storage);
        Assert.Equal(1, settings.Settings.UiFontScale);
        settings.Settings.UiFontScale = 1.25;
        settings.Save();
        Assert.Equal(1.25, new SettingsService(path, storage).Settings.UiFontScale);
        JsonNode json = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.True(json.AsObject().Remove("uiFontScale"));
        File.WriteAllText(path, json.ToJsonString());
        Assert.Equal(1, new SettingsService(path, storage).Settings.UiFontScale);
    }

    [Theory]
    [InlineData(-1, 0.8)]
    [InlineData(0, 0.8)]
    [InlineData(5, 1.5)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void Invalid_text_scales_remain_usable(double value, double expected) =>
        Assert.Equal(expected, new AppSettings { UiFontScale = value }.UiFontScale);
}
