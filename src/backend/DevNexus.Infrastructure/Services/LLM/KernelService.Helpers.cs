using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.LLM;

public partial class KernelService
{
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
    private static (int? InputTokens, int? OutputTokens) ExtractTokenUsageFromMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata == null)
        {
            return (null, null);
        }

        // 尝试从 "Usage" 键获取 Token 使用量（Semantic Kernel 标准格式）
        if (metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
        {
            // OpenAI SDK 返回的是 ChatTokenUsage 类型
            var usageType = usageObj.GetType();

            // 尝试获取 InputTokens 和 OutputTokens 属性
            var inputTokensProp = usageType.GetProperty("InputTokens");
            var outputTokensProp = usageType.GetProperty("OutputTokens");

            if (inputTokensProp != null && outputTokensProp != null)
            {
                var inputTokens = inputTokensProp.GetValue(usageObj) as int?;
                var outputTokens = outputTokensProp.GetValue(usageObj) as int?;

                if (inputTokens.HasValue && outputTokens.HasValue)
                {
                    return (inputTokens.Value, outputTokens.Value);
                }
            }

            // 兼容旧版字段名（PromptTokens, CompletionTokens）
            var promptTokensProp = usageType.GetProperty("PromptTokens");
            var completionTokensProp = usageType.GetProperty("CompletionTokens");

            if (promptTokensProp != null && completionTokensProp != null)
            {
                var promptTokens = promptTokensProp.GetValue(usageObj) as int?;
                var completionTokens = completionTokensProp.GetValue(usageObj) as int?;

                if (promptTokens.HasValue && completionTokens.HasValue)
                {
                    return (promptTokens.Value, completionTokens.Value);
                }
            }
        }

        return (null, null);
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
