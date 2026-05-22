using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.LLM;

public partial class KernelService
{
    private static readonly string[] InputTokenPropertyNames =
    [
        "InputTokens",
        "PromptTokens"
    ];

    private static readonly string[] OutputTokenPropertyNames =
    [
        "OutputTokens",
        "CompletionTokens"
    ];

    private static readonly string[] CachedTokenPropertyNames =
    [
        "CachedPromptTokens",
        "CachedInputTokens",
        "CacheReadInputTokens",
        "CacheReadTokens",
        "CachedTokens"
    ];

    private static readonly string[] TokenDetailsPropertyNames =
    [
        "InputTokenDetails",
        "PromptTokenDetails",
        "PromptTokensDetails"
    ];

    private sealed record TokenUsageMetadata(
        int? InputTokens,
        int? OutputTokens,
        int? CachedPromptTokens);

    /// <summary>
    /// 估算文本的 Token 数量
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var chineseCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherCount = text.Length - chineseCount;

        return (int)Math.Ceiling(chineseCount / 1.5) + (int)Math.Ceiling(otherCount / 4.0);
    }

    /// <summary>
    /// 估算聊天历史的 Token 数量
    /// </summary>
    private static int EstimateChatHistoryTokens(ChatHistory chatHistory)
    {
        return chatHistory.Sum(m => EstimateTokenCount(m.Content ?? string.Empty));
    }

    /// <summary>
    /// 从 Metadata 提取实际 Token 使用量
    /// 支持 OpenAI 兼容的 ChatTokenUsage 格式
    /// </summary>
    /// <param name="metadata">消息元数据</param>
    /// <returns>输入和输出 Token 数量元组，如不可用则返回 null</returns>
    private static TokenUsageMetadata ExtractTokenUsageFromMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata == null)
        {
            return new TokenUsageMetadata(null, null, null);
        }

        // 尝试从 "Usage" 键获取 Token 使用量（Semantic Kernel 标准格式）
        if (metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
        {
            var inputTokens = TryGetIntProperty(usageObj, InputTokenPropertyNames);
            var outputTokens = TryGetIntProperty(usageObj, OutputTokenPropertyNames);
            var cachedPromptTokens = TryGetIntProperty(usageObj, CachedTokenPropertyNames)
                ?? TryGetNestedIntProperty(usageObj, TokenDetailsPropertyNames, CachedTokenPropertyNames);

            return new TokenUsageMetadata(inputTokens, outputTokens, cachedPromptTokens);
        }

        return new TokenUsageMetadata(null, null, null);
    }

    private static int? TryGetNestedIntProperty(
        object source,
        IReadOnlyList<string> containerNames,
        IReadOnlyList<string> valueNames)
    {
        foreach (var containerName in containerNames)
        {
            var container = source.GetType().GetProperty(containerName)?.GetValue(source);
            if (container == null)
            {
                continue;
            }

            var value = TryGetIntProperty(container, valueNames);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private static int? TryGetIntProperty(object source, IReadOnlyList<string> names)
    {
        if (source is JsonElement jsonElement)
        {
            return TryGetIntFromJson(jsonElement, names);
        }

        foreach (var name in names)
        {
            var property = source.GetType().GetProperty(name);
            var value = property?.GetValue(source);
            if (value == null)
            {
                continue;
            }

            if (value is JsonElement nestedJson)
            {
                return TryGetIntFromJson(nestedJson, names);
            }

            if (int.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? TryGetIntFromJson(JsonElement element, IReadOnlyList<string> names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)
                && !element.TryGetProperty(ToSnakeCase(name), out value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }
        }

        return null;
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var character in value)
        {
            if (char.IsUpper(character) && builder.Length > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 记录 API 错误的详细信息
    /// </summary>
    private void LogApiError(Exception ex, Guid providerId, ChatHistory chatHistory)
    {
        var providerInfo = _providerFactory.GetCurrentProviderInfo();
        var nonJsonStartChar = TryGetInvalidJsonStartCharacter(ex);

        _logger.LogError(
            ex,
            "[AI.Kernel.Error] API call failed | ProviderId={ProviderId} Model={Model} BaseUrl={BaseUrl} MessageCount={Count} ExceptionType={ExceptionType}",
            providerId,
            providerInfo?.ModelName ?? "unknown",
            providerInfo?.BaseUrl ?? "unknown",
            chatHistory.Count,
            ex.GetType().Name);

        // 尝试提取更多错误详情
        if (ex is System.ClientModel.ClientResultException clientEx)
        {
            _logger.LogError(
                "[AI.Kernel.Error] ClientResultException Details | Status={Status} BaseUrl={BaseUrl} Model={Model} Message={Message}",
                clientEx.Status,
                providerInfo?.BaseUrl ?? "unknown",
                providerInfo?.ModelName ?? "unknown",
                clientEx.Message);
        }
        else if (nonJsonStartChar.HasValue)
        {
            _logger.LogError(
                "[AI.Kernel.Error] 遇到非 JSON 响应。首字节={StartChar} BaseUrl={BaseUrl} Model={Model}。这通常说明 Provider 返回了纯文本错误、HTML 错误页或 SSE 数据流。请检查 Endpoint 是否指向标准 OpenAI Chat Completions JSON 接口，确认没有误填 /chat/completions 全路径、没有把流式接口当非流式接口调用，且网关/模型服务不会返回文本错误串。",
                nonJsonStartChar.Value,
                providerInfo?.BaseUrl ?? "unknown",
                providerInfo?.ModelName ?? "unknown");
        }

        // 记录 ChatHistory 摘要用于诊断
        var summary = chatHistory.Select((m, i) => new
        {
            Index = i,
            Role = m.Role.ToString(),
            ContentLength = m.Content?.Length ?? 0,
            IsEmpty = string.IsNullOrWhiteSpace(m.Content)
        });

        try
        {
            var summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = false });
            _logger.LogError(
                "[AI.Kernel.Error] ChatHistory Summary: {Summary}",
                summaryJson);
        }
        catch
        {
            // 忽略序列化错误
        }
    }

    /// <summary>
    /// 将 OpenAI SDK 的非 JSON 响应异常转换为更易诊断的异常。
    /// </summary>
    private Exception WrapNonJsonResponseException(Exception ex)
    {
        var invalidStartChar = TryGetInvalidJsonStartCharacter(ex);
        if (!invalidStartChar.HasValue)
        {
            return ex;
        }

        var providerInfo = _providerFactory.GetCurrentProviderInfo();
        var message =
            $"LLM Provider 返回的不是合法 JSON，首字节为 '{invalidStartChar.Value}'。" +
            $" BaseUrl={providerInfo?.BaseUrl ?? "unknown"} Model={providerInfo?.ModelName ?? "unknown"}。" +
            " 这通常表示 Endpoint 配置错误、命中了 SSE/流式接口，或上游网关返回了纯文本错误。";

        return new InvalidOperationException(message, ex);
    }

    /// <summary>
    /// 尝试从异常消息中提取非法 JSON 起始字符。
    /// </summary>
    private static char? TryGetInvalidJsonStartCharacter(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is not JsonException jsonException)
            {
                continue;
            }

            const string prefix = "'";
            const string suffix = "' is an invalid start of a value";
            var message = jsonException.Message;
            var suffixIndex = message.IndexOf(suffix, StringComparison.Ordinal);
            if (suffixIndex <= 1)
            {
                continue;
            }

            var startIndex = message.LastIndexOf(prefix, suffixIndex - 1, StringComparison.Ordinal);
            if (startIndex < 0 || startIndex + 1 >= message.Length)
            {
                continue;
            }

            return message[startIndex + 1];
        }

        return null;
    }
}
