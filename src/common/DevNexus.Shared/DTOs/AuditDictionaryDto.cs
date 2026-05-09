namespace DevNexus.Shared.DTOs;

/// <summary>
/// 审计字典 DTO。
/// </summary>
public class AuditDictionaryDto
{
    /// <summary>
    /// 场景字典。
    /// </summary>
    public List<AuditDictionaryItemDto> Scenes { get; set; } = new();

    /// <summary>
    /// 主体字典。
    /// </summary>
    public List<AuditDictionaryItemDto> Owners { get; set; } = new();

    /// <summary>
    /// 状态字典。
    /// </summary>
    public List<AuditDictionaryItemDto> Statuses { get; set; } = new();
}

/// <summary>
/// 审计字典项 DTO。
/// </summary>
public class AuditDictionaryItemDto
{
    /// <summary>
    /// 编码。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 中文显示名。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
