using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// CLI 审批授权实体。
/// 用于持久化会话级命令放行记录，避免进程重启后授权丢失。
/// </summary>
public class CliApprovalGrant : AuditableEntity
{
    /// <summary>
    /// 用户标识。
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 聊天会话标识。
    /// </summary>
    public Guid? ChatSessionId { get; set; }

    /// <summary>
    /// 会话范围键。
    /// 一般为聊天会话 GUID 的 N 格式。
    /// </summary>
    public string SessionScopeKey { get; set; } = string.Empty;

    /// <summary>
    /// 授权范围。
    /// </summary>
    public CliApprovalGrantScope Scope { get; set; } = CliApprovalGrantScope.Once;

    /// <summary>
    /// 匹配值。
    /// 单次授权时为命令指纹，会话授权时为命令模式。
    /// </summary>
    public string MatchValue { get; set; } = string.Empty;

    /// <summary>
    /// 授权生效时间。
    /// </summary>
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 单次授权消耗时间。
    /// </summary>
    public DateTime? ConsumedAt { get; set; }
}
