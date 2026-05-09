using DevNexus.Client.Shared.Abstractions;


namespace DevNexus.Client.Shared.Services.Http;

/// <summary>
/// Logging HTTP Handler - 记录请求和响应日志
/// </summary>
public class LoggingHandler : DelegatingHandler
{
    private readonly IRemoteLogService _remoteLog;

    public LoggingHandler(IRemoteLogService remoteLog)
    {
        _remoteLog = remoteLog;
    }

    /// <summary>
    /// 处理请求，记录日志
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTime.UtcNow;

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            // 上报远程日志
            await _remoteLog.LogErrorAsync(ex, $"HttpClient.{request.Method}", new Dictionary<string, object?>
            {
                ["RequestId"] = requestId,
                ["Uri"] = request.RequestUri?.ToString(),
                ["ElapsedMs"] = elapsedMs
            });
            throw;
        }

        if (response.StatusCode == global::System.Net.HttpStatusCode.Unauthorized)
        {
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            await _remoteLog.LogWarningAsync("HTTP 401 Unauthorized", $"HttpClient.{request.Method}", new Dictionary<string, object?>
            {
                ["RequestId"] = requestId,
                ["Uri"] = request.RequestUri?.ToString(),
                ["ElapsedMs"] = elapsedMs
            });
        }
        return response;
    }
}

