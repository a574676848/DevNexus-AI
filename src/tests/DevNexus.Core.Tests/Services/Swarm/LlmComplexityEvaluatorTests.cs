using DevNexus.Core.Abstractions;
using DevNexus.Core.Models;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Domain.Models;
using DevNexus.Shared.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// LLM 复杂度评估器测试。
/// </summary>
public sealed class LlmComplexityEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldMarkFallback_WhenKernelFails()
    {
        var evaluator = new LlmComplexityEvaluator(
            new ThrowingKernelService(),
            NullLogger<LlmComplexityEvaluator>.Instance);
        const double lowerBoundForBorderlineFallback = 55.0;
        const double swarmThreshold = 60.0;

        var vector = await evaluator.EvaluateAsync("需要跨模块重构", Guid.NewGuid());

        vector.IsEvaluationFallback.Should().BeTrue();
        vector.EvaluationFailureReason.Should().Be(nameof(InvalidOperationException));
        vector.CompositeScore.Should().BeInRange(lowerBoundForBorderlineFallback, swarmThreshold);
        vector.SuggestedMode.Should().Be("LightCollaboration");
        evaluator.ShouldEscalateToSwarm(vector).Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldNotMarkFallback_WhenJsonIsValid()
    {
        const string responseJson = """
        {
          "domain": "Coding",
          "semanticEntropy": 0.4,
          "skillBreadth": 8,
          "contextDepth": 6,
          "toolComplexity": 7,
          "riskLevel": 5,
          "taskScale": 8,
          "stepComplexity": 7
        }
        """;
        var evaluator = new LlmComplexityEvaluator(
            new StaticKernelService(responseJson),
            NullLogger<LlmComplexityEvaluator>.Instance);

        var vector = await evaluator.EvaluateAsync("需要跨模块重构", Guid.NewGuid());

        vector.IsEvaluationFallback.Should().BeFalse();
        vector.EvaluationFailureReason.Should().BeNull();
        vector.PrimaryDomain.Should().Be(DomainType.Coding);
        vector.CompositeScore.Should().BeGreaterThan(60);
    }

    private sealed class StaticKernelService : KernelServiceStub
    {
        private readonly string _content;

        public StaticKernelService(string content)
        {
            _content = content;
        }

        public override Task<ChatMessageContent> GetChatCompletionAsync(
            ChatHistory chatHistory,
            Guid providerId,
            Guid? sessionId = null,
            Guid? messageId = null,
            Guid? userId = null,
            IEnumerable<SkillMatchResult>? matchedSkills = null,
            CancellationToken cancellationToken = default,
            bool enableAutoFunctionCalling = true,
            ModelInvocationScopeDto? auditScope = null,
            PromptOptimizationMetadataDto? promptMetadata = null)
        {
            return Task.FromResult(new ChatMessageContent(AuthorRole.Assistant, _content));
        }
    }

    private sealed class ThrowingKernelService : KernelServiceStub
    {
        public override Task<ChatMessageContent> GetChatCompletionAsync(
            ChatHistory chatHistory,
            Guid providerId,
            Guid? sessionId = null,
            Guid? messageId = null,
            Guid? userId = null,
            IEnumerable<SkillMatchResult>? matchedSkills = null,
            CancellationToken cancellationToken = default,
            bool enableAutoFunctionCalling = true,
            ModelInvocationScopeDto? auditScope = null,
            PromptOptimizationMetadataDto? promptMetadata = null)
        {
            throw new InvalidOperationException("复杂度评估模型不可用");
        }
    }

    private abstract class KernelServiceStub : IKernelService
    {
        public IAsyncEnumerable<StreamingChatMessageContent> StreamChatCompletionAsync(
            ChatHistory chatHistory,
            Guid providerId,
            Guid? sessionId = null,
            Guid? messageId = null,
            Guid? userId = null,
            IEnumerable<SkillMatchResult>? matchedSkills = null,
            CancellationToken cancellationToken = default,
            bool enableAutoFunctionCalling = true,
            ModelInvocationScopeDto? auditScope = null,
            PromptOptimizationMetadataDto? promptMetadata = null)
        {
            throw new NotSupportedException();
        }

        public virtual Task<ChatMessageContent> GetChatCompletionAsync(
            ChatHistory chatHistory,
            Guid providerId,
            Guid? sessionId = null,
            Guid? messageId = null,
            Guid? userId = null,
            IEnumerable<SkillMatchResult>? matchedSkills = null,
            CancellationToken cancellationToken = default,
            bool enableAutoFunctionCalling = true,
            ModelInvocationScopeDto? auditScope = null,
            PromptOptimizationMetadataDto? promptMetadata = null)
        {
            throw new NotSupportedException();
        }

        public Task<ChatMessageContent> GetVisionChatCompletionAsync(
            string prompt,
            string imageDataUrl,
            Guid providerId,
            CancellationToken cancellationToken = default,
            ModelInvocationScopeDto? auditScope = null)
        {
            throw new NotSupportedException();
        }

        public Task<Kernel> GetKernelAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ImageGenerationResult> GenerateImageAsync(
            string prompt,
            int width = 1024,
            int height = 1024,
            CancellationToken cancellationToken = default,
            ModelInvocationScopeDto? auditScope = null)
        {
            throw new NotSupportedException();
        }

        public Task<string> GenerateTextAsync(
            string prompt,
            CancellationToken cancellationToken = default,
            ModelInvocationScopeDto? auditScope = null)
        {
            throw new NotSupportedException();
        }

        public Task<T> RunWithAuditScopeAsync<T>(ModelInvocationScopeDto scope, Func<Task<T>> action)
        {
            throw new NotSupportedException();
        }

        public Task RunWithAuditScopeAsync(ModelInvocationScopeDto scope, Func<Task> action)
        {
            throw new NotSupportedException();
        }
    }
}
