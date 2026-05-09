using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs.Auth;

/// <summary>
/// 登录请求 DTO
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// 用户名或邮箱
    /// </summary>
    [JsonPropertyName("usernameOrEmail")]
    public string UsernameOrEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// 密码
    /// </summary>
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
    
    /// <summary>
    /// 设备ID（用于设备指纹）
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// 设备名称
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }
    
    /// <summary>
    /// 设备类型
    /// </summary>
    [JsonPropertyName("deviceType")]
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// 是否记住登录状态
    /// </summary>
    [JsonPropertyName("rememberMe")]
    public bool RememberMe { get; set; } = true;
}

/// <summary>
/// Token 响应 DTO
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>
    /// 刷新令牌
    /// </summary>
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
    
    /// <summary>
    /// 令牌类型
    /// </summary>
    [JsonPropertyName("tokenType")]
    public string TokenType { get; set; } = "Bearer";
    
    /// <summary>
    /// 访问令牌过期时间（秒）
    /// </summary>
    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }
    
    /// <summary>
    /// 刷新令牌过期时间（秒）
    /// </summary>
    [JsonPropertyName("refreshTokenExpiresIn")]
    public int RefreshTokenExpiresIn { get; set; }
    
    /// <summary>
    /// 用户信息
    /// </summary>
    [JsonPropertyName("user")]
    public UserInfo? User { get; set; }
}

/// <summary>
/// 用户信息 DTO
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    /// <summary>
    /// 用户名
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// 邮箱
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 手机号
    /// </summary>
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    
    /// <summary>
    /// 显示名称
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 头像URL
    /// </summary>
    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// 角色列表
    /// </summary>
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// 刷新令牌请求 DTO
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// 刷新令牌
    /// </summary>
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// 修改密码请求 DTO
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// 当前密码
    /// </summary>
    [JsonPropertyName("currentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;
    
    /// <summary>
    /// 新密码
    /// </summary>
    [JsonPropertyName("newPassword")]
    public string NewPassword { get; set; } = string.Empty;
    
    /// <summary>
    /// 确认新密码
    /// </summary>
    [JsonPropertyName("confirmNewPassword")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

/// <summary>
/// 认证结果 DTO
/// </summary>
public class AuthResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// 成功时的消息
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
