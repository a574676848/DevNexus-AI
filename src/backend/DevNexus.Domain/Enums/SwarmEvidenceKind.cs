namespace DevNexus.Domain.Enums;

/// <summary>
/// Swarm 证据类型。
/// </summary>
public enum SwarmEvidenceKind
{
    /// <summary>
    /// 需求说明。
    /// </summary>
    Requirement = 0,

    /// <summary>
    /// 仓库代码。
    /// </summary>
    SourceCode = 1,

    /// <summary>
    /// API 文档。
    /// </summary>
    ApiDocument = 2,

    /// <summary>
    /// 数据库结构。
    /// </summary>
    DatabaseSchema = 3,

    /// <summary>
    /// 工具执行结果。
    /// </summary>
    ToolOutput = 4,

    /// <summary>
    /// 用户补充说明。
    /// </summary>
    UserInput = 5,

    /// <summary>
    /// 系统经验与历史记忆。
    /// </summary>
    Memory = 6
}
