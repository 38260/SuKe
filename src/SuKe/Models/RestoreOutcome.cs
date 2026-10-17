using System;
using System.Collections.Generic;

namespace SuKe.Models;

/// <summary>
/// 一次恢复操作最终结果的整体状态。每个目标只归入一个状态，各状态数量可核对闭合。
/// </summary>
public enum RestoreOutcomeStatus
{
    /// <summary>已开始执行但没有写下最终结果（例如进程异常退出）。</summary>
    Interrupted,

    /// <summary>全部纳入计划的目标都成功。</summary>
    Succeeded,

    /// <summary>既有成功也有失败、跳过、受限或未确认。</summary>
    PartiallyCompleted,

    /// <summary>失败（存在失败项且没有成功项）。</summary>
    Failed,

    /// <summary>用户取消。</summary>
    Cancelled,

    /// <summary>计划在桌面变化后失效，未套用任何过期动作。</summary>
    StalePlan,

    /// <summary>被安全门拦下（例如无法写入恢复检查点），没有对桌面做任何改动。</summary>
    Blocked,

    /// <summary>旧记录没有保存恢复结果，无法判断成功或失败。</summary>
    Unknown,

    /// <summary>全部目标按策略跳过，没有执行恢复动作。</summary>
    Skipped,
}

/// <summary>按单一内容类型统计的一组数量。</summary>
/// <remarks>
/// <see cref="Succeeded"/> 只对应执行逻辑确认完成的恢复目标，因此各状态之和恒等于
/// <see cref="Planned"/>（未纳入计划的内容不计入目标）。<see cref="Recorded"/> 为 false 时
/// 表示这一类型没有可靠数据，界面必须显示“未记录”而不是补成 0。
/// </remarks>
public sealed record RestoreOutcomeCounts
{
    /// <summary>本次执行计划中的目标数量（不含被取消勾选的条目）。</summary>
    public int Planned { get; set; }

    public int Succeeded { get; set; }

    public int Failed { get; set; }

    /// <summary>因策略或用户选择而跳过。</summary>
    public int Skipped { get; set; }

    public int Cancelled { get; set; }

    /// <summary>受限：条目被阻塞，或整次操作被安全门拦下。</summary>
    public int Blocked { get; set; }

    /// <summary>计划失效。</summary>
    public int Stale { get; set; }

    /// <summary>已请求处理但无法确认最终结果（例如已请求启动但窗口未出现）。</summary>
    public int Unconfirmed { get; set; }

    /// <summary>是否记录了可靠数据；计划中没有该类型目标时为 false。</summary>
    public bool Recorded => Planned > 0;
}

/// <summary>失败或未完成项的结构化原因。</summary>
/// <remarks>
/// 只保存原因码与脱敏后的短说明，绝不写入原始窗口标题、文件路径、URL、命令行或异常全文。
/// </remarks>
public sealed record RestoreOutcomeIssue
{
    /// <summary>稳定的原因码，例如 <c>placement_verification_failed</c>。</summary>
    public string ReasonCode { get; set; } = "";

    /// <summary>归属的内容类型，例如 <c>window</c>、<c>browser_tab</c>。</summary>
    public string ContentType { get; set; } = "";

    /// <summary>相关应用的进程名（已脱敏）；无法确定时为空。</summary>
    public string Subject { get; set; } = "";

    /// <summary>脱敏并截断后的说明。</summary>
    public string Description { get; set; } = "";
}

/// <summary>
/// 一次恢复操作的持久化结果记录。同一 <see cref="OperationId"/> 只会有一条，先写开始、
/// 完成时以同一标识覆盖写入，因此不会重复生成活动。
/// </summary>
public sealed record RestoreOutcomeRecord
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>单条记录中保存的原因条数上限，避免历史记录无限增长。</summary>
    public const int MaximumIssues = 16;

    /// <summary>单条脱敏说明的最大长度。</summary>
    public const int MaximumDescriptionLength = 200;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>本次操作的稳定标识；活动与结果按它关联，不按工作区编号或时间猜测。</summary>
    public string OperationId { get; set; } = "";

    /// <summary>本次操作使用的恢复前检查点标识（未创建检查点时为 null）。</summary>
    public string? CheckpointId { get; set; }

    public string WorkspaceId { get; set; } = "";

    /// <summary>工作区名称；仅在本地界面展示。</summary>
    public string WorkspaceName { get; set; } = "";

    public WorkspaceCheckpointTrigger Trigger { get; set; }

    public DateTime StartedAtUtc { get; set; }

    /// <summary>最终写入时间；仍为 null 表示操作已开始但没有写下结果。</summary>
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>执行耗时（毫秒）。</summary>
    public double DurationMs { get; set; }

    public RestoreOutcomeStatus Status { get; set; } = RestoreOutcomeStatus.Interrupted;

    public RestoreOutcomeCounts Windows { get; set; } = new();

    public RestoreOutcomeCounts BrowserTabs { get; set; } = new();

    public RestoreOutcomeCounts ExplorerTabs { get; set; } = new();

    public RestoreOutcomeCounts TerminalTabs { get; set; } = new();

    public IReadOnlyList<RestoreOutcomeIssue> Issues { get; set; } = Array.Empty<RestoreOutcomeIssue>();

    /// <summary>最终结果是否已经写入。</summary>
    public bool IsComplete => EndedAtUtc is not null;
}
