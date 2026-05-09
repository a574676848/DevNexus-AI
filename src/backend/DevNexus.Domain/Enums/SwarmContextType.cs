namespace DevNexus.Domain.Enums;

/// <summary>
/// Swarm 上下文类型。
/// </summary>
public enum SwarmContextType
{
    /// <summary>
    /// 未分类上下文。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 任务目标与验收标准上下文。
    /// </summary>
    Task = 1,

    /// <summary>
    /// 会话运行状态与中间产物上下文。
    /// </summary>
    State = 2,

    /// <summary>
    /// 用户偏好、历史经验与团队约定上下文。
    /// </summary>
    Memory = 3,

    /// <summary>
    /// 文档、代码、工具输出等证据上下文。
    /// </summary>
    Evidence = 4,

    /// <summary>
    /// 代码仓库与实现范围上下文。
    /// </summary>
    Codebase = 5,

    /// <summary>
    /// 接口与契约上下文。
    /// </summary>
    ApiContract = 6,

    /// <summary>
    /// 数据模型与数据库上下文。
    /// </summary>
    Data = 7,

    /// <summary>
    /// 前端交互与页面上下文。
    /// </summary>
    Frontend = 8,

    /// <summary>
    /// 基础设施与运行环境上下文。
    /// </summary>
    Infrastructure = 9
}
