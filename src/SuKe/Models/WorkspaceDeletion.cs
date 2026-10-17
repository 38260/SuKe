using System;

namespace SuKe.Models;

/// <summary>
/// 一次命名工作区删除的持久化记录。
/// </summary>
/// <remarks>
/// <para>
/// 删除工作区会连带删掉工作区文档本身，而活动流是从现存文档投影出来的——
/// 于是那条「已保存」记录会随之消失，历史看起来就像凭空少了一段。
/// 这份记录就是补上这个缺口的：它记下工作区叫什么、最后一次保存了什么，
/// 让时间线上「曾经存在过这个工作区」这件事仍然可查。
/// </para>
/// <para>
/// 它只陈述删除这一事实与被删工作区的规模，不推断任何恢复结果，
/// 也不携带路径、窗口标题、URL 或命令行。
/// </para>
/// </remarks>
public sealed record WorkspaceDeletionRecord
{
    /// <summary>稳定的删除标识，同时用作文件名；重复记录同一件事不会产生第二条。</summary>
    public string DeletionId { get; init; } = "";

    /// <summary>被删除工作区的稳定标识。工作区已不存在，仅用于串联历史。</summary>
    public string WorkspaceId { get; init; } = "";

    /// <summary>删除时的工作区名称。这是用户数据，只在本地界面展示。</summary>
    public string WorkspaceName { get; init; } = "";

    /// <summary>删除发生的时刻（UTC），即这条活动在时间线上的位置。</summary>
    public DateTime DeletedAtUtc { get; init; }

    /// <summary>被删除工作区最后一次保存的时刻（UTC）；不可知时为 null。</summary>
    public DateTime? SavedAtUtc { get; init; }

    /// <summary>被删除工作区当时记录的窗口数量。</summary>
    public int WindowCount { get; init; }

    /// <summary>被删除工作区当时记录的显示器数量。</summary>
    public int MonitorCount { get; init; }

    /// <summary>被删除工作区最后一次保存内容的脱敏摘要；无快照时为 null。</summary>
    public WorkspaceActivitySnapshot? Snapshot { get; init; }
}
