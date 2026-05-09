using DevNexus.Core.Models;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 认证令牌生成抽象，隔离 JWT 配置和实现细节。
/// </summary>
public interface IAuthTokenService
{
    int AccessTokenExpiresInSeconds { get; }
    int RefreshTokenExpiryDays { get; }

    string GenerateAccessToken(UserIdentityModel user, IEnumerable<string> roles);
}
