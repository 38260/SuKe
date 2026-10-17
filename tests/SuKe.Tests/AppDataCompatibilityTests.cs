using SuKe.Services;

namespace SuKe.Tests;

public class AppDataCompatibilityTests
{
    [Fact]
    public void Application_and_test_assemblies_use_the_current_identity()
    {
        Assert.Equal("SuKe", typeof(App).Assembly.GetName().Name);
        Assert.Equal("SuKe.App", typeof(App).FullName);
        Assert.Equal("SuKe.Tests", typeof(AppDataCompatibilityTests).Assembly.GetName().Name);
    }

    [Fact]
    public void A_new_installation_uses_the_current_product_directory_without_creating_it()
    {
        using var directory = new TestDirectory();
        string target = AppDataPaths.ResolveDataDirectory(directory.Path);
        Assert.Equal(Path.Combine(directory.Path, "SuKe"), target);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Existing_user_data_is_used_in_place_without_copying_or_deleting_it()
    {
        using var directory = new TestDirectory();
        string legacy = Path.Combine(directory.Path, AppDataPaths.LegacyProductName);
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "original settings");
        Assert.Equal(legacy, AppDataPaths.ResolveDataDirectory(directory.Path));
        Assert.Equal("original settings", File.ReadAllText(Path.Combine(legacy, "settings.json")));
        Assert.False(Directory.Exists(Path.Combine(directory.Path, "SuKe")));
    }

    [Fact]
    public void Current_data_wins_and_both_existing_data_sets_remain_untouched()
    {
        using var directory = new TestDirectory();
        string legacy = Path.Combine(directory.Path, AppDataPaths.LegacyProductName);
        string current = Path.Combine(directory.Path, "SuKe");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(current);
        File.WriteAllText(Path.Combine(current, "settings.json"), "current settings");
        Assert.Equal(current, AppDataPaths.ResolveDataDirectory(directory.Path));
        Assert.True(Directory.Exists(legacy));
        Assert.Equal("current settings", File.ReadAllText(Path.Combine(current, "settings.json")));
    }

    [Fact]
    public void A_settings_service_uses_its_explicit_storage_directory()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var settings = new SettingsService(storage);
        settings.Settings.NotificationsEnabled = false;
        settings.Save();
        Assert.True(File.Exists(Path.Combine(directory.Path, "settings.json")));
        Assert.False(new SettingsService(storage).Settings.NotificationsEnabled);
    }
}
