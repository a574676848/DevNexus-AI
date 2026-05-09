using System.Net.Http.Json;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.DTOs.Auth;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Services.Exceptions;
using System.Text.Json;
namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务实现
/// </summary>
public partial class ApiService : IApiService
{
    private static readonly TimeSpan SessionListTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SessionMessagesTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClientEnvironmentService _clientEnvironmentService;

    public ApiService(
        IHttpClientFactory httpClientFactory,
        IClientEnvironmentService clientEnvironmentService)
    {
        _httpClientFactory = httpClientFactory;
        _httpClient = httpClientFactory.CreateClient("DevNexusApi");
        _clientEnvironmentService = clientEnvironmentService;
    }

    #region 会话管理

    /// <inheritdoc />
    public async Task<List<ChatSessionDto>> GetSessionsAsync()
    {
        using var cts = new CancellationTokenSource(SessionListTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chat/sessions");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessAsync(response);

        var payload = await response.Content.ReadAsStringAsync(cts.Token);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new();
        }

        return JsonSerializer.Deserialize<List<ChatSessionDto>>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
    }

    /// <inheritdoc />
    public async Task<ChatSessionDto> CreateSessionAsync(string? title = null)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/chat/sessions", new { Title = title });
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ChatSessionDto>()
            ?? throw new ApiException("创建会话失败");
    }

    /// <inheritdoc />
    public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid sessionId)
    {
        using var cts = new CancellationTokenSource(SessionMessagesTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chat/sessions/{sessionId}/messages");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessAsync(response);

        var payload = await response.Content.ReadAsStringAsync(cts.Token);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new();
        }

        return JsonSerializer.Deserialize<List<ChatMessageDto>>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
    }

    /// <inheritdoc />
    public async Task<List<TerminalRecordDto>> GetActiveTerminalRecordsAsync(Guid sessionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chat/sessions/{sessionId}/active-terminals");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await EnsureSuccessAsync(response);

        var payload = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new();
        }

        return JsonSerializer.Deserialize<List<TerminalRecordDto>>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
    }

    /// <inheritdoc />
    public async Task<TerminalOutputContentDto> GetTerminalOutputAsync(Guid sessionId, Guid recordId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/chat/sessions/{sessionId}/terminals/{recordId}/output");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<TerminalOutputContentDto>()
            ?? throw new ApiException("获取终端完整输出失败");
    }

    /// <inheritdoc />
    public async Task<List<PendingInteractionDto>> GetPendingInteractionsAsync(Guid sessionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chat/sessions/{sessionId}/pending-interactions");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await EnsureSuccessAsync(response);

        var payload = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new();
        }

        return JsonSerializer.Deserialize<List<PendingInteractionDto>>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
    }

    /// <inheritdoc />
    public async Task<ChatSessionRuntimeDto> GetSessionRuntimeAsync(Guid sessionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chat/sessions/{sessionId}/runtime");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<ChatSessionRuntimeDto>()
            ?? throw new ApiException("获取会话运行态失败");
    }

    /// <inheritdoc />
    public async Task<PendingInteractionResolutionResponse> ResolvePendingInteractionAsync(
        Guid sessionId,
        Guid interactionId,
        PendingInteractionResolutionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/chat/sessions/{sessionId}/pending-interactions/{interactionId}/resolve",
            request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PendingInteractionResolutionResponse>()
            ?? throw new ApiException("解决挂起交互失败");
    }

    /// <inheritdoc />
    public async Task DeleteMessageAsync(Guid sessionId, Guid messageId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/chat/sessions/{sessionId}/messages/{messageId}");
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task<int> DeleteMessagesAsync(Guid sessionId, List<Guid> messageIds)
    {
        if (messageIds == null || messageIds.Count == 0)
        {
            return 0;
        }

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/chat/sessions/{sessionId}/messages/batch-delete",
            new { MessageIds = messageIds });
        await EnsureSuccessAsync(response);
        
        var result = await response.Content.ReadFromJsonAsync<BatchDeleteResponse>();
        return result?.DeletedCount ?? 0;
    }

    private record BatchDeleteResponse(int DeletedCount, string Message);

    /// <inheritdoc />

    /// <inheritdoc />
    public async Task DeleteSessionAsync(Guid sessionId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/chat/sessions/{sessionId}");
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task<ChatSessionDto> UpdateSessionAsync(Guid sessionId, ChatSessionUpdateRequest request)
    {
        var response = await _httpClient.PatchAsJsonAsync($"/api/v1/chat/sessions/{sessionId}", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ChatSessionDto>()
            ?? throw new ApiException("更新会话失败");
    }

    /// <inheritdoc />
    public async Task UpdateSessionTitleAsync(Guid sessionId, string title)
    {
        await UpdateSessionAsync(sessionId, new ChatSessionUpdateRequest { Title = title });
    }

    /// <inheritdoc />
    public async Task<string?> GenerateSmartTitleAsync(Guid sessionId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/chat/sessions/{sessionId}/generate-title", null);
            await EnsureSuccessAsync(response);
            var result = await response.Content.ReadFromJsonAsync<GenerateSmartTitleResponse>();
            return result?.Title;
        }
        catch
        {
            return null; // 生成失败时返回 null，且不再在底层写本地日志（网络失败已由 LoggingHandler 上报）
        }
    }

    private record GenerateSmartTitleResponse(string? Title);
 
    /// <inheritdoc />
    public async Task AbortSwarmSessionAsync(Guid sessionId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/swarm/sessions/{sessionId}/abort", null);
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task RetrySwarmPackageAsync(Guid sessionId, string packageId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/swarm/sessions/{sessionId}/packages/{packageId}/retry", null);
        await EnsureSuccessAsync(response);
    }
 
    #endregion

    #region 辅助方法

    /// <summary>
    /// 确保响应成功
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var errorMessage = BuildApiErrorMessage(response.StatusCode, content);
            throw new ApiException(errorMessage, (int)response.StatusCode, content);
        }
    }

    private static string BuildApiErrorMessage(global::System.Net.HttpStatusCode statusCode, string? responseContent)
    {
        var extracted = TryExtractErrorMessage(responseContent);
        if (!string.IsNullOrWhiteSpace(extracted))
        {
            return extracted;
        }

        return $"API 请求失败: {statusCode}";
    }

    private static string? TryExtractErrorMessage(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            // 统一 ApiResponse<T> / ProblemDetails / 匿名对象 error/message 兼容提取
            var candidates = new[] { "error", "message", "title", "detail" };
            foreach (var propertyName in candidates)
            {
                if (TryGetStringProperty(root, propertyName, out var value))
                {
                    return value;
                }
            }

            // ApiResponse.Errors[0].Message 兼容
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (TryGetStringProperty(error, "message", out var errorMessage))
                    {
                        return errorMessage;
                    }
                }
            }

            // ASP.NET Core ModelState: errors: { field: [msg1,msg2] }
            if (root.TryGetProperty("errors", out var modelErrors) && modelErrors.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in modelErrors.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var itemText = item.GetString();
                            if (!string.IsNullOrWhiteSpace(itemText))
                            {
                                return itemText;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // 非 JSON 响应，回落使用原文本。
        }

        return string.IsNullOrWhiteSpace(responseContent) ? null : responseContent.Trim();
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    /// <summary>
    /// 读取统一响应包装中的数据。
    /// </summary>
    private async Task<T?> ReadApiResponseAsync<T>(HttpResponseMessage response)
    {
        var wrapped = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
        if (wrapped != null)
        {
            return wrapped.Data;
        }

        return await response.Content.ReadFromJsonAsync<T>();
    }

    /// <summary>
    /// 构建日期查询参数
    /// </summary>
    private static string BuildDateQuery(DateTime? startDate, DateTime? endDate)
    {
        var parts = new List<string>();
        if (startDate.HasValue)
            parts.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            parts.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        return parts.Count > 0 ? "?" + string.Join("&", parts) : "";
    }

    #endregion
}
