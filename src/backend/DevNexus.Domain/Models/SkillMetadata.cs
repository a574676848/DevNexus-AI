using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Models;

/// <summary>
/// Skill 元数据（从 SKILL.md YAML frontmatter 解析）
/// 对齐 Agent Skills 官方规范：https://agentskills.io/specification
/// </summary>
public class SkillMetadata
{
    // ==================== 官方规范必填字段 ====================

    /// <summary>
    /// 唯一标识名称（小写+连字符，1-64 字符，须匹配目录名）
    /// 正则: ^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述（何时使用、做什么，1-1024 字符）</summary>
    public string Description { get; set; } = string.Empty;

    // ==================== 官方规范可选字段 ====================

    /// <summary>许可证信息</summary>
    public string? License { get; set; }

    /// <summary>兼容性说明（环境要求等）</summary>
    public string? Compatibility { get; set; }

    /// <summary>自定义元数据键值对</summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>预批准工具列表（空格分隔字符串）</summary>
    public string? AllowedTools { get; set; }

    // ==================== DevNexus 扩展字段 ====================

    /// <summary>类型: PromptOnly | Script | PluginBound | Hybrid</summary>
    public SkillType Type { get; set; } = SkillType.PromptOnly;

    /// <summary>绑定的 Plugin 名称列表</summary>
    public List<string> Plugins { get; set; } = new();

    /// <summary>关联的脚本列表</summary>
    public List<SkillScript> Scripts { get; set; } = new();

    /// <summary>是否自动触发（SkillMatcher 根据消息自动匹配）</summary>
    public bool AutoTrigger { get; set; } = true;

    /// <summary>是否需要用户上下文（userId/sessionId）</summary>
    public bool RequiresContext { get; set; }

    /// <summary>优先级 (0-100)，匹配冲突时高优先级优先</summary>
    public int Priority { get; set; } = 50;

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>辅助匹配标签</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>正则触发模式（快速匹配层）</summary>
    public List<string> TriggerPatterns { get; set; } = new();

    /// <summary>版本</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>作者</summary>
    public string Author { get; set; } = string.Empty;

    // ==================== 运行时字段（不从 YAML 解析） ====================

    /// <summary>Skill 目录的绝对路径</summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>SKILL.md 正文内容（L2 指令，惰性加载后缓存）</summary>
    public string? InstructionContent { get; set; }

    /// <summary>所属作用域: BuiltIn | Shared | User</summary>
    public SkillScope Scope { get; set; }

    /// <summary>参考链接或标识列表</summary>
    public List<string> References { get; set; } = new();

    /// <summary>最后修改时间 (SKILL.md 的修改时间)</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.MinValue;

    /// <summary>所属用户 ID（User 级 Skill 时有值）</summary>
    public Guid? OwnerUserId { get; set; }
}

/// <summary>
/// Skill 关联脚本定义
/// </summary>
public class SkillScript
{
    /// <summary>脚本相对路径（相对于 Skill 目录）</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>运行时: pwsh | python | bash | node</summary>
    public string Runtime { get; set; } = "pwsh";

    /// <summary>脚本描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>超时时间（毫秒），默认 30000</summary>
    public int Timeout { get; set; } = 30000;
}
