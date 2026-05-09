namespace DevNexus.Shared.Enums;

/// <summary>
/// Skill 类型枚举
/// </summary>
public enum SkillType
{
    /// <summary>纯指令型：仅包含提示词指令，无绑定 Plugin 或脚本</summary>
    PromptOnly,

    /// <summary>脚本型：通过 HostService 执行外部脚本</summary>
    Script,

    /// <summary>绑定型：绑定到特定 Semantic Kernel Plugin</summary>
    PluginBound,

    /// <summary>混合型：同时绑定 Plugin 和脚本</summary>
    Hybrid
}

/// <summary>
/// Skill 作用域枚举
/// </summary>
public enum SkillScope
{
    /// <summary>内置 Skill（随项目发布）</summary>
    BuiltIn,

    /// <summary>全局共享 Skill（管理员创建）</summary>
    Shared,

    /// <summary>用户私有 Skill</summary>
    User
}

/// <summary>
/// Skill 匹配方式枚举
/// </summary>
public enum SkillMatchMethod
{
    /// <summary>用户显式选择</summary>
    ExplicitSelection,

    /// <summary>正则模式命中</summary>
    TriggerPattern,

    /// <summary>标签/关键词匹配</summary>
    KeywordTag,

    /// <summary>语义向量相似度</summary>
    SemanticSimilarity
}
