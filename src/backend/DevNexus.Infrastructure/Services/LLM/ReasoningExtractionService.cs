using DevNexus.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// 推理内容提取服务实现
/// 基于适配器模式，封装对不同 LLM SDK 版本和厂商的兼容性逻辑
/// </summary>
public class ReasoningExtractionService : IReasoningExtractionService
{
    private readonly ILogger<ReasoningExtractionService> _logger;

    public ReasoningExtractionService(ILogger<ReasoningExtractionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string? Extract(IReadOnlyDictionary<string, object?>? metadata, object? innerContent)
    {
        try
        {
            // 1. 检查 Metadata 中的常见推理字段（OpenAI O1/O3、DeepSeek 以及通用 thinking 块）
            if (metadata != null)
            {
                // OpenAI O1/O3: reasoning_content 字段
                if (metadata.TryGetValue("reasoning_content", out var reasoning) && reasoning != null)
                {
                    var content = reasoning.ToString();
                    if (!string.IsNullOrEmpty(content)) return content;
                }

                // DeepSeek: reasoning 字段
                if (metadata.TryGetValue("reasoning", out var deepseekReasoning) && deepseekReasoning != null)
                {
                    var content = deepseekReasoning.ToString();
                    if (!string.IsNullOrEmpty(content)) return content;
                }

                // 通用 thinking 块
                if (metadata.TryGetValue("thinking", out var thinking) && thinking != null)
                {
                    var content = thinking.ToString();
                    if (!string.IsNullOrEmpty(content)) return content;
                }

                // 通用: ContentType 或 ContentRole 为 reasoning/thinking
                if (metadata.TryGetValue("ContentType", out var contentType))
                {
                    var typeStr = contentType?.ToString()?.ToLowerInvariant();
                    if (typeStr == "reasoning" || typeStr == "thinking" || typeStr == "thought")
                    {
                        if (metadata.TryGetValue("Content", out var typeContent) && typeContent != null)
                        {
                            return typeContent.ToString();
                        }
                    }
                }
            }

            // 2. [反射] 深度检查 InnerContent
            if (innerContent != null)
            {
                return ExtractFromInnerContent(innerContent);
            }
        }
        catch (Exception ex)
        {
            // 防御性：推理提取失败绝不应阻断主流程
            _logger.LogWarning(ex, "[AI.Reasoning] Failed to extract reasoning content from response");
        }

        return null;
    }

    /// <summary>
    /// 通过反射从 InnerContent 中尝试提取 (包含降级策略)
    /// </summary>
    private string? ExtractFromInnerContent(object innerContent)
    {
        var innerType = innerContent.GetType();

        // 2.1 检查公共属性 (Public Properties) - SDK 友好模式
        var publicProps = new[] { "ReasoningContent", "Reasoning" };
        foreach (var propName in publicProps)
        {
            try
            {
                var prop = innerType.GetProperty(propName);
                if (prop?.GetValue(innerContent)?.ToString() is string val && !string.IsNullOrEmpty(val))
                    return val;
            }
            catch
            {
                // 忽略单个属性读取错误
            }
        }

        // 2.2 [关键] 检查公共扩展字典 (Public Dictionary)
        // System.ClientModel 有时会将数据放在 AdditionalBinaryDataProperties 中
        var dictProps = new[] { "AdditionalBinaryDataProperties", "AdditionalProperties" };
        foreach (var propName in dictProps)
        {
            try
            {
                var prop = innerType.GetProperty(propName);
                if (prop != null)
                {
                    var dictValue = prop.GetValue(innerContent);
                    
                    if (dictValue is IDictionary<string, BinaryData> binaryDict)
                    {
                        if (binaryDict.TryGetValue("reasoning_content", out var bd) || binaryDict.TryGetValue("reasoning", out bd))
                        {
                            return bd.ToString(); // BinaryData.ToString() 通常能转回文本
                        }
                    }
                    if (dictValue is IDictionary<string, object> objDict)
                    {
                        if (objDict.TryGetValue("reasoning_content", out var od) || objDict.TryGetValue("reasoning", out od))
                        {
                            return od.ToString();
                        }
                    }
                }
            }
            catch
            {
                // 忽略字典读取错误
            }
        }

        // 2.3 [最后的手段] 检查私有字段 (Private Fields) - 针对 OpenAI SDK v2
        // 这一步最脆弱，加了 TryCatch 保护
        var privateFields = new[] { "_serializedAdditionalRawData", "_additionalBinaryDataProperties" };
        foreach (var fieldName in privateFields)
        {
            try
            {
                var field = innerType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var fieldValue = field.GetValue(innerContent);

                    if (fieldValue is IDictionary<string, BinaryData> binDict)
                    {
                        if (binDict.TryGetValue("reasoning_content", out var val) || binDict.TryGetValue("reasoning", out val))
                            return val.ToString();
                    }
                    if (fieldValue is IDictionary<string, object> objDict)
                    {
                        if (objDict.TryGetValue("reasoning_content", out var val) || objDict.TryGetValue("reasoning", out val))
                            return val.ToString();
                    }
                }
            }
            catch
            {
                // 忽略私有字段读取错误
            }
        }

        return null;
    }
}
