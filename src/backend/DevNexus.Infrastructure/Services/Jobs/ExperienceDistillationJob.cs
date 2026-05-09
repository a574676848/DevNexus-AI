using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using DevNexus.Shared.DTOs;
using Hangfire;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 后台经验提纯任务
/// 从历史会话中提取高质量的解决方案或 SOP 存入记忆库
/// </summary>
public class ExperienceDistillationJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAgentMemoryService _memoryService;
    private readonly IKernelService _kernelService;
    private readonly ILLMProviderManagementService _providerService;
    private readonly ILogger<ExperienceDistillationJob> _logger;

    public ExperienceDistillationJob(
        ApplicationDbContext dbContext,
        IAgentMemoryService memoryService,
        IKernelService kernelService,
        ILLMProviderManagementService providerService,
        ILogger<ExperienceDistillationJob> logger)
    {
        _dbContext = dbContext;
        _memoryService = memoryService;
        _kernelService = kernelService;
        _providerService = providerService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task DistillSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DistillationJob] 开始评估会话 {SessionId} 的经验提取价值", sessionId);
        var messages = await _dbContext.ChatMessages
            .Where(m => m.ChatSessionId == sessionId && m.Status == ChatConstants.StatusCompleted)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        if (messages.Count < 2) return;

        // 如果包含 SwarmMode 标记
        var isSwarm = messages.Any(m => ChatMessageMetadataKeys.IsSwarmMode(m.Metadata));
        if (isSwarm) return; // Swarm 工作包拓扑提纯不在这里，它在编排结束时已直接落库；这里主要针对 QA 对话。

        var userMessage = messages.FirstOrDefault(m => ChatConstants.IsUserSender(m.SenderType));
        var aiMessage = messages.LastOrDefault(m => ChatConstants.IsAssistantSender(m.SenderType));

        if (userMessage == null || aiMessage == null) return;
        var qText = userMessage.Content["text"]?.ToString() ?? "";
        var aText = aiMessage.Content["text"]?.ToString() ?? "";

        // 太短的内容跳过
        if (qText.Length < 10 || aText.Length < 30) return;

        var defaultProvider = await _providerService.GetDefaultProviderAsync(cancellationToken);
        if (defaultProvider == null) return;

        var prompt = $"请判断接下来的 QA 是否具有普适性的经验价值（能作为 SOP 解决同类问题）。如果有，请提纯为高质量的 SOP 教科书描述；如果没有，请回复 NONE。\n\nQ:{qText}\nA:{aText}\n\n如有价值，请在第一行输出: [INTENT]用户的核心意图提取；从第二行开始：SOP 步骤描述。";

        string result;
        try
        {
            result = await _kernelService.GenerateTextAsync(
                prompt,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SessionId = sessionId,
                    SceneCode = ModelInvocationSceneCodes.MemorySystemExperienceDistill,
                    SceneCategory = ModelInvocationSceneCategories.Memory,
                    ResourceType = ModelInvocationResourceTypes.Session,
                    ResourceId = sessionId.ToString()
                },
                cancellationToken: cancellationToken);
            result = result.Trim();
        }
        catch (TimeoutRejectedException ex)
        {
            // 经验提纯属于后台最佳努力任务，LLM 超时不应让整条作业失败。
            _logger.LogWarning(
                ex,
                "[DistillationJob] 会话 {SessionId} 的 LLM 提纯超时，已跳过本次提纯",
                sessionId);
            return;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Polly 超时或底层 HttpClient 取消通常会落到 TaskCanceledException。
            _logger.LogWarning(
                ex,
                "[DistillationJob] 会话 {SessionId} 的 LLM 提纯被取消或超时，已跳过本次提纯",
                sessionId);
            return;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // 某些运行时会把超时映射成 OperationCanceledException，这里同样按最佳努力处理。
            _logger.LogWarning(
                ex,
                "[DistillationJob] 会话 {SessionId} 的 LLM 提纯被中断，已跳过本次提纯",
                sessionId);
            return;
        }

        if (result.StartsWith("NONE") || string.IsNullOrWhiteSpace(result))
        {
            _logger.LogInformation("[DistillationJob] 会话 {SessionId} 无提纯价值", sessionId);
            return;
        }

        var lines = result.Split('\n', 2);
        if (lines.Length < 2) return;
        var intent = lines[0].Replace("[INTENT]", "").Trim();
        var sop = lines[1].Trim();

        var exp = new SystemExperience
        {
            Id = Guid.NewGuid(),
            Type = ExperienceType.QA,
            Intent = intent,
            SolutionSop = sop,
            UtilityScore = 5.0, // 初始分
            UsageCount = 0,
            LastMatchedAt = DateTime.UtcNow
        };

        await _memoryService.SaveExperienceAsync(exp, cancellationToken);
        _logger.LogInformation("[DistillationJob] 会话 {SessionId} 提纯成功，提取意图：{Intent}", sessionId, intent);
    }
}
