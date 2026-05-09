using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using DevNexus.Client.Shared.Abstractions;
namespace DevNexus.Client.Shared.Services.Auth;

/// <summary>
/// 认证服务实现 - 管理 Token 和用户认证状态
/// </summary>
public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IRemoteLogService _remoteLog;
    private readonly ISecureStorageService _secureStorage;
    private readonly AuthRuntimeState _runtimeState;

    private const string RefreshTokenKey = "refresh_token";
    private const string ApiBaseUrlKey = "api_base_url";

    /// <inheritdoc />
    public bool IsAuthenticated => !string.IsNullOrEmpty(_runtimeState.AccessToken) &&
                                   _runtimeState.TokenExpiry > DateTime.UtcNow;

    /// <inheritdoc />
    public Guid? CurrentUserId => _runtimeState.CurrentUserId;

    /// <inheritdoc />
    public IReadOnlyList<string> CurrentUserRoles => _runtimeState.CurrentUserRoles.AsReadOnly();

    /// <inheritdoc />
    public event Action<bool>? OnAuthStateChanged;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        IRemoteLogService remoteLog,
        ISecureStorageService secureStorage,
        AuthRuntimeState runtimeState)
    {
        _httpClient = httpClientFactory.CreateClient("AuthApi");
        _remoteLog = remoteLog;
        _secureStorage = secureStorage;
        _runtimeState = runtimeState;
    }

    /// <inheritdoc />
    public async Task<string> GetApiBaseUrlAsync()
    {
        try
        {
            var savedUrl = await _secureStorage.GetAsync(ApiBaseUrlKey);
            // 使用统一配置中的 ApiBaseUrl 作为默认值
            return string.IsNullOrEmpty(savedUrl) ? AppSettings.DefaultApiBaseUrl : savedUrl;
        }
        catch
        {
            return AppSettings.DefaultApiBaseUrl;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetAccessTokenAsync()
    {
        // Token 还有效
        if (!string.IsNullOrEmpty(_runtimeState.AccessToken) &&
            _runtimeState.TokenExpiry > DateTime.UtcNow.AddMinutes(2))
        {
            Console.WriteLine(
                $"[AuthService] 返回内存 AccessToken | ExpiresAt={_runtimeState.TokenExpiry:O} | UserId={_runtimeState.CurrentUserId}");
            return _runtimeState.AccessToken;
        }

        // Token 即将过期或已过期，尝试刷新
        if (!string.IsNullOrEmpty(_runtimeState.RefreshToken))
        {
            var refreshed = await TryRefreshTokenAsync();
            if (refreshed)
            {
                return _runtimeState.AccessToken;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> TryRefreshTokenAsync()
    {
        var wasAuthenticated = IsAuthenticated;
        if (string.IsNullOrEmpty(_runtimeState.RefreshToken))
        {
            await _remoteLog.LogWarningAsync(
                "刷新访问令牌时未找到 refresh token",
                "AuthService.TryRefreshTokenAsync",
                new Dictionary<string, object?>
                {
                    ["WasAuthenticated"] = wasAuthenticated
                });
            return false;
        }

        var cachedRefreshTask = _runtimeState.RefreshTokenTask;
        if (cachedRefreshTask != null)
        {
            return await cachedRefreshTask;
        }

        await _runtimeState.RefreshLock.WaitAsync();
        Task<bool> refreshTask;
        try
        {
            cachedRefreshTask = _runtimeState.RefreshTokenTask;
            if (cachedRefreshTask != null)
            {
                refreshTask = cachedRefreshTask;
            }
            else
            {
                refreshTask = RefreshTokenCoreAsync(wasAuthenticated);
                _runtimeState.RefreshTokenTask = refreshTask;
            }
        }
        finally
        {
            _runtimeState.RefreshLock.Release();
        }

        try
        {
            return await refreshTask;
        }
        finally
        {
            await _runtimeState.RefreshLock.WaitAsync();
            try
            {
                if (ReferenceEquals(_runtimeState.RefreshTokenTask, refreshTask))
                {
                    _runtimeState.RefreshTokenTask = null;
                }
            }
            finally
            {
                _runtimeState.RefreshLock.Release();
            }
        }
    }

    private async Task<bool> RefreshTokenCoreAsync(bool wasAuthenticated)
    {
        HttpResponseMessage? response = null;

        try
        {
            response = await _httpClient.PostAsJsonAsync("/api/v1/auth/refresh-token", new
            {
                RefreshToken = _runtimeState.RefreshToken
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (result == null)
                {
                    await _remoteLog.LogWarningAsync(
                        "刷新访问令牌成功但响应体为空",
                        "AuthService.TryRefreshTokenAsync");
                    return false;
                }

                var tokensApplied = await SetTokensAsync(result.AccessToken, result.RefreshToken, result.ExpiresIn);
                if (!tokensApplied)
                {
                    await _remoteLog.LogWarningAsync(
                        "刷新访问令牌成功，但客户端应用 token 失败",
                        "AuthService.TryRefreshTokenAsync");
                    return false;
                }

                if (!wasAuthenticated && IsAuthenticated)
                {
                    // 仅在从未认证切换为已认证时通知，避免频繁刷新导致重复初始化
                    OnAuthStateChanged?.Invoke(true);
                }

                return true;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            await _remoteLog.LogWarningAsync(
                "刷新访问令牌失败",
                "AuthService.TryRefreshTokenAsync",
                new Dictionary<string, object?>
                {
                    ["StatusCode"] = (int)response.StatusCode,
                    ["Response"] = responseContent
                });

            // 明确的认证失败说明 refresh token 已失效，此时再清理本地状态。
            if (response.StatusCode is global::System.Net.HttpStatusCode.Unauthorized
                or global::System.Net.HttpStatusCode.BadRequest)
            {
                await ClearTokensAsync();
                if (wasAuthenticated)
                {
                    OnAuthStateChanged?.Invoke(false);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "AuthService.TryRefreshTokenAsync", new Dictionary<string, object?>
            {
                ["HasRefreshToken"] = !string.IsNullOrEmpty(_runtimeState.RefreshToken)
            });

            // 瞬时异常不主动清空 refresh token，避免把网络/JS 异常误判成登录失效。
            return false;
        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new
            {
                UsernameOrEmail = username,
                Password = password
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (result != null)
                {
                    var tokensApplied = await SetTokensAsync(result.AccessToken, result.RefreshToken, result.ExpiresIn);
                    if (tokensApplied)
                    {
                        OnAuthStateChanged?.Invoke(true);
                        return true;
                    }

                    await _remoteLog.LogWarningAsync(
                        "登录成功，但客户端应用 token 失败",
                        "AuthService.LoginAsync",
                        new Dictionary<string, object?>
                        {
                            ["Username"] = username
                        });
                }
            }
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "AuthService.LoginAsync", new Dictionary<string, object?>
            {
                ["Username"] = username
            });
        }

        return false;
    }

    /// <inheritdoc />
    public async Task LogoutAsync()
    {
        // Best-effort server-side revoke. Even if this fails, we still clear local tokens.
        try
        {
            if (!string.IsNullOrEmpty(_runtimeState.RefreshToken))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
                {
                    Content = JsonContent.Create(new { RefreshToken = _runtimeState.RefreshToken })
                };

                if (!string.IsNullOrEmpty(_runtimeState.AccessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _runtimeState.AccessToken);
                }

                _ = await _httpClient.SendAsync(request);
            }
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "AuthService.LogoutAsync.RevokeServerToken");
        }

        await ClearTokensAsync();
        OnAuthStateChanged?.Invoke(false);
    }

    /// <inheritdoc />
    public async Task<bool> TryRestoreSessionAsync()
    {
        var cachedRestoreTask = _runtimeState.RestoreSessionTask;
        if (cachedRestoreTask != null)
        {
            return await cachedRestoreTask;
        }

        await _runtimeState.RestoreLock.WaitAsync();
        try
        {
            cachedRestoreTask = _runtimeState.RestoreSessionTask;
            if (cachedRestoreTask != null)
            {
                return await cachedRestoreTask;
            }

            _runtimeState.RestoreSessionTask = RestoreSessionCoreAsync();
        }
        finally
        {
            _runtimeState.RestoreLock.Release();
        }

        return await _runtimeState.RestoreSessionTask;
    }

    private async Task<bool> RestoreSessionCoreAsync()
    {
        try
        {
            var savedRefreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
            if (string.IsNullOrEmpty(savedRefreshToken))
            {
                await _remoteLog.LogWarningAsync(
                    "恢复会话时未找到 refresh token",
                    "AuthService.TryRestoreSessionAsync");
                return false;
            }

            _runtimeState.RefreshToken = savedRefreshToken;
            Console.WriteLine("[AuthService] 已从安全存储恢复 RefreshToken，开始刷新 AccessToken");
            return await TryRefreshTokenAsync();
        }
        catch (Exception ex)
        {
            // 安全存储访问异常通常比较重要，上报到远程
            await _remoteLog.LogErrorAsync(ex, "AuthService.TryRestoreSessionAsync");
            return false;
        }
    }

    /// <summary>
    /// 设置 Token
    /// </summary>
    private async Task<bool> SetTokensAsync(string accessToken, string refreshToken, int expiresIn)
    {
        _runtimeState.AccessToken = accessToken;
        _runtimeState.RefreshToken = refreshToken;
        _runtimeState.TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        _runtimeState.CurrentUserId = null;
        _runtimeState.CurrentUserRoles.Clear();

        // 解析用户ID和角色
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(accessToken);
            Console.WriteLine(
                $"[AuthService] 收到 AccessToken | Claims={jwt.Claims.Count()} | ExpiresIn={expiresIn}s");
            
            // 解析用户ID
            // Server uses ClaimTypes.NameIdentifier, which becomes a URI in JWT payload.
            var userIdClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == "sub" ||
                c.Type == "userId" ||
                c.Type == "nameid" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/nameidentifier");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _runtimeState.CurrentUserId = userId;
            }
            
            // 解析角色 (支持标准 ClaimTypes.Role)
            var roleClaims = jwt.Claims.Where(c => 
                c.Type == "role" || 
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
            _runtimeState.CurrentUserRoles = roleClaims.Select(c => c.Value).ToList();

            // Treat tokens without a resolvable user id as invalid for app auth state.
            if (_runtimeState.CurrentUserId == null)
            {
                Console.WriteLine("[AuthService] AccessToken 缺少可解析的用户ID声明");
                throw new InvalidOperationException("Access token does not contain a valid user id claim.");
            }

            Console.WriteLine(
                $"[AuthService] AccessToken 解析成功 | UserId={_runtimeState.CurrentUserId} | Roles={string.Join(",", _runtimeState.CurrentUserRoles)}");
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "AuthService.SetTokensAsync.ParseJwt");
            await ClearTokensAsync();
            return false;
        }

        // 安全存储 RefreshToken
        try
        {
            await _secureStorage.SetAsync(RefreshTokenKey, refreshToken);
            Console.WriteLine("[AuthService] RefreshToken 已写入安全存储");
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "AuthService.SetTokensAsync.SaveRefreshToken");
        }

        Console.WriteLine(
            $"[AuthService] Token 应用完成 | IsAuthenticated={IsAuthenticated} | UserId={_runtimeState.CurrentUserId}");
        return IsAuthenticated;
    }

    /// <summary>
    /// 清除 Token
    /// </summary>
    private async Task ClearTokensAsync()
    {
        _runtimeState.AccessToken = null;
        _runtimeState.RefreshToken = null;
        _runtimeState.TokenExpiry = DateTime.MinValue;
        _runtimeState.CurrentUserId = null;
        _runtimeState.CurrentUserRoles.Clear();

        try
        {
            await _secureStorage.RemoveAsync(RefreshTokenKey);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "AuthService.ClearTokensAsync.RemoveRefreshToken");
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// 认证响应
/// </summary>
public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn);
