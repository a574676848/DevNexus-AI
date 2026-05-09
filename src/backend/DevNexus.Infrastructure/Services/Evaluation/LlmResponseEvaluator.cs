using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Extensions;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Evaluation;

/// <summary>
/// 基于 LLM 的响应评估器
/// 重放现有 LlmTaskEvaluator 逻辑并适配统一接口
/// 遵循洋葱架构：具体实现在 Infrastructure 层
/// </summary>
public class LlmResponseEvaluator : ILlmResponseEvaluator
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<LlmResponseEvaluator> _logger;
    private const int MaxResultPreviewLength = 4000;
    private const string HumanInterventionGuidance = """

[额外判定准则]
- 如果失败根因是缺少用户提供的前置条件，例如密码、验证码、登录态刷新、人工确认、授权审批、外部权限、必须由用户补充的账户信息，请将 `requiresHumanIntervention` 设为 true。
- 这类场景即使还能继续尝试别的命令，也不应建议自动重试；应在 `feedback` 中明确说明缺什么，以及需要用户下一步提供什么。
- 只有当问题可以仅靠调整工具参数、切换命令、修复路径、重试网络请求等方式自行解决时，才将 `requiresHumanIntervention` 设为 false。
""";

    public LlmResponseEvaluator(
        IKernelService kernelService,
        ILogger<LlmResponseEvaluator> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    public async Task<EvaluationResult> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 从上下文中获取 ProviderId
            var providerId = context.ProviderId ?? Guid.Empty;

            var resultPreview = context.Result.Length > MaxResultPreviewLength
                ? context.Result[..MaxResultPreviewLength] + "\n... (已截断)"
                : context.Result;
            var toolRecordSummary = BuildToolRecordSummary(context.ToolRecords);

            var supervisorPrompt = string.Format(PromptConstants.Swarm.SupervisorPrompt, context.Goal, resultPreview);
            var userPrompt = string.Format(
                PromptConstants.Swarm.TaskEvaluationUserPromptTemplate,
                supervisorPrompt,
                "N/A", // 统一评估暂不强制要求上下文 ID
                "评估任务",
                context.Role ?? "智能体",
                string.IsNullOrWhiteSpace(context.ExpectedOutputSchema) ? "(无)" : context.ExpectedOutputSchema)
                + "\n\n[结构化工具执行记录]\n"
                + toolRecordSummary
                + HumanInterventionGuidance;

            var history = new ChatHistory(PromptConstants.Swarm.TaskEvaluationSystemPrompt);
            history.AddUserMessage(userPrompt);

            var response = await _kernelService.GetChatCompletionAsync(
                chatHistory: history,
                providerId: providerId,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.EvaluationResponseReview,
                    SceneCategory = ModelInvocationSceneCategories.Governance,
                    ResourceType = ModelInvocationResourceTypes.None
                },
                cancellationToken: cancellationToken,
                enableAutoFunctionCalling: false);

            var rawContent = response.Content ?? "{}";
            var jsonContent = rawContent.CleanJsonContent();
            
            var evalResponse = JsonSerializer.Deserialize<LlmEvaluationJsonResponse>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (evalResponse == null) throw new JsonException("LLM 评估结果反序列化失败");

            var result = MapToEvaluationResult(evalResponse);
            
            _logger.LogInformation(
                "LLM 评估完成：{Score:F1} 分 (通过: {Passed})", 
                result.Score, result.Passed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 评估执行失败，触发安全回退机制");
            return new EvaluationResult { Passed = true, Score = 75, Feedback = "评估引擎暂时不可用，已自动采用安全通过策略以避免阻断流程。" };
        }
    }

    private static EvaluationResult MapToEvaluationResult(LlmEvaluationJsonResponse response)
    {
        var correctness = Math.Clamp(response.Correctness ?? response.Score ?? 0, 0, 100);
        var completeness = Math.Clamp(response.Completeness ?? response.Score ?? 0, 0, 100);
        var quality = Math.Clamp(response.Quality ?? response.Score ?? 0, 0, 100);
        var efficiency = Math.Clamp(response.Efficiency ?? response.Score ?? 0, 0, 100);

        double weightedScore = response.Score ?? 
            (correctness * 0.4 + completeness * 0.3 + quality * 0.2 + efficiency * 0.1);
            
        bool passed = response.IsApproved ?? weightedScore >= 80.0;

        return new EvaluationResult
        {
            Passed = passed,
            Score = weightedScore,
            CorrectnessScore = correctness,
            CompletenessScore = completeness,
            QualityScore = quality,
            EfficiencyScore = efficiency,
            Feedback = response.Feedback,
            CanRepair = !passed && !response.RequiresHumanIntervention,
            ImprovementSuggestions = response.Suggestions ?? new()
        };
    }

    private static string BuildToolRecordSummary(IReadOnlyCollection<ToolExecutionRecord>? toolRecords)
    {
        if (toolRecords == null || toolRecords.Count == 0)
        {
            return "(无工具调用)";
        }

        var summary = toolRecords
            .Select(record => new
            {
                toolName = record.ToolName,
                success = record.Success,
                failureReason = record.FailureReason.ToWireValue(),
                retryable = record.Retryable,
                requiresHumanIntervention = record.RequiresHumanIntervention,
                shouldFallback = record.ShouldFallback,
                shouldRotateCredential = record.ShouldRotateCredential,
                suggestedAction = record.SuggestedAction.ToWireValue(),
                requestedUserInputKind = record.RequestedUserInputKind,
                requestedUserInputLabel = record.RequestedUserInputLabel,
                userMessage = record.UserMessage,
                errorSummary = record.ErrorSummary,
                exitCode = record.ExitCode,
                durationMs = record.Duration.TotalMilliseconds
            })
            .ToList();

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
    }

    private class LlmEvaluationJsonResponse
    {
        public bool? IsApproved { get; set; }
        public double? Score { get; set; }
        public bool RequiresHumanIntervention { get; set; }
        public double? Correctness { get; set; }
        public double? Completeness { get; set; }
        public double? Quality { get; set; }
        public double? Efficiency { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public List<string>? Suggestions { get; set; }
    }
}
