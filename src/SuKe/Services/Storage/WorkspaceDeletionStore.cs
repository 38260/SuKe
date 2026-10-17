using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>
/// 命名工作区删除记录：每删除一个工作区落一份 JSON，活动流据此补上那段历史。
/// </summary>
/// <remarks>
/// <para>
/// 与工作区、检查点、恢复结果一样落在同一存储根目录下的独立子目录，沿用
/// <see cref="IAtomicFileWriter"/> 的原子写入（先写同目录临时文件再替换），
/// 因此不需要数据库，也不会出现写了一半的记录。
/// </para>
/// <para>
/// 删除工作区会连带删掉工作区文档，而活动流只投影现存文档——这条记录是唯一还能证明
/// “曾经存在过这个工作区”的东西，所以它连同最后一次保存的内容摘要一起落盘。
/// </para>
/// <para>
/// 任何写入失败都不会抛出到删除流程：失败只记录日志并置位 <see cref="LastWriteFailed"/>，
/// 因此历史记录保存失败不影响工作区本身已经被删除这一事实。
/// </para>
/// </remarks>
internal sealed class WorkspaceDeletionStore
{
    /// <summary>保留的记录条数上限。</summary>
    internal const int MaximumRecords = 200;

    /// <summary>保留的最长时间。</summary>
    internal static readonly TimeSpan MaximumAge = TimeSpan.FromDays(365);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;
    private readonly IAtomicFileWriter _writer;

    internal WorkspaceDeletionStore(string baseDirectory, IAtomicFileWriter writer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _directory = Path.Combine(baseDirectory, "deletions");
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>最近一次写入是否失败。界面据此提示“历史记录保存失败”。</summary>
    internal static bool LastWriteFailed { get; private set; }

    /// <summary>本实例最近一次写入是否失败；用于在测试中确定性地观察写入结果。</summary>
    internal bool WriteFailed { get; private set; }

    /// <summary>写入一条删除记录后触发；活动页据此在已打开时刷新。</summary>
    internal static event Action? DeletionRecorded;

    /// <summary>在工作区已经从磁盘删除之后，记下这件事。</summary>
    internal void Record(WorkspaceSnapshot snapshot, DateTime deletedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        WorkspaceDeletionRecord record = WorkspaceDeletionProjector.Project(snapshot, deletedAtUtc);
        if (Write(record)) DeletionRecorded?.Invoke();
    }

    /// <summary>读取全部删除记录，最新在前。不可读的单条记录只跳过，不影响其余记录。</summary>
    internal IReadOnlyList<WorkspaceDeletionRecord> Read()
    {
        if (!Directory.Exists(_directory)) return Array.Empty<WorkspaceDeletionRecord>();

        var records = new List<WorkspaceDeletionRecord>();
        foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                WorkspaceDeletionRecord? record =
                    JsonSerializer.Deserialize<WorkspaceDeletionRecord>(File.ReadAllText(file), JsonOptions);
                if (record is null || string.IsNullOrWhiteSpace(record.DeletionId)) continue;
                records.Add(record);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                AppLogger.Warn(
                    "activity.deletion_unreadable",
                    "Skipped an unreadable workspace deletion record",
                    LogField.Path("recordPath", file),
                    LogField.Public("errorCategory", "workspace_deletion_read"));
            }
        }

        return records
            .OrderByDescending(record => record.DeletedAtUtc)
            .ToArray();
    }

    private bool Write(WorkspaceDeletionRecord record)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(record.DeletionId))
                throw new InvalidDataException("The deletion identifier must not be empty.");

            _writer.WriteAllText(
                Path.Combine(_directory, record.DeletionId + ".json"),
                JsonSerializer.Serialize(record, JsonOptions));
            LastWriteFailed = false;
            WriteFailed = false;
            Prune();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or NotSupportedException)
        {
            // 写入失败不得影响已经发生的删除。
            LastWriteFailed = true;
            WriteFailed = true;
            AppLogger.Warn(
                "activity.deletion_persist_failed",
                "Could not persist a workspace deletion record; the workspace itself is already deleted",
                ex,
                LogField.Public("errorCategory", "workspace_deletion_write"));
            return false;
        }
    }

    /// <summary>按条数与时间上限裁剪历史记录，忽略原子写入留下的临时文件。</summary>
    private void Prune()
    {
        try
        {
            DateTime cutoff = DateTime.UtcNow - MaximumAge;
            var entries = new List<(string Path, DateTime WrittenAtUtc)>();
            foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    entries.Add((file, File.GetLastWriteTimeUtc(file)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            entries.Sort((left, right) => right.WrittenAtUtc.CompareTo(left.WrittenAtUtc));
            for (int index = 0; index < entries.Count; index++)
            {
                bool beyondCount = index >= MaximumRecords;
                bool tooOld = entries[index].WrittenAtUtc < cutoff;
                if (!beyondCount && !tooOld) continue;
                try
                {
                    File.Delete(entries[index].Path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
