using SuKe.Models;
using SuKe.Services;

namespace SuKe.Tests;

public class WorkspacePreviewStoreTests
{
    [Fact]
    public void Save_replaces_all_screens_and_ignores_a_stale_capture()
    {
        using var directory = new TestDirectory();
        var store = new WorkspacePreviewStore(directory.Path);
        var workspace = new WorkspaceSnapshot
        {
            Name = "Preview",
            SavedAt = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc)
        };

        store.Save(workspace,
        [new ScreenPreview(0, [1, 2]), new ScreenPreview(1, [3, 4])]);
        Assert.True(store.HasPreview(workspace));
        Assert.Equal(new[] { 0, 1 }, store.Load(workspace).Select(image => image.ScreenIndex));

        workspace.SavedAt = workspace.SavedAt.AddMinutes(1);
        Assert.False(store.HasPreview(workspace));
        Assert.Empty(store.Load(workspace));

        store.Save(workspace, [new ScreenPreview(1, [5, 6])]);
        ScreenPreview replacement = Assert.Single(store.Load(workspace));
        Assert.Equal(1, replacement.ScreenIndex);
        Assert.Equal(new byte[] { 5, 6 }, replacement.Png);
    }

    [Fact]
    public void Deleting_a_workspace_removes_its_screen_preview()
    {
        using var directory = new TestDirectory();
        var storage = new StorageService(directory.Path);
        var workspace = new WorkspaceSnapshot
        {
            Name = "Preview",
            SavedAt = DateTime.UtcNow
        };
        storage.SaveWorkspace(workspace);
        storage.Previews.Save(workspace, [new ScreenPreview(0, [1, 2, 3])]);

        storage.DeleteWorkspace(workspace);

        Assert.False(storage.Previews.HasPreview(workspace));
        Assert.Empty(storage.LoadAllWorkspaces());
    }
}
