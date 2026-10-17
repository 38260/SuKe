using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>
/// 一次恢复操作的结果记录：开始与完成合并写入同一条记录，按稳定的操作标识幂等覆盖。
/// </summary>
/// <remarks>
/// <para>
/// 与工作区、检查点、截图一样落在同一个存储根目录下的独立子目录，每份记录一个 JSON 文件，
/// 沿用 <see cref="IAtomicFileWriter"/> 的原子写入（先写同目录临时文件再替换），因此不需要数据库，
/// 也不会出现写了一半的记录。同一 <c>OperationId</c> 永远只有一个文件，所以完成时的写入是幂等的，
/// 不会重复生成活动。
/// </para>
/// <para>
/// 开始即落盘，是“异常退出”可以显示为中断/结果未知的前提：没有最终结果的记录在读取侧一律按
/// <see cref="RestoreOutcomeStatus.Interrupted"/> 处理，绝不推断成功。
/// </para>
/// <para>
/// 任何写入失败都不会抛出到恢复流程：失败只记录日志并置位 <see cref="LastWriteFailed"/>，
/// 因此历史记录保存失败不会影响已经发生的真实恢复结果。
/// </para>
/// </remarks>
internal sealed class RestoreOutcomeStore
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

    internal RestoreOutcomeStore(string baseDirectory, IAtomicFileWriter writer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _directory = Path.Combine(baseDirectory, "restore-results");
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>最近一次写入是否失败。界面据此提示“历史记录保存失败”。</summary>
    internal static bool LastWriteFailed { get; private set; }

    /// <summary>本实例最近一次写入是否失败；用于在测试中确定性地观察写入结果。</summary>
    internal bool WriteFailed { get; private set; }

    /// <summary>写下一条最终结果后触发；活动页据此在已打开时刷新。</summary>
    internal static event Action? OutcomeRecorded;

    /// <summary>写下“操作已开始”，返回本次操作的稳定标识。</summary>
    internal string Begin(RestorePlan plan, DateTime startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        string operationId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        Write(RestoreOutcomeProjector.ProjectStarted(operationId, plan, startedAtUtc));
        return operationId;
    }

    /// <summary>覆盖写入最终结果；同一操作标识重复调用不会产生第二条记录。</summary>
    internal void Complete(string operationId, RestorePlan plan, RestoreExecutionResult result, DateTime startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(result);

        RestoreOutcomeRecord record = RestoreOutcomeProjector.Project(
            operationId,
            plan,
            result,
            startedAtUtc,
            DateTime.UtcNow);
        if (Write(record)) OutcomeRecorded?.Invoke();
    }

    /// <summary>读取全部已记录结果，最新在前。不可读的单条记录只跳过，不影响其余记录。</summary>
    internal IReadOnlyList<RestoreOutcomeRecord> Read()
    {
        if (!Directory.Exists(_directory)) return Array.Empty<RestoreOutcomeRecord>();

        var records = new List<RestoreOutcomeRecord>();
        foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                RestoreOutcomeRecord? record =
                    JsonSerializer.Deserialize<RestoreOutcomeRecord>(File.ReadAllText(file), JsonOptions);
                if (record is null || string.IsNullOrWhiteSpace(record.OperationId)) continue;
                // 没有最终结果的记录一律按“中断/结果未知”呈现。
                records.Add(record with
                {
                    Status = record.IsComplete ? record.Status : RestoreOutcomeStatus.Interrupted,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                AppLogger.Warn(
                    "activity.outcome_unreadable",
                    "Skipped an unreadable restore result record",
                    LogField.Path("recordPath", file),
                    LogField.Public("errorCategory", "restore_outcome_read"));
            }
        }

        return records
            .OrderByDescending(record => record.StartedAtUtc)
            .ToArray();
    }

    private bool Write(RestoreOutcomeRecord record)
    {
        try
        {
            if (!Guid.TryParseExact(record.OperationId, "N", out _))
                throw new InvalidDataException("The operation identifier must be a plain GUID.");

            _writer.WriteAllText(
                Path.Combine(_directory, record.OperationId + ".json"),
                JsonSerializer.Serialize(record, JsonOptions));
            LastWriteFailed = false;
            WriteFailed = false;
            Prune();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or NotSupportedException)
        {
            // 写入失败不得影响已经发生的恢复结果。
            LastWriteFailed = true;
            WriteFailed = true;
            AppLogger.Warn(
                "activity.outcome_persist_failed",
                "Could not persist a restore result record; the live session is unaffected",
                ex,
                LogField.Public("errorCategory", "restore_outcome_write"));
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
