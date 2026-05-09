using System;
using System.Collections.Generic;
using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 上下文工作包持久化实体
/// </summary>
public class ContextWorkPackageRecord : AuditableEntity
{
    /// <summary>
    /// 内部工作包 ID
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// 工作包标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 工作包详细描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 执行角色 (例如 Architect, Coder)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 上下文类型。
    /// </summary>
    public string ContextType { get; set; } = string.Empty;

    /// <summary>
    /// 执行策略。
    /// </summary>
    public string ExecutionStrategy { get; set; } = string.Empty;

    /// <summary>
    /// 工作包状态
    /// </summary>
    public SwarmTaskStatus Status { get; set; }

    /// <summary>
    /// 依赖的工作包 ID 列表 (JSON 存储)
    /// </summary>
    public List<string> Dependencies { get; set; } = new List<string>();

    /// <summary>
    /// 执行结果
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 最近失败原因。
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 最近执行体名称。
    /// </summary>
    public string? ExecutorName { get; set; }

    /// <summary>
    /// 最近命令行摘要。
    /// </summary>
    public string? CommandLine { get; set; }

    /// <summary>
    /// 最近工作目录。
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 最近执行报告 Artifact 标识。
    /// </summary>
    public Guid? ExecutionReportArtifactId { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 逻辑单元列表 (JSON 存储，用于冲突检测)
    /// </summary>
    public List<string> LogicalUnits { get; set; } = new List<string>();

    /// <summary>
    /// 输入契约列表 (JSON 存储)。
    /// </summary>
    public List<string> InputContracts { get; set; } = new List<string>();

    /// <summary>
    /// 输出契约列表 (JSON 存储)。
    /// </summary>
    public List<string> OutputContracts { get; set; } = new List<string>();

    /// <summary>
    /// 拥有的文件范围 (JSON 存储)。
    /// </summary>
    public List<string> OwnedFiles { get; set; } = new List<string>();

    /// <summary>
    /// 拥有的符号范围 (JSON 存储)。
    /// </summary>
    public List<string> OwnedSymbols { get; set; } = new List<string>();

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    public Guid ContextSwarmSessionId { get; set; }

    /// <summary>
    /// 所属会话
    /// </summary>
    public virtual ContextSwarmSession ContextSwarmSession { get; set; } = null!;
}
