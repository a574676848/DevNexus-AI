using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Notes;

/// <summary>
/// Memos 笔记服务实现 (支持 v0.24.0)
/// </summary>
public class MemosNoteService : INoteService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MemosNoteService> _logger;
    private readonly string _baseUrl;
    private readonly string _accessToken;
    private string? _userId;

    public MemosNoteService(
        string endpoint,
        string accessToken,
        IHttpClientFactory httpClientFactory,
        ILogger<MemosNoteService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _baseUrl = endpoint.TrimEnd('/');
        _accessToken = accessToken;

        // 设置默认请求头
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        
        // 从 JWT token 中提取 userId
        _userId = ExtractUserIdFromToken(accessToken);
    }

    /// <summary>
    /// 从 JWT token 中提取用户 ID
    /// </summary>
    private string? ExtractUserIdFromToken(string token)
    {
        try
        {
            // JWT 格式: header.payload.signature
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("Invalid JWT token format");
                return null;
            }

            // 解码 payload (Base64Url)
            var payload = parts[1];
            // 调整 Base64Url 到标准 Base64
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(jsonBytes);
            
            var jsonDoc = JsonDocument.Parse(json);
            if (jsonDoc.RootElement.TryGetProperty("sub", out var subElement))
            {
                var userId = subElement.GetString();
                _logger.LogInformation("Extracted user ID from token: {UserId}", userId);
                return userId;
            }

            _logger.LogWarning("'sub' claim not found in JWT token");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract user ID from JWT token");
            return null;
        }
    }

    public async Task<SearchNotesResponse> SearchNotesAsync(
        SearchNotesRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_userId))
            {
                throw new InvalidOperationException("无法获取用户 ID，JWT 令牌无效");
            }

            var queryParams = new List<string>
            {
                $"pageSize={request.PageSize}"
            };

            if (!string.IsNullOrEmpty(request.PageToken))
            {
                queryParams.Add($"pageToken={Uri.EscapeDataString(request.PageToken)}");
            }

            if (!string.IsNullOrEmpty(request.Query))
            {
                // Memos v0.24.0 使用 filter 参数进行搜索
                var filter = $"content.contains(\"{request.Query}\")";
                queryParams.Add($"filter={Uri.EscapeDataString(filter)}");
            }

            // Memos v0.24.0 要求使用 /users/{userId}/memos 端点
            var url = $"{_baseUrl}/api/v1/users/{_userId}/memos?{string.Join("&", queryParams)}";
            
            // 生成并打印 curl 命令
            var curlCommand = GenerateCurlCommand(HttpMethod.Get, url);
            _logger.LogInformation("CURL: {CurlCommand}", curlCommand);
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var memosResponse = JsonSerializer.Deserialize<MemosListResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new SearchNotesResponse
            {
                Notes = memosResponse?.Memos?.Select(MapToNoteDto).ToList() ?? new(),
                NextPageToken = memosResponse?.NextPageToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search notes in Memos");
            throw;
        }
    }

    public async Task<NoteDto> CreateNoteAsync(
        CreateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_userId))
            {
                throw new InvalidOperationException("无法获取用户 ID，JWT 令牌无效");
            }

            var payload = new
            {
                content = request.Content,
                visibility = request.Visibility ?? "PRIVATE"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Memos v0.24.0: 创建 memo 使用 /api/v1/memos 端点（列表查询才用 /users/{userId}/memos）
            var url = $"{_baseUrl}/api/v1/memos";
            
            // 生成并打印 curl 命令
            var curlCommand = GenerateCurlCommand(HttpMethod.Post, url, json);
            _logger.LogInformation("CURL: {CurlCommand}", curlCommand);

            var response = await _httpClient.PostAsync(
                url,
                content,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var memo = JsonSerializer.Deserialize<MemosMemo>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return MapToNoteDto(memo!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create note in Memos");
            throw;
        }
    }

    public async Task<NoteDto> UpdateNoteAsync(
        string noteId,
        UpdateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                content = request.Content,
                visibility = request.Visibility ?? "PRIVATE"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Memos v0.24.0: noteId 格式为 "memos/xxx"，直接使用
            var url = $"{_baseUrl}/api/v1/{noteId}?updateMask=content,visibility";
            
            // 生成并打印 curl 命令
            var curlCommand = GenerateCurlCommand(HttpMethod.Patch, url, json);
            _logger.LogInformation("CURL: {CurlCommand}", curlCommand);

            var response = await _httpClient.PatchAsync(
                url,
                content,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var memo = JsonSerializer.Deserialize<MemosMemo>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return MapToNoteDto(memo!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update note in Memos: {NoteId}", noteId);
            throw;
        }
    }

    public async Task<bool> DeleteNoteAsync(
        string noteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Memos v0.24.0: noteId 格式为 "memos/xxx"，直接使用
            var url = $"{_baseUrl}/api/v1/{noteId}";
            
            // 生成并打印 curl 命令
            var curlCommand = GenerateCurlCommand(HttpMethod.Delete, url);
            _logger.LogInformation("CURL: {CurlCommand}", curlCommand);
            
            var response = await _httpClient.DeleteAsync(
                url,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete note in Memos: {NoteId}", noteId);
            return false;
        }
    }

    public async Task<NoteDto?> GetNoteAsync(
        string noteId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Memos v0.24.0: noteId 格式为 "memos/xxx"，直接使用
            var url = $"{_baseUrl}/api/v1/{noteId}";
            
            // 生成并打印 curl 命令
            var curlCommand = GenerateCurlCommand(HttpMethod.Get, url);
            _logger.LogInformation("CURL: {CurlCommand}", curlCommand);
            
            var response = await _httpClient.GetAsync(
                url,
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var memo = JsonSerializer.Deserialize<MemosMemo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return memo != null ? MapToNoteDto(memo) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get note from Memos: {NoteId}", noteId);
            return null;
        }
    }

    public async Task<bool> ValidateConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_userId))
            {
                _logger.LogWarning("Cannot validate connection: User ID not available");
                return false;
            }

            // Memos v0.24.0 要求使用 /users/{userId}/memos 端点
            var url = $"{_baseUrl}/api/v1/users/{_userId}/memos?pageSize=1";
            
            // 生成并打印 curl 命令
            var curlCommand = GenerateCurlCommand(HttpMethod.Get, url);
            _logger.LogInformation("CURL: {CurlCommand}", curlCommand);
            
            var response = await _httpClient.GetAsync(
                url,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Memos connection");
            return false;
        }
    }

    private static NoteDto MapToNoteDto(MemosMemo memo)
    {
        return new NoteDto
        {
            Id = memo.Name ?? string.Empty,
            Content = memo.Content ?? string.Empty,
            Visibility = memo.Visibility,
            Tags = memo.Tags?.ToList(),
            CreatedAt = DateTime.TryParse(memo.CreateTime, out var createTime) 
                ? createTime : DateTime.UtcNow,
            UpdatedAt = DateTime.TryParse(memo.UpdateTime, out var updateTime) 
                ? updateTime : DateTime.UtcNow
        };
    }

    private string GenerateCurlCommand(HttpMethod method, string url, string? body = null)
    {
        var curl = new StringBuilder();
        curl.Append($"curl -X {method.Method.ToUpper()} '{url}'");
        
        // 添加认证头
        curl.Append($" -H 'Authorization: Bearer {_accessToken}'");
        
        // 添加 Accept 头
        curl.Append(" -H 'Accept: application/json'");
        
        // 如果有请求体，添加 Content-Type 和数据
        if (!string.IsNullOrEmpty(body))
        {
            curl.Append(" -H 'Content-Type: application/json'");
            // 转义单引号
            var escapedBody = body.Replace("'", "'\\''");
            curl.Append($" -d '{escapedBody}'");
        }
        
        return curl.ToString();
    }

    #region Memos API Models

    private class MemosListResponse
    {
        public List<MemosMemo>? Memos { get; set; }
        public string? NextPageToken { get; set; }
    }

    private class MemosMemo
    {
        public string? Name { get; set; }
        public string? Content { get; set; }
        public string? Visibility { get; set; }
        public string[]? Tags { get; set; }
        public string? CreateTime { get; set; }
        public string? UpdateTime { get; set; }
    }

    #endregion
}
