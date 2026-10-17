using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>命名工作区的本地屏幕预览附件；不进入工作区 JSON 和传输文件。</summary>
internal sealed class WorkspacePreviewStore
{
    private const int MaximumScreens = 16;
    private const int MaximumImageBytes = 50 * 1024 * 1024;
    private readonly string _directory;

    internal WorkspacePreviewStore(string baseDirectory)
    {
        _directory = Path.Combine(baseDirectory, "previews");
    }

    internal void Save(WorkspaceSnapshot workspace, IReadOnlyList<ScreenPreview> images)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            Delete(workspace.WorkspaceId);
            return;
        }
        if (images.Count > MaximumScreens ||
            images.Any(image => image.ScreenIndex < 0 || image.ScreenIndex >= MaximumScreens ||
                image.Png.Length == 0 || image.Png.Length > MaximumImageBytes) ||
            images.Select(image => image.ScreenIndex).Distinct().Count() != images.Count)
        {
            throw new InvalidDataException("Screen preview data is outside the supported limits.");
        }

        Directory.CreateDirectory(_directory);
        string path = GetPath(workspace.WorkspaceId);
        string temporaryPath = Path.Combine(_directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.WriteThrough))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    WriteEntry(archive, "saved-at.txt",
                        Encoding.ASCII.GetBytes(workspace.SavedAt.Ticks.ToString(CultureInfo.InvariantCulture)));
                    foreach (ScreenPreview image in images.OrderBy(image => image.ScreenIndex))
                        WriteEntry(archive, $"screen-{image.ScreenIndex}.png", image.Png);
                }
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    internal bool HasPreview(WorkspaceSnapshot workspace)
    {
        try
        {
            using ZipArchive? archive = OpenCurrent(workspace);
            return archive != null && archive.Entries.Any(entry =>
                entry.Name.StartsWith("screen-", StringComparison.Ordinal) &&
                entry.Name.EndsWith(".png", StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    internal IReadOnlyList<ScreenPreview> Load(WorkspaceSnapshot workspace)
    {
        try
        {
            using ZipArchive? archive = OpenCurrent(workspace);
            if (archive == null) return [];

            var images = new List<ScreenPreview>();
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.Name.StartsWith("screen-", StringComparison.Ordinal) ||
                    !entry.Name.EndsWith(".png", StringComparison.Ordinal) ||
                    entry.FullName != entry.Name ||
                    !int.TryParse(entry.Name.AsSpan(7, entry.Name.Length - 11), out int index) ||
                    index < 0 || index >= MaximumScreens)
                    continue;
                if (entry.Length <= 0 || entry.Length > MaximumImageBytes || images.Count >= MaximumScreens)
                    throw new InvalidDataException("Screen preview exceeds the supported limits.");
                using Stream source = entry.Open();
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                images.Add(new ScreenPreview(index, buffer.ToArray()));
            }
            return images.OrderBy(image => image.ScreenIndex).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return [];
        }
    }

    internal void Delete(string workspaceId)
    {
        string path = GetPath(workspaceId);
        if (File.Exists(path)) File.Delete(path);
    }

    private ZipArchive? OpenCurrent(WorkspaceSnapshot workspace)
    {
        string path = GetPath(workspace.WorkspaceId);
        if (!File.Exists(path)) return null;
        var archive = ZipFile.OpenRead(path);
        try
        {
            ZipArchiveEntry? marker = archive.GetEntry("saved-at.txt");
            if (marker == null || marker.Length > 32) return DisposeAndReturnNull(archive);
            using StreamReader reader = new(marker.Open(), Encoding.ASCII);
            if (!long.TryParse(reader.ReadToEnd(), NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) ||
                ticks != workspace.SavedAt.Ticks)
                return DisposeAndReturnNull(archive);
            return archive;
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private static ZipArchive? DisposeAndReturnNull(ZipArchive archive)
    {
        archive.Dispose();
        return null;
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        using Stream target = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
        target.Write(bytes);
    }

    private string GetPath(string workspaceId)
    {
        if (!Guid.TryParse(workspaceId, out Guid parsed))
            throw new InvalidDataException("WorkspaceId must be a GUID.");
        return Path.Combine(_directory, $"{parsed:D}.zip");
    }
}
