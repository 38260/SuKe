using System.Text.Json;
using System.Security.Cryptography;
using SuKe.Services;

namespace SuKe.Tests;

public class BrowserIntegrationServiceTests
{
    [Fact]
    public void Chromium_setup_uses_the_identity_of_the_bundled_connector()
    {
        using JsonDocument manifest = ReadManifest("browser-extension");
        byte[] key = Convert.FromBase64String(manifest.RootElement.GetProperty("key").GetString()!);
        byte[] digest = SHA256.HashData(key);
        string id = string.Concat(digest.Take(16).SelectMany(value => new[]
        {
            (char)('a' + (value >> 4)), (char)('a' + (value & 15)),
        }));
        Assert.Equal(BrowserIntegrationService.ChromeExtensionId, id);
        Assert.Equal("chrome://extensions/", BrowserIntegrationService.ChromiumManagementUrl);
    }

    [Fact]
    public void Firefox_setup_uses_the_bundled_add_on_identity_and_management_page()
    {
        using JsonDocument manifest = ReadManifest("firefox-extension");
        Assert.Equal(BrowserIntegrationService.FirefoxExtensionId,
            manifest.RootElement.GetProperty("browser_specific_settings").GetProperty("gecko").GetProperty("id").GetString());
        Assert.Equal("about:addons", BrowserIntegrationService.FirefoxManagementUrl);
    }

    [Fact]
    public void Firefox_native_host_manifest_uses_allowed_extensions()
    {
        using JsonDocument manifest = JsonDocument.Parse(
            BrowserIntegrationService.CreateNativeHostManifest(
                @"C:\Program Files\SuKe\SuKe.exe",
                BrowserConnectorKind.Firefox));

        JsonElement root = manifest.RootElement;
        Assert.Equal(BrowserIntegrationService.HostName, root.GetProperty("name").GetString());
        Assert.Equal(
            BrowserIntegrationService.FirefoxExtensionId,
            root.GetProperty("allowed_extensions")[0].GetString());
        Assert.False(root.TryGetProperty("allowed_origins", out _));
    }

    [Fact]
    public void Chromium_native_host_manifest_keeps_the_exact_connector_origin()
    {
        using JsonDocument manifest = JsonDocument.Parse(
            BrowserIntegrationService.CreateNativeHostManifest(
                @"C:\Program Files\SuKe\SuKe.exe",
                BrowserConnectorKind.Chromium));

        JsonElement root = manifest.RootElement;
        Assert.Equal(
            $"chrome-extension://{BrowserIntegrationService.ChromeExtensionId}/",
            root.GetProperty("allowed_origins")[0].GetString());
        Assert.False(root.TryGetProperty("allowed_extensions", out _));
    }

    [Fact]
    public void Native_host_launch_arguments_route_each_browser_family()
    {
        Assert.Equal(
            BrowserSessionBridge.FirefoxPipeName,
            BrowserIntegrationService.ResolveNativeMessagingPipe([
                @"C:\Users\Example\native-host-manifest-firefox.json",
                BrowserIntegrationService.FirefoxExtensionId]));
        Assert.Equal(
            BrowserSessionBridge.ChromiumPipeName,
            BrowserIntegrationService.ResolveNativeMessagingPipe([
                $"chrome-extension://{BrowserIntegrationService.ChromeExtensionId}/"]));
        Assert.Null(BrowserIntegrationService.ResolveNativeMessagingPipe(["unrelated"]));
    }

    private static JsonDocument ReadManifest(string folder)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SuKe.sln")))
                return JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, folder, "manifest.json")));
        throw new DirectoryNotFoundException("The project root was not found above the test output.");
    }
}
