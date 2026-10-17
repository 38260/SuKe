using System;
using System.Globalization;
using SuKe.Models;

namespace SuKe.Services;

/// <summary>
/// 把一份正在被删除的工作区快照投影成可持久化的删除记录。
/// </summary>
/// <remarks>
/// 纯函数：只读取快照与删除时刻，不做任何 I/O，也不回头再查一次磁盘上的工作区文档。
/// 删除记录刻意只在删除成功之后生成（见 <see cref="WorkspaceDeletionStore"/>），
/// 以免清理失败、工作区仍保留时留下一条与事实不符的“已删除”。
/// </remarks>
internal static class WorkspaceDeletionProjector
{
    /// <summary>生成删除标识：时间戳前缀保证文件名可读、可排序，短 GUID 保证不撞车。</summary>
    internal static string CreateDeletionId(DateTime deletedAtUtc) =>
        deletedAtUtc.ToUniversalTime().ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture) +
        "-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

    internal static WorkspaceDeletionRecord Project(WorkspaceSnapshot snapshot, DateTime deletedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        DateTime utc = deletedAtUtc.ToUniversalTime();

        return new WorkspaceDeletionRecord
        {
            DeletionId    = CreateDeletionId(utc),
            WorkspaceId   = snapshot.WorkspaceId,
            WorkspaceName = snapshot.Name,
            DeletedAtUtc  = utc,
            SavedAtUtc    = snapshot.SavedAt == default
                ? null
                : snapshot.SavedAt.ToUniversalTime(),
            WindowCount   = snapshot.Entries.Count,
            MonitorCount  = snapshot.Monitors.Count,
            Snapshot      = WorkspaceActivitySnapshotBuilder.Describe(snapshot),
        };
    }
}
