using DevNexus.Core.Extensions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace DevNexus.Core.Services.Swarm.Analysis;

/// <summary>
/// 基于 LLM 的任务复杂度评估器
/// </summary>
public class LlmComplexityEvaluator : IComplexityEvaluator
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<LlmComplexityEvaluator> _logger;
    private const double SwarmThreshold = 60.0;
    private const double FallbackSemanticEntropy = 0.5;
    private const double FallbackSkillBreadth = 6.0;
    private const double FallbackContextDepth = 3.0;
    private const double FallbackToolComplexity = 5.0;
    private const double FallbackRiskLevel = 3.0;
    private const double FallbackTaskScale = 7.5;
    private const double FallbackStepComplexity = 7.0;

    public LlmComplexityEvaluator(IKernelService kernelService, ILogger<LlmComplexityEvaluator> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ComplexityVector> EvaluateAsync(
        string userRequest,
        Guid providerId,
        ChatHistory? history = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 构建 Prompt
            var promptHistory = new ChatHistory(DevNexus.Shared.Constants.PromptConstants.Swarm.ComplexityAnalysisSystemPrompt);

            var userPrompt = $"用户请求: {userRequest}";
            if (history != null && history.Count > 0)
            {
                var recentHistory = string.Join("\n", history.TakeLast(3).Select(m => $"{m.Role}: {m.Content}"));
                userPrompt += $"\n\n最近对话历史:\n{recentHistory}";
            }

            promptHistory.AddUserMessage(userPrompt);

            // 统一走 KernelService 出口，避免绕过审计链路。
            var result = await _kernelService.GetChatCompletionAsync(
                chatHistory: promptHistory,
                providerId: providerId,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.SwarmComplexityEvaluate,
                    SceneCategory = ModelInvocationSceneCategories.Swarm,
                    ResourceType = ModelInvocationResourceTypes.None
                },
                cancellationToken: cancellationToken,
                enableAutoFunctionCalling: false);

            var rawContent = result.Content ?? "{}";
            var jsonContent = rawContent.CleanJsonContent();
            
            _logger.LogDebug("Complexity analysis JSON preview: {JsonPreview}", 
                jsonContent.Length > 150 ? jsonContent.Substring(0, 150) + "..." : jsonContent);
            
            ComplexityAnalysisResult? analysis;
            try
            {
                analysis = JsonSerializer.Deserialize<ComplexityAnalysisResult>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse complexity analysis JSON. Raw content: {RawContent}", rawContent);
                throw new JsonException($"LLM 返回的复杂度分析结果不是有效的 JSON。响应内容: {rawContent.Substring(0, Math.Min(200, rawContent.Length))}", ex);
            }

            if (analysis == null) throw new JsonException("Failed to deserialize complexity analysis result.");

            return new ComplexityVector
            {
                PrimaryDomain = Enum.TryParse<DomainType>(analysis.Domain, true, out var domain) ? domain : DomainType.General,
                SemanticEntropy = analysis.SemanticEntropy,
                SkillBreadth = analysis.SkillBreadth,
                ContextDepth = analysis.ContextDepth,
                ToolComplexity = analysis.ToolComplexity,
                RiskLevel = analysis.RiskLevel,
                TaskScale = analysis.TaskScale,
                StepComplexity = analysis.StepComplexity
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate complexity with LLM. Falling back to explicit borderline single-agent mode.");

            return new ComplexityVector
            {
                PrimaryDomain = DomainType.General,
                SemanticEntropy = FallbackSemanticEntropy,
                SkillBreadth = FallbackSkillBreadth,
                ContextDepth = FallbackContextDepth,
                ToolComplexity = FallbackToolComplexity,
                RiskLevel = FallbackRiskLevel,
                TaskScale = FallbackTaskScale,
                StepComplexity = FallbackStepComplexity,
                IsEvaluationFallback = true,
                EvaluationFailureReason = ex.GetType().Name
            };
        }
    }

    /// <inheritdoc />
    public bool ShouldEscalateToSwarm(ComplexityVector vector)
    {
        return vector.CompositeScore >= SwarmThreshold;
    }

    private class ComplexityAnalysisResult
    {
        public string Domain { get; set; } = "General";
        public double SemanticEntropy { get; set; }
        public double SkillBreadth { get; set; }
        public double ContextDepth { get; set; }
        public double ToolComplexity { get; set; }
        public double RiskLevel { get; set; }
        public double TaskScale { get; set; }
        public double StepComplexity { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }
}
