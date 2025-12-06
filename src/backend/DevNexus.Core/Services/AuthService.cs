using DevNexus.Core.Abstractions;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DevNexus.Core.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    
    /// <summary>
    /// Access Token 过期时间（小时）
    /// </summary>
    private const int AccessTokenExpirationHours = 1;
    
    /// <summary>
    /// Refresh Token 过期时间（天）
    /// </summary>
    private const int RefreshTokenExpirationDays = 30;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    public AuthService(
        UserManager<User> userManager,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
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
        var user = await _userManager.FindByNameAsync(request.UsernameOrEmail)
            ?? await _userManager.FindByEmailAsync(request.UsernameOrEmail);
        
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
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("[Auth.Login] Invalid password | UserId={UserId}", user.Id);
            return null;
        }
        
        // 更新最后登录信息
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginDeviceId = request.DeviceId;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        
        // 生成令牌
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var refreshToken = await GenerateRefreshTokenAsync(
            user.Id,
            request.DeviceId,
            request.DeviceName,
            request.DeviceType,
            ipAddress,
            userAgent,
            request.RememberMe,
            cancellationToken);
        
        _logger.LogInformation("[Auth.Login] Login successful | UserId={UserId} Username={Username}", user.Id, user.UserName);
        
        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            TokenType = "Bearer",
            ExpiresIn = AccessTokenExpirationHours * 3600,
            RefreshTokenExpiresIn = RefreshTokenExpirationDays * 24 * 3600,
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
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
        var storedToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
        
        if (storedToken == null || !storedToken.IsActive)
        {
            _logger.LogWarning("[Auth.RefreshToken] Invalid or expired refresh token");
            return null;
        }
        
        var user = storedToken.User;
        if (!user.IsEnabled)
        {
            _logger.LogWarning("[Auth.RefreshToken] User is disabled | UserId={UserId}", user.Id);
            return null;
        }
        
        // 更新令牌使用时间（滑动过期）
        storedToken.LastUsedAt = DateTime.UtcNow;
        storedToken.ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // 生成新的 Access Token
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        
        _logger.LogInformation("[Auth.RefreshToken] Token refreshed | UserId={UserId}", user.Id);
        
        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken, // 返回相同的 refresh token
            TokenType = "Bearer",
            ExpiresIn = AccessTokenExpirationHours * 3600,
            RefreshTokenExpiresIn = (int)(storedToken.ExpiresAt - DateTime.UtcNow).TotalSeconds,
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Roles = roles.ToList()
            }
        };
    }
    
    /// <inheritdoc />
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
        
        if (storedToken != null)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedReason = "User logout";
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("[Auth.Logout] Token revoked | UserId={UserId}", storedToken.UserId);
        }
    }
    
    /// <inheritdoc />
    public async Task LogoutAllDevicesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);
        
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "Logout from all devices";
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("[Auth.LogoutAll] All tokens revoked | UserId={UserId} Count={Count}", userId, tokens.Count);
    }
    
    /// <inheritdoc />
    public async Task<AuthResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
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
        
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        
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
            Errors = result.Errors.Select(e => e.Description).ToList()
        };
    }
    
    /// <inheritdoc />
    public async Task<UserInfo?> GetUserInfoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;
        
        var roles = await _userManager.GetRolesAsync(user);
        
        return new UserInfo
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Roles = roles.ToList()
        };
    }
    
    /// <summary>
    /// 生成 Access Token
    /// </summary>
    private string GenerateAccessToken(User user, IList<string> roles)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "your-secret-key-here-1234567890123456";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("display_name", user.DisplayName)
        };
        
        // 添加角色声明
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "DevNexus",
            audience: _configuration["Jwt:Audience"] ?? "DevNexus",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(AccessTokenExpirationHours),
            signingCredentials: creds);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    /// <summary>
    /// 生成 Refresh Token
    /// </summary>
    private async Task<RefreshToken> GenerateRefreshTokenAsync(
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
        var expirationDays = rememberMe ? RefreshTokenExpirationDays : 1;
        
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = tokenValue,
            TokenHash = HashToken(tokenValue),
            DeviceId = deviceId,
            DeviceName = deviceName,
            DeviceType = deviceType,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        };
        
        await _dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return refreshToken;
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
}
