namespace DevNexus.Domain.Configuration;

/// <summary>
/// JWT 认证配置选项
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// JWT 密钥（至少32字符）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 发行者
    /// </summary>
    public string Issuer { get; set; } = "DevNexus";

    /// <summary>
    /// 受众
    /// </summary>
    public string Audience { get; set; } = "DevNexus";

    /// <summary>
    /// 访问令牌过期时间（分钟）
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// 刷新令牌过期时间（天）
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 30;

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException(
                "JWT Key is required. Set it via appsettings.json (Jwt:Key) or environment variable (Jwt__Key). " +
                "For production, use a secure random key of at least 32 characters.");
        }

        if (Key.Length < 32)
        {
            throw new InvalidOperationException(
                $"JWT Key must be at least 32 characters long. Current length: {Key.Length}. " +
                "Generate a secure key using: openssl rand -base64 32");
        }
    }
}
