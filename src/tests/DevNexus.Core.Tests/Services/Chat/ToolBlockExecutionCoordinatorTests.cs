using DevNexus.Core.Abstractions;
using DevNexus.Core.Models;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using System.Threading.Channels;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

public sealed class ToolBlockExecutionCoordinatorTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnRepositoryWarning_WhenWebpageUrlIsGitRepository()
    {
        var coordinator = new ToolBlockExecutionCoordinator(
            new FailingKernelService(),
            NullLogger<ToolBlockExecutionCoordinator>.Instance);

        var block = new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Metadata = new Dictionary<string, object>
            {
                [ToolBlockMetadataConstants.CardType] = ToolBlockMetadataConstants.CardTypeWebpage,
                [ToolBlockMetadataConstants.Status] = ToolBlockMetadataConstants.StatusLoading,
                [ToolBlockMetadataConstants.Url] = "https://github.com/a574676848/auto-devnexus",
                [ToolBlockMetadataConstants.Method] = "auto"
            }
        };

        var channel = Channel.CreateUnbounded<BlockDto>();

        await coordinator.HandleAsync(
            block,
            providerId: Guid.NewGuid(),
            messageId: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            channel.Writer,
            CancellationToken.None);

        channel.Reader.TryRead(out var output).Should().BeTrue();
        output!.Content.Should().NotBeNull();
        var content = output.Content!;
        content.Should().Contain("repo-parser");
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("error").GetString()
            .Should().Be(WebResourceRoutingPolicy.GitRepositoryReaderError);
        doc.RootElement.GetProperty("recommendedSkill").GetString().Should().Be("repo-parser");
        output.Metadata.Should().NotBeNull();
        output.Metadata![ToolBlockMetadataConstants.CardType].Should().Be(ToolBlockMetadataConstants.CardTypeWebpage);
    }

    private sealed class FailingKernelService : IKernelService
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

        public Task<ChatMessageContent> GetChatCompletionAsync(
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
            => throw new NotSupportedException();

        public Task<ChatMessageContent> GetVisionChatCompletionAsync(
            string prompt,
            string imageDataUrl,
            Guid providerId,
            CancellationToken cancellationToken = default,
            ModelInvocationScopeDto? auditScope = null)
            => throw new NotSupportedException();

        public Task<Kernel> GetKernelAsync(Guid providerId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Git 仓库 URL 不应触发网页阅读器后的 Kernel 调用。");

        public Task<ImageGenerationResult> GenerateImageAsync(
            string prompt,
            int width = 1024,
            int height = 1024,
            CancellationToken cancellationToken = default,
            ModelInvocationScopeDto? auditScope = null)
            => throw new NotSupportedException();

        public Task<string> GenerateTextAsync(
            string prompt,
            CancellationToken cancellationToken = default,
            ModelInvocationScopeDto? auditScope = null)
            => throw new NotSupportedException();

        public Task<T> RunWithAuditScopeAsync<T>(ModelInvocationScopeDto scope, Func<Task<T>> action)
            => throw new NotSupportedException();

        public Task RunWithAuditScopeAsync(ModelInvocationScopeDto scope, Func<Task> action)
            => throw new NotSupportedException();
    }
}
