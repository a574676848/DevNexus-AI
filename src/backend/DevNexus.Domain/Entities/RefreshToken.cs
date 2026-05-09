namespace DevNexus.Domain.Entities;

/// <summary>
/// 刷新令牌实体
/// 用于实现长效登录和 Token 刷新机制
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// 令牌ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// 令牌哈希（用于快速查找）
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;
    
    /// <summary>
    /// 设备ID/指纹
    /// </summary>
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// 设备名称
    /// </summary>
    public string? DeviceName { get; set; }
    
    /// <summary>
    /// 设备类型 (Desktop, Mobile, Tablet, Web)
    /// </summary>
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// 客户端IP地址
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// 用户代理
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// 最后使用时间（滑动过期）
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>
    /// 是否已撤销
    /// </summary>
    public bool IsRevoked { get; set; } = false;
    
    /// <summary>
    /// 撤销时间
    /// </summary>
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>
    /// 撤销原因
    /// </summary>
    public string? RevokedReason { get; set; }
    
    /// <summary>
    /// 替换令牌ID（当此令牌被刷新时指向新令牌）
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }
    
    /// <summary>
    /// 检查令牌是否有效
    /// </summary>
    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiresAt;
    
    /// <summary>
    /// 检查令牌是否已过期
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
