using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// Skill 元数据 DTO（用于 API 响应）
/// </summary>
public class SkillDto
{
    /// <summary>Skill 名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>类型</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>作用域</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; }

    /// <summary>优先级</summary>
    public int Priority { get; set; }

    /// <summary>标签</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>绑定的 Plugin</summary>
    public List<string> Plugins { get; set; } = new();

    /// <summary>版本</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>作者</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>是否有指令内容</summary>
    public bool HasInstruction { get; set; }

    /// <summary>最后修改时间</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>当前用户是否是此 Skill 的所有者</summary>
    public bool IsOwner { get; set; }

    /// <summary>当前用户是否有权限管理此 Skill (删除等)</summary>
    public bool CanManage { get; set; }

    /// <summary>关联的脚本</summary>
    public List<SkillScriptDto> Scripts { get; set; } = new();

    /// <summary>参考链接</summary>
    public List<string> References { get; set; } = new();

    /// <summary>元数据</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
