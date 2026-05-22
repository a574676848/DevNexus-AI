using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using DevNexus.Shared.DTOs;
using DevNexus.Core.Services.Chat;
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
    public Task DistillSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return DistillSessionAsync(
            sessionId,
            ExperienceDistillationScheduleContext.Empty,
            cancellationToken);
    }

    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task DistillSessionAsync(
        Guid sessionId,
        string candidateReason,
        string contextPressureReason,
        string contextCompressionSummaryFingerprint,
        CancellationToken cancellationToken)
    {
        var scheduleContext = ExperienceDistillationScheduleContext.Create(
            candidateReason,
            contextPressureReason,
            contextCompressionSummaryFingerprint);

        return DistillSessionAsync(sessionId, scheduleContext, cancellationToken);
    }

    private async Task DistillSessionAsync(
        Guid sessionId,
        ExperienceDistillationScheduleContext scheduleContext,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DistillationJob] 开始评估会话 {SessionId} 的经验提取价值 | " +
            "CandidateReason={CandidateReason} ContextPressureReason={ContextPressureReason} " +
            "ContextCompressionSummaryFingerprint={ContextCompressionSummaryFingerprint}",
            sessionId,
            scheduleContext.CandidateReason,
            scheduleContext.ContextPressureReason,
            scheduleContext.ContextCompressionSummaryFingerprint);
        var messages = await _dbContext.ChatMessages
            .Where(m => m.ChatSessionId == sessionId && m.Status == ChatConstants.StatusCompleted)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        if (messages.Count < 2)
        {
            LogSkippedReview(
                sessionId,
                SelfIterationSkipReasons.TooFewMessages,
                scheduleContext);
            return;
        }

        // 如果包含 SwarmMode 标记
        var isSwarm = messages.Any(m => ChatMessageMetadataKeys.IsSwarmMode(m.Metadata));
        if (isSwarm)
        {
            LogSkippedReview(
                sessionId,
                SelfIterationSkipReasons.SwarmSession,
                scheduleContext);
            return;
        }

        var qaPair = ExperienceDistillationQaPairSelector.SelectLatestCompletedPair(
            messages.Select(message => new ExperienceDistillationQaMessage
            {
                SenderType = message.SenderType,
                Text = message.Content.GetValueOrDefault("text")?.ToString() ?? string.Empty
            }).ToList());

        if (qaPair == null)
        {
            LogSkippedReview(
                sessionId,
                SelfIterationSkipReasons.MissingQaPair,
                scheduleContext);
            return;
        }
        var qText = qaPair.Question;
        var aText = qaPair.Answer;

        var admission = ExperienceDistillationAdmissionPolicy.Decide(qText, aText);
        if (!admission.ShouldDistill)
        {
            _logger.LogDebug(
                "[DistillationJob] 会话 {SessionId} 不满足提纯准入 | Reason={Reason} " +
                "MatchedSkipCondition={MatchedSkipCondition} MatchedValueSignal={MatchedValueSignal}",
                sessionId,
                admission.Reason,
                admission.MatchedSkipConditionKeyword,
                admission.MatchedValueSignalKeyword);
            LogAdmissionSkippedReview(
                sessionId,
                admission,
                scheduleContext);
            return;
        }

        var defaultProvider = await _providerService.GetDefaultProviderAsync(cancellationToken);
        if (defaultProvider == null)
        {
            LogSkippedReview(
                sessionId,
                SelfIterationSkipReasons.ProviderMissing,
                scheduleContext);
            return;
        }

        var prompt = ExperienceDistillationPromptBuilder.Build(qText, aText);

        string result;
        try
        {
            result = await _kernelService.GenerateTextAsync(
                prompt.Content,
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
            LogSkippedReview(
                sessionId,
                SelfIterationSkipReasons.ModelTimeout,
                scheduleContext);
            return;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Polly 超时或底层 HttpClient 取消通常会落到 TaskCanceledException。
            _logger.LogWarning(
                ex,
                "[DistillationJob] 会话 {SessionId} 的 LLM 提纯被取消或超时，已跳过本次提纯",
                sessionId);
            LogSkippedReview(
                sessionId,
                SelfIterationSkipReasons.ModelCancelled,
                scheduleContext);
            return;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // 某些运行时会把超时映射成 OperationCanceledException，这里同样按最佳努力处理。
            _logger.LogWarning(
                ex,
                "[DistillationJob] 会话 {SessionId} 的 LLM 提纯被中断，已跳过本次提纯",
                sessionId);
            LogSkippedReview(
                sessionId,
                SelfIterationSkipReasons.ModelInterrupted,
                scheduleContext);
            return;
        }

        var parseResult = ExperienceDistillationParser.Parse(result);
        if (!parseResult.HasValue)
        {
            _logger.LogInformation(
                "[DistillationJob] 会话 {SessionId} 无提纯价值 | Reason={Reason}",
                sessionId,
                parseResult.Reason);
            LogAdmittedSkippedReview(
                sessionId,
                parseResult.Reason,
                admission,
                scheduleContext,
                prompt.Fingerprint);
            return;
        }

        var exp = ExperienceDistillationExperienceFactory.CreateQaExperience(
            parseResult,
            DateTime.UtcNow,
            scheduleContext,
            admission.MatchedValueSignalKeyword,
            sessionId,
            prompt.Fingerprint);

        var saveResult = await _memoryService.SaveExperienceAsync(exp, cancellationToken);
        var reviewDecision = SelfIterationReviewPolicy.Decide(saveResult);
        _logger.LogInformation(
            "[AI.SelfIteration.Review] Experience distillation completed | SessionId={SessionId} Intent={Intent} " +
            "Protocol={Protocol} SaveReason={SaveReason} ReviewReason={ReviewReason} HasNewExperience={HasNewExperience} " +
            "ObserveOnly={ObserveOnly} RequiresRepairAttention={RequiresRepairAttention} VectorIndexed={VectorIndexed} " +
            "CandidateReason={CandidateReason} ContextPressureReason={ContextPressureReason} " +
            "ContextCompressionSummaryFingerprint={ContextCompressionSummaryFingerprint} " +
            "MatchedValueSignal={MatchedValueSignal} DistillationPromptFingerprint={DistillationPromptFingerprint} " +
            "CitationFingerprint={CitationFingerprint} AttemptCitationFingerprint={AttemptCitationFingerprint}",
            sessionId,
            parseResult.Intent,
            ExperienceDistillationOutputProtocol.Version,
            saveResult.Reason,
            reviewDecision.Reason,
            reviewDecision.HasNewExperience,
            reviewDecision.ShouldObserveOnly,
            reviewDecision.RequiresRepairAttention,
            saveResult.VectorIndexed,
            scheduleContext.CandidateReason,
            scheduleContext.ContextPressureReason,
            scheduleContext.ContextCompressionSummaryFingerprint,
            admission.MatchedValueSignalKeyword,
            prompt.Fingerprint,
            saveResult.CitationFingerprint,
            saveResult.AttemptCitationFingerprint);
    }

    private void LogSkippedReview(
        Guid sessionId,
        string skipReason,
        ExperienceDistillationScheduleContext scheduleContext)
    {
        var reviewDecision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(skipReason);
        _logger.LogInformation(
            "[AI.SelfIteration.Review] Experience distillation skipped | SessionId={SessionId} " +
            "Protocol={Protocol} SkipReason={SkipReason} ReviewReason={ReviewReason} " +
            "HasNewExperience={HasNewExperience} ObserveOnly={ObserveOnly} RequiresRepairAttention={RequiresRepairAttention} " +
            "CandidateReason={CandidateReason} ContextPressureReason={ContextPressureReason} " +
            "ContextCompressionSummaryFingerprint={ContextCompressionSummaryFingerprint}",
            sessionId,
            ExperienceDistillationOutputProtocol.Version,
            skipReason,
            reviewDecision.Reason,
            reviewDecision.HasNewExperience,
            reviewDecision.ShouldObserveOnly,
            reviewDecision.RequiresRepairAttention,
            scheduleContext.CandidateReason,
            scheduleContext.ContextPressureReason,
            scheduleContext.ContextCompressionSummaryFingerprint);
    }

    private void LogAdmissionSkippedReview(
        Guid sessionId,
        ExperienceDistillationAdmissionDecision admission,
        ExperienceDistillationScheduleContext scheduleContext)
    {
        var reviewDecision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(admission.Reason);
        _logger.LogInformation(
            "[AI.SelfIteration.Review] Experience distillation skipped | SessionId={SessionId} " +
            "Protocol={Protocol} SkipReason={SkipReason} ReviewReason={ReviewReason} " +
            "HasNewExperience={HasNewExperience} ObserveOnly={ObserveOnly} RequiresRepairAttention={RequiresRepairAttention} " +
            "CandidateReason={CandidateReason} ContextPressureReason={ContextPressureReason} " +
            "ContextCompressionSummaryFingerprint={ContextCompressionSummaryFingerprint} " +
            "MatchedSkipCondition={MatchedSkipCondition} MatchedValueSignal={MatchedValueSignal}",
            sessionId,
            ExperienceDistillationOutputProtocol.Version,
            admission.Reason,
            reviewDecision.Reason,
            reviewDecision.HasNewExperience,
            reviewDecision.ShouldObserveOnly,
            reviewDecision.RequiresRepairAttention,
            scheduleContext.CandidateReason,
            scheduleContext.ContextPressureReason,
            scheduleContext.ContextCompressionSummaryFingerprint,
            admission.MatchedSkipConditionKeyword,
            admission.MatchedValueSignalKeyword);
    }

    private void LogAdmittedSkippedReview(
        Guid sessionId,
        string skipReason,
        ExperienceDistillationAdmissionDecision admission,
        ExperienceDistillationScheduleContext scheduleContext,
        string distillationPromptFingerprint)
    {
        var citation = SystemExperienceMemoryCitation.CreateUnpersistedDistillationCitation(
            sessionId,
            admission.MatchedValueSignalKeyword,
            distillationPromptFingerprint);
        var reviewDecision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(
            skipReason,
            citation);
        _logger.LogInformation(
            "[AI.SelfIteration.Review] Experience distillation skipped | SessionId={SessionId} " +
            "Protocol={Protocol} SkipReason={SkipReason} ReviewReason={ReviewReason} " +
            "HasNewExperience={HasNewExperience} ObserveOnly={ObserveOnly} RequiresRepairAttention={RequiresRepairAttention} " +
            "CandidateReason={CandidateReason} ContextPressureReason={ContextPressureReason} " +
            "ContextCompressionSummaryFingerprint={ContextCompressionSummaryFingerprint} " +
            "MatchedValueSignal={MatchedValueSignal} DistillationPromptFingerprint={DistillationPromptFingerprint} " +
            "CitationFingerprint={CitationFingerprint}",
            sessionId,
            ExperienceDistillationOutputProtocol.Version,
            skipReason,
            reviewDecision.Reason,
            reviewDecision.HasNewExperience,
            reviewDecision.ShouldObserveOnly,
            reviewDecision.RequiresRepairAttention,
            scheduleContext.CandidateReason,
            scheduleContext.ContextPressureReason,
            scheduleContext.ContextCompressionSummaryFingerprint,
            admission.MatchedValueSignalKeyword,
            distillationPromptFingerprint,
            reviewDecision.MemoryCitation.CitationFingerprint);
    }
}
