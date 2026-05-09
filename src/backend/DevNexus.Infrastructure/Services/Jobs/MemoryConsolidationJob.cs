using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Hangfire;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly.Timeout;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevNexus.Infrastructure.Services.LLM;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 记忆沉淀后台任务
/// 在会话结束后分析对话，提取用户偏好和生成摘要
/// </summary>
public class MemoryConsolidationJob
{
    private readonly IUserMemoryService _memoryService;
    private readonly IChatService _chatService;
    private readonly IKernelService _kernelService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MemoryConsolidationJob> _logger;

    public MemoryConsolidationJob(
        IUserMemoryService memoryService,
        IChatService chatService,
        IKernelService kernelService,
        ApplicationDbContext dbContext,
        ILogger<MemoryConsolidationJob> logger)
    {
        _memoryService = memoryService;
        _chatService = chatService;
        _kernelService = kernelService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 执行记忆沉淀
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var previousContext = TokenAuditContext.Current;
        _logger.LogInformation(
            "[MemoryConsolidation] Starting for Session={SessionId} User={UserId}",
            sessionId, userId);

        try
        {
            TokenAuditContext.Current = new TokenAuditContext
            {
                OwnerType = DevNexus.Shared.DTOs.ModelInvocationOwnerTypes.User,
                OwnerUserId = userId,
                SessionId = sessionId,
                InvocationKind = DevNexus.Shared.DTOs.ModelInvocationKinds.ChatCompletion,
                SceneCode = DevNexus.Shared.DTOs.ModelInvocationSceneCodes.MemorySessionSummary,
                SceneCategory = DevNexus.Shared.DTOs.ModelInvocationSceneCategories.Memory,
                ResourceType = DevNexus.Shared.DTOs.ModelInvocationResourceTypes.Session,
                ResourceId = sessionId.ToString()
            };

            // 0. 幂等检查 - 获取会话信息
            var session = await _dbContext.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

            if (session == null)
            {
                _logger.LogWarning("[MemoryConsolidation] Session {SessionId} not found, skipping", sessionId);
                return;
            }

            // 1. 获取会话最近的对话记录
            var messages = await _chatService.GetChatMessagesAsync(sessionId, cancellationToken);
            var currentMessageCount = messages.Count;

            // 幂等检查：如果消息数没有变化，跳过
            if (currentMessageCount <= session.LastConsolidatedMessageCount)
            {
                _logger.LogDebug(
                    "[MemoryConsolidation] Skipped - no new messages since last consolidation (current={Current}, last={Last})",
                    currentMessageCount,
                    session.LastConsolidatedMessageCount);
                return;
            }

            if (messages.Count < 2)
            {
                _logger.LogDebug("[MemoryConsolidation] Skipped - too few messages ({Count})", messages.Count);
                return;
            }

            // 构建对话文本
            var conversationText = BuildConversationText(messages);

            // 2. 调用 LLM 提取用户偏好
            var extractedFacts = await ExtractUserFactsAsync(conversationText, cancellationToken);

            // 3. Upsert 到 UserFacts 表
            var savedFactCount = 0;
            foreach (var fact in extractedFacts)
            {
                if (!string.IsNullOrWhiteSpace(fact.Category) && !string.IsNullOrWhiteSpace(fact.Content))
                {
                    await _memoryService.UpsertFactAsync(
                        userId,
                        fact.Category,
                        fact.Content,
                        sessionId,
                        cancellationToken);
                    savedFactCount++;
                }
            }

            // 4. 生成对话摘要
            var summary = await GenerateSummaryAsync(conversationText, cancellationToken);

            if (!string.IsNullOrWhiteSpace(summary))
            {
                // 5. 提取技术标签
                var tags = await ExtractTagsAsync(conversationText, cancellationToken);

                // 6. 存入 Qdrant
                await _memoryService.SaveEpisodicMemoryAsync(
                    userId, sessionId, summary, tags, cancellationToken);

                _logger.LogInformation(
                    "[MemoryConsolidation] Completed - Facts={FactCount} Summary={SummaryLength} Tags={Tags}",
                    savedFactCount, summary.Length, string.Join(", ", tags));
            }
            else
            {
                _logger.LogDebug("[MemoryConsolidation] No meaningful summary generated, skipping episodic memory save");
            }

            // 7. 更新会话的沉淀跟踪状态
            session.LastConsolidatedMessageCount = currentMessageCount;
            session.LastConsolidatedAt = DateTime.UtcNow;
            session.MemoryConsolidationJobId = null; // 清除已完成的任务ID
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "[MemoryConsolidation] Updated session tracking | SessionId={SessionId} MessageCount={Count}",
                sessionId,
                currentMessageCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MemoryConsolidation] Failed for Session={SessionId}", sessionId);
            throw;
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <summary>
    /// 构建对话文本
    /// </summary>
    private static string BuildConversationText(List<ChatMessageDto> messages)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var msg in messages.OrderBy(m => m.CreatedAt).TakeLast(20)) // 只取最近20条
        {
            var role = ChatConstants.IsUserSender(msg.SenderType) ? "用户" : "助手";
            sb.AppendLine($"{role}: {msg.Content}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 从对话中提取用户偏好
    /// </summary>
    private async Task<List<ExtractedUserFact>> ExtractUserFactsAsync(
        string conversationText,
        CancellationToken cancellationToken)
    {
        var previousContext = TokenAuditContext.Current;
        try
        {
            TokenAuditContext.Current = BuildChildContext(
                previousContext,
                DevNexus.Shared.DTOs.ModelInvocationSceneCodes.MemoryUserFactExtract);
            var prompt = string.Format(PromptConstants.Memory.UserFactExtractionPrompt, conversationText);
            var result = await _kernelService.GenerateTextAsync(prompt, cancellationToken: cancellationToken);

            // 尝试解析 JSON
            var jsonStart = result.IndexOf('[');
            var jsonEnd = result.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = result[jsonStart..(jsonEnd + 1)];
                var facts = JsonSerializer.Deserialize<List<ExtractedUserFact>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return facts ?? new List<ExtractedUserFact>();
            }

            return new List<ExtractedUserFact>();
        }
        catch (TimeoutRejectedException ex)
        {
            // LLM 超时属于最佳努力失败，改为跳过本轮事实提取。
            _logger.LogWarning(ex, "[MemoryConsolidation] 用户偏好提取超时，已回退为空结果");
            return new List<ExtractedUserFact>();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] 用户偏好提取被取消或超时，已回退为空结果");
            return new List<ExtractedUserFact>();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] 用户偏好提取中断，已回退为空结果");
            return new List<ExtractedUserFact>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] Failed to extract user facts");
            return new List<ExtractedUserFact>();
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <summary>
    /// 生成对话摘要
    /// </summary>
    private async Task<string> GenerateSummaryAsync(
        string conversationText,
        CancellationToken cancellationToken)
    {
        var previousContext = TokenAuditContext.Current;
        try
        {
            TokenAuditContext.Current = BuildChildContext(
                previousContext,
                DevNexus.Shared.DTOs.ModelInvocationSceneCodes.MemorySessionSummary);
            var prompt = string.Format(PromptConstants.Memory.EpisodicSummaryPrompt, conversationText);
            var result = await _kernelService.GenerateTextAsync(prompt, cancellationToken: cancellationToken);
            return result.Trim();
        }
        catch (TimeoutRejectedException ex)
        {
            // 超时后回退为空摘要，不阻塞后续事实与标签保存。
            _logger.LogWarning(ex, "[MemoryConsolidation] 对话摘要生成超时，已回退为空摘要");
            return string.Empty;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] 对话摘要生成被取消或超时，已回退为空摘要");
            return string.Empty;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] 对话摘要生成中断，已回退为空摘要");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] Failed to generate summary");
            return string.Empty;
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <summary>
    /// 提取技术标签
    /// </summary>
    private async Task<List<string>> ExtractTagsAsync(
        string conversationText,
        CancellationToken cancellationToken)
    {
        var previousContext = TokenAuditContext.Current;
        try
        {
            TokenAuditContext.Current = BuildChildContext(
                previousContext,
                DevNexus.Shared.DTOs.ModelInvocationSceneCodes.MemorySessionTags);
            var prompt = string.Format(PromptConstants.Memory.TechTagExtractionPrompt, conversationText);
            var result = await _kernelService.GenerateTextAsync(prompt, cancellationToken: cancellationToken);

            // 尝试解析 JSON 数组
            var jsonStart = result.IndexOf('[');
            var jsonEnd = result.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = result[jsonStart..(jsonEnd + 1)];
                return ParseTagsFromJsonContent(jsonContent);
            }

            return ParseTagsFromPlainText(result);
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] 技术标签提取超时，已回退为空结果");
            return new List<string>();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] 技术标签提取被取消或超时，已回退为空结果");
            return new List<string>();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] 技术标签提取中断，已回退为空结果");
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MemoryConsolidation] Failed to extract tags");
            return new List<string>();
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <summary>
    /// 基于父上下文创建子场景上下文。
    /// </summary>
    private static TokenAuditContext BuildChildContext(TokenAuditContext? parent, string sceneCode)
    {
        return new TokenAuditContext
        {
            OwnerType = parent?.OwnerType ?? DevNexus.Shared.DTOs.ModelInvocationOwnerTypes.System,
            OwnerUserId = parent?.OwnerUserId,
            SessionId = parent?.SessionId,
            MessageId = parent?.MessageId,
            InvocationKind = parent?.InvocationKind ?? DevNexus.Shared.DTOs.ModelInvocationKinds.ChatCompletion,
            SceneCode = sceneCode,
            SceneCategory = DevNexus.Shared.DTOs.ModelInvocationSceneCategories.Memory,
            ResourceType = parent?.ResourceType ?? DevNexus.Shared.DTOs.ModelInvocationResourceTypes.Session,
            ResourceId = parent?.ResourceId,
            TraceId = parent?.TraceId,
            ParentInvocationId = parent?.ParentInvocationId,
            RootInvocationId = parent?.RootInvocationId,
            ModelName = parent?.ModelName ?? string.Empty,
            ProviderName = parent?.ProviderName ?? string.Empty,
            ProviderId = parent?.ProviderId ?? string.Empty,
            LLMProviderId = parent?.LLMProviderId ?? Guid.Empty
        };
    }

    /// <summary>
    /// 从 JSON 数组文本中解析技术标签。
    /// </summary>
    private List<string> ParseTagsFromJsonContent(string jsonContent)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }

            var tags = new List<string>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var tag = NormalizeTag(element.GetString());
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    tags.Add(tag);
                }
            }

            return DeduplicateTags(tags);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "[MemoryConsolidation] 技术标签 JSON 解析失败，回退到宽松解析");

            // LLM 偶发会输出尾随说明或格式不标准 JSON，这里回退到字符串提取，避免整个任务失败。
            var tags = Regex.Matches(jsonContent, "\"(?<tag>[^\"]+)\"")
                .Select(match => NormalizeTag(match.Groups["tag"].Value))
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToList();

            return DeduplicateTags(tags);
        }
    }

    /// <summary>
    /// 从非 JSON 文本中宽松提取技术标签。
    /// </summary>
    private List<string> ParseTagsFromPlainText(string content)
    {
        var tags = content
            .Split(new[] { '\r', '\n', ',', '，', '、' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();

        return DeduplicateTags(tags);
    }

    /// <summary>
    /// 清洗单个标签内容。
    /// </summary>
    private static string NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        return tag.Trim().Trim('"', '\'', '[', ']', '。', '.', ';', '；');
    }

    /// <summary>
    /// 标签去重并限制数量。
    /// </summary>
    private static List<string> DeduplicateTags(IEnumerable<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }
}
