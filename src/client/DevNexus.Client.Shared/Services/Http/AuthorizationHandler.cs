using System.Net;
using System.Net.Http.Headers;
using DevNexus.Client.Shared.Abstractions;
namespace DevNexus.Client.Shared.Services.Http;

/// <summary>
/// Authorization HTTP Handler - 自动添加 Bearer Token 并处理 401 刷新
/// </summary>
public class AuthorizationHandler : DelegatingHandler
{
    private readonly IAuthService _authService;
    private readonly IRemoteLogService _remoteLogService;
    private readonly SemaphoreSlim _refreshTaskLock = new(1, 1);
    private Task<bool>? _refreshTask;

    public AuthorizationHandler(IAuthService authService, IRemoteLogService remoteLogService)
    {
        _authService = authService;
        _remoteLogService = remoteLogService;
    }

    /// <summary>
    /// 处理请求，添加 Authorization Header
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 添加 Authorization Header
        await AddAuthorizationHeaderAsync(request);

        // 发送请求
        var response = await base.SendAsync(request, cancellationToken);

        // 如果是 401，尝试刷新 Token 并重试
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response = await HandleUnauthorizedAsync(request, response, cancellationToken);
        }

        return response;
    }

    /// <summary>
    /// 添加 Authorization Header
    /// </summary>
    private async Task AddAuthorizationHeaderAsync(HttpRequestMessage request)
    {
        // 跳过不需要认证的请求 (login, refresh 等)
        if (IsAnonymousEndpoint(request.RequestUri))
        {
            return;
        }

        try
        {
            var token = await _authService.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                Console.WriteLine(
                    $"[AuthorizationHandler] 已添加 Bearer Token | {request.Method} {request.RequestUri}");
            }
            else
            {
                Console.WriteLine(
                    $"[AuthorizationHandler] 未获取到 AccessToken | {request.Method} {request.RequestUri}");
                await _remoteLogService.LogWarningAsync(
                    "请求发送前未获取到访问令牌",
                    "AuthorizationHandler.AddAuthorizationHeaderAsync",
                    new Dictionary<string, object?>
                    {
                        ["Uri"] = request.RequestUri?.ToString(),
                        ["Method"] = request.Method.Method
                    });
            }
        }
        catch (Exception ex)
        {
            // 获取 token 失败时，继续发送请求（可能会返回 401）
            Console.WriteLine(
                $"[AuthorizationHandler] 获取 AccessToken 异常 | {request.Method} {request.RequestUri} | {ex.Message}");
            await _remoteLogService.LogErrorAsync(ex, "AuthorizationHandler.AddAuthorizationHeaderAsync", new Dictionary<string, object?>
            {
                ["Uri"] = request.RequestUri?.ToString(),
                ["Method"] = request.Method.Method
            });
        }
    }

    /// <summary>
    /// 处理 401 未授权响应
    /// </summary>
    private async Task<HttpResponseMessage> HandleUnauthorizedAsync(
        HttpRequestMessage originalRequest,
        HttpResponseMessage originalResponse,
        CancellationToken cancellationToken)
    {
        if (IsAnonymousEndpoint(originalRequest.RequestUri))
        {
            return originalResponse;
        }

        var requestUri = originalRequest.RequestUri?.ToString();

        var refreshed = await GetOrStartRefreshTaskAsync(cancellationToken);
        try
        {
            if (refreshed)
            {
                originalResponse.Dispose();
                return await RetryRequestAsync(originalRequest, cancellationToken);
            }

            await _remoteLogService.LogWarningAsync(
                "收到 401 后刷新访问令牌失败，返回原始响应",
                "AuthorizationHandler.HandleUnauthorizedAsync",
                new Dictionary<string, object?>
                {
                    ["Uri"] = requestUri,
                    ["Method"] = originalRequest.Method.Method
                });

            return originalResponse;
        }
        catch (Exception ex)
        {
            await _remoteLogService.LogErrorAsync(ex, "AuthorizationHandler.HandleUnauthorizedAsync", new Dictionary<string, object?>
            {
                ["Uri"] = requestUri,
                ["Method"] = originalRequest.Method.Method
            });

            return originalResponse;
        }
    }

    /// <summary>
    /// 重试请求
    /// </summary>
    private async Task<HttpResponseMessage> RetryRequestAsync(
        HttpRequestMessage originalRequest,
        CancellationToken cancellationToken)
    {
        // 创建新请求 (原请求已被发送，不能重用)
        var retryRequest = await CloneRequestAsync(originalRequest);

        // 添加新的 Token
        await AddAuthorizationHeaderAsync(retryRequest);

        var retryResponse = await base.SendAsync(retryRequest, cancellationToken);

        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _remoteLogService.LogWarningAsync(
                "刷新访问令牌后重试请求仍返回 401",
                "AuthorizationHandler.RetryRequestAsync",
                new Dictionary<string, object?>
                {
                    ["Uri"] = retryRequest.RequestUri?.ToString(),
                    ["Method"] = retryRequest.Method.Method
                });
        }

        return retryResponse;
    }

    /// <summary>
    /// 克隆 HTTP 请求
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        // 复制 Headers
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // 复制 Content
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            // 复制 Content Headers
            if (request.Content.Headers.ContentType != null)
            {
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
            }
        }

        return clone;
    }

    /// <summary>
    /// 检查是否为匿名端点 (不需要 Token)
    /// </summary>
    private static bool IsAnonymousEndpoint(Uri? requestUri)
    {
        if (requestUri == null) return true;

        var path = requestUri.AbsolutePath.ToLowerInvariant();
        return path.Contains("/auth/login") ||
               path.Contains("/auth/refresh-token") ||
               path.Contains("/auth/register") ||
               path.Contains("/system/health") ||
               path.Contains("/system/client-version") ||
               path.Contains("/api/update/manifest");
    }

    private async Task<bool> GetOrStartRefreshTaskAsync(CancellationToken cancellationToken)
    {
        var cachedTask = Volatile.Read(ref _refreshTask);
        if (cachedTask != null)
        {
            return await cachedTask;
        }

        await _refreshTaskLock.WaitAsync(cancellationToken);
        Task<bool> refreshTask;
        try
        {
            cachedTask = _refreshTask;
            if (cachedTask != null)
            {
                refreshTask = cachedTask;
            }
            else
            {
                refreshTask = _authService.TryRefreshTokenAsync();
                _refreshTask = refreshTask;
            }
        }
        finally
        {
            _refreshTaskLock.Release();
        }

        try
        {
            return await refreshTask;
        }
        finally
        {
            await _refreshTaskLock.WaitAsync(cancellationToken);
            try
            {
                if (ReferenceEquals(_refreshTask, refreshTask))
                {
                    _refreshTask = null;
                }
            }
            finally
            {
                _refreshTaskLock.Release();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTaskLock.Dispose();
        }
        base.Dispose(disposing);
    }
}

