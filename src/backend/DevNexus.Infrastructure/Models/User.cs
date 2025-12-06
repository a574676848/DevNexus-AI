using DevNexus.Infrastructure.Models.Base;
using Microsoft.AspNetCore.Identity;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// 用户实体 - 继承自 ASP.NET Identity
/// </summary>
public class User : IdentityUser<Guid>, IAuditableEntity
{
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 头像URL
    /// </summary>
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; } = null;
    
    /// <summary>
    /// 最后登录设备ID
    /// </summary>
    public string? LastLoginDeviceId { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 关联的聊天会话
    /// </summary>
    public List<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    
    /// <summary>
    /// 关联的刷新令牌
    /// </summary>
    public List<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

/// <summary>
/// 可审计实体接口
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    DateTime UpdatedAt { get; set; }
}
