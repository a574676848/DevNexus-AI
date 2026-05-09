namespace DevNexus.Shared.Enums;

/// <summary>
/// 系统经验类型
/// </summary>
public enum ExperienceType
{
    /// <summary>
    /// 普通问答的文本/代码片段
    /// </summary>
    QA = 0,
    
    /// <summary>
    /// 复杂任务的上下文工作包拓扑快照
    /// </summary>
    SwarmDag = 1,

    /// <summary>
    /// 代码修复经验
    /// </summary>
    CodeFix = 2
}
