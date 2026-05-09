// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Core.Models;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace DevNexus.Core.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserIdentityService _userIdentityService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IAuthTokenService _authTokenService;
    private readonly ILogger<AuthService> _logger;
    private readonly IUserStoragePathService _userStoragePathService;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    public AuthService(
        IUserIdentityService userIdentityService,
        IRefreshTokenStore refreshTokenStore,
        IAuthTokenService authTokenService,
        ILogger<AuthService> logger,
        IUserStoragePathService userStoragePathService)
    {
        _userIdentityService = userIdentityService;
        _refreshTokenStore = refreshTokenStore;
        _authTokenService = authTokenService;
        _logger = logger;
        _userStoragePathService = userStoragePathService;
    }
    
    /// <inheritdoc />
    public async Task<TokenResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Auth.Login] Login attempt | UsernameOrEmail={UsernameOrEmail} DeviceId={DeviceId}",
            request.UsernameOrEmail,
            request.DeviceId);
        
        // 查找用户
        var user = await _userIdentityService.FindByUsernameAsync(request.UsernameOrEmail)
            ?? await _userIdentityService.FindByEmailAsync(request.UsernameOrEmail);
        
        if (user == null)
        {
            _logger.LogWarning("[Auth.Login] User not found | UsernameOrEmail={UsernameOrEmail}", request.UsernameOrEmail);
            return null;
        }
        
        // 检查用户是否启用
        if (!user.IsEnabled)
        {
            _logger.LogWarning("[Auth.Login] User is disabled | UserId={UserId}", user.Id);
            return null;
        }
        
        // 验证密码
        var passwordValid = await _userIdentityService.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("[Auth.Login] Invalid password | UserId={UserId}", user.Id);
            return null;
        }
        
        // 更新最后登录信息
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginDeviceId = request.DeviceId;
        user.UpdatedAt = DateTime.UtcNow;
        await _userIdentityService.UpdateAsync(user);
        
        // 生成令牌
        var roles = await _userIdentityService.GetRolesAsync(user);
        var accessToken = _authTokenService.GenerateAccessToken(user, roles);
        var refreshTokenResult = await GenerateRefreshTokenAsync(
            user.Id,
            request.DeviceId,
            request.DeviceName,
            request.DeviceType,
            ipAddress,
            userAgent,
            request.RememberMe,
            cancellationToken);
        
        // 初始化用户存储目录（创建 tmp/project 目录，清空 tmp）
        try
        {
            _userStoragePathService.InitializeUserStorage(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Auth.Login] 用户存储目录初始化失败，不影响登录 | UserId={UserId}", user.Id);
        }

        _logger.LogInformation("[Auth.Login] Login successful | UserId={UserId} Username={Username}", user.Id, user.Username);
        
        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenResult.TokenValue,
            TokenType = "Bearer",
            ExpiresIn = _authTokenService.AccessTokenExpiresInSeconds,
            RefreshTokenExpiresIn = (int)Math.Max(0, (refreshTokenResult.Token.ExpiresAt - DateTime.UtcNow).TotalSeconds),
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToList()
            }
        };
    }
    
    /// <inheritdoc />
    public async Task<TokenResponse?> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _refreshTokenStore.FindByTokenHashAsync(tokenHash, cancellationToken);
        
        if (storedToken == null)
        {
            _logger.LogWarning("[Auth.RefreshToken] Invalid or expired refresh token");
            return null;
        }

        // Rotation + reuse detection: a revoked token being presented again is suspicious.
        if (storedToken.IsRevoked)
        {
            _logger.LogWarning(
                "[Auth.RefreshToken] Refresh token reuse detected | UserId={UserId} TokenId={TokenId} ReplacedBy={ReplacedBy}",
                storedToken.UserId,
                storedToken.Id,
                storedToken.ReplacedByTokenId);
            return null;
        }

        if (storedToken.IsExpired)
        {
            _logger.LogWarning("[Auth.RefreshToken] Invalid or expired refresh token");
            return null;
        }
        
        var user = await _userIdentityService.FindByIdAsync(storedToken.UserId);
        if (user == null)
        {
            _logger.LogWarning("[Auth.RefreshToken] User not found | UserId={UserId}", storedToken.UserId);
            return null;
        }

        if (!user.IsEnabled)
        {
            _logger.LogWarning("[Auth.RefreshToken] User is disabled | UserId={UserId}", user.Id);
            return null;
        }

        // Rotate refresh token on each use. We keep the "short session" behavior (1 day)
        // by inferring initial lifetime from the stored token record.
        var initialLifetime = storedToken.ExpiresAt - storedToken.CreatedAt;
        var expirationDays = initialLifetime.TotalDays <= 1.1 ? 1 : _authTokenService.RefreshTokenExpiryDays;

        var newRefreshTokenValue = GenerateSecureToken();
        var newToken = new RefreshToken
        {
            UserId = storedToken.UserId,
            TokenHash = HashToken(newRefreshTokenValue),
            DeviceId = storedToken.DeviceId,
            DeviceName = storedToken.DeviceName,
            DeviceType = storedToken.DeviceType,
            IpAddress = ipAddress ?? storedToken.IpAddress,
            UserAgent = storedToken.UserAgent,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        };

        storedToken.LastUsedAt = DateTime.UtcNow;
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedReason = "Token rotated";
        storedToken.ReplacedByTokenId = newToken.Id;

        await _refreshTokenStore.AddAsync(newToken, cancellationToken);
        await _refreshTokenStore.SaveChangesAsync(cancellationToken);

        // 生成新的 Access Token
        var roles = await _userIdentityService.GetRolesAsync(user);
        var accessToken = _authTokenService.GenerateAccessToken(user, roles);
        
        _logger.LogInformation("[Auth.RefreshToken] Token refreshed | UserId={UserId}", user.Id);
        
        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshTokenValue,
            TokenType = "Bearer",
            ExpiresIn = _authTokenService.AccessTokenExpiresInSeconds,
            RefreshTokenExpiresIn = (int)Math.Max(0, (newToken.ExpiresAt - DateTime.UtcNow).TotalSeconds),
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToList()
            }
        };
    }
    
    /// <inheritdoc />
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _refreshTokenStore.FindByTokenHashAsync(tokenHash, cancellationToken);
        
        if (storedToken != null)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedReason = "User logout";
            
            await _refreshTokenStore.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("[Auth.Logout] Token revoked | UserId={UserId}", storedToken.UserId);
        }
    }
    
    /// <inheritdoc />
    public async Task LogoutAllDevicesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _refreshTokenStore.GetActiveTokensByUserIdAsync(userId, cancellationToken);
        
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "Logout from all devices";
        }
        
        await _refreshTokenStore.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("[Auth.LogoutAll] All tokens revoked | UserId={UserId} Count={Count}", userId, tokens.Count);
    }
    
    /// <inheritdoc />
    public async Task<AuthResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userIdentityService.FindByIdAsync(userId);
        if (user == null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户不存在" }
            };
        }
        
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "新密码与确认密码不匹配" }
            };
        }
        
        var result = await _userIdentityService.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        
        if (result.Succeeded)
        {
            _logger.LogInformation("[Auth.ChangePassword] Password changed | UserId={UserId}", userId);
            
            // 撤销所有刷新令牌，强制重新登录
            await LogoutAllDevicesAsync(userId, cancellationToken);
            
            return new AuthResult
            {
                Succeeded = true,
                Message = "密码修改成功，请重新登录"
            };
        }
        
        return new AuthResult
        {
            Succeeded = false,
            Errors = result.Errors.ToList()
        };
    }
    
    /// <inheritdoc />
    public async Task<UserInfo?> GetUserInfoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userIdentityService.FindByIdAsync(userId);
        if (user == null) return null;
        
        var roles = await _userIdentityService.GetRolesAsync(user);
        
        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            PhoneNumber = user.PhoneNumber,
            Roles = roles.ToList()
        };
    }
    
    /// <summary>
    /// 生成 Refresh Token
    /// </summary>
    private async Task<GeneratedRefreshToken> GenerateRefreshTokenAsync(
        Guid userId,
        string? deviceId,
        string? deviceName,
        string? deviceType,
        string? ipAddress,
        string? userAgent,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        var tokenValue = GenerateSecureToken();
        var expirationDays = rememberMe ? _authTokenService.RefreshTokenExpiryDays : 1;
        
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(tokenValue),
            DeviceId = deviceId,
            DeviceName = deviceName,
            DeviceType = deviceType,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        };
        
        await _refreshTokenStore.AddAsync(refreshToken, cancellationToken);
        await _refreshTokenStore.SaveChangesAsync(cancellationToken);
        
        return new GeneratedRefreshToken(refreshToken, tokenValue);
    }
    
    /// <summary>
    /// 生成安全随机令牌
    /// </summary>
    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
    
    /// <summary>
    /// 计算令牌哈希
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private sealed record GeneratedRefreshToken(RefreshToken Token, string TokenValue);
}
