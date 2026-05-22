using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 挂起交互服务测试。
/// </summary>
public sealed class PendingInteractionServiceTests
{
    /// <summary>
    /// 审批通过时即使没有表单值，也应写入恢复元数据。
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ShouldPersistResolutionMetadata_WhenApprovalPattern()
    {
        var sessionId = Guid.NewGuid();
        var interaction = new PendingInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Kind = PendingInteractionKind.Approval,
            Status = PendingInteractionStatus.Pending,
            RequestedData = new Dictionary<string, object>
            {
                ["approval"] = new Dictionary<string, object>
                {
                    ["commandFingerprint"] = "fingerprint",
                    ["commandPattern"] = "pattern"
                }
            }
        };
        var repository = new FakePendingInteractionRepository(interaction);
        var approvalGrantService = new FakeCliApprovalGrantService();
        var service = new PendingInteractionService(repository, approvalGrantService);

        var resolved = await service.ResolveAsync(
            userId: Guid.NewGuid(),
            sessionId,
            interaction.Id,
            PendingInteractionResolutionActions.ApprovePattern,
            new Dictionary<string, string?>());

        resolved.Status.Should().Be(PendingInteractionStatus.Resolved);
        resolved.ResolutionData.Should().ContainKey(PendingInteractionMetadataKeys.ResolutionAction)
            .WhoseValue.Should().Be(PendingInteractionResolutionActions.ApprovePattern);
        resolved.ResolutionData.Should().ContainKey(PendingInteractionMetadataKeys.ApprovalScope)
            .WhoseValue.Should().Be(CliApprovalGrantScope.Pattern.ToString());
        approvalGrantService.PatternGrantCount.Should().Be(1);
    }

    private sealed class FakePendingInteractionRepository : IPendingInteractionRepository
    {
        private readonly PendingInteraction _interaction;

        public FakePendingInteractionRepository(PendingInteraction interaction)
        {
            _interaction = interaction;
        }

        public Task<PendingInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == _interaction.Id ? _interaction : null);
        }

        public Task<IReadOnlyList<PendingInteraction>> GetActiveBySessionIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PendingInteraction>>(Array.Empty<PendingInteraction>());
        }

        public Task<IReadOnlyList<PendingInteraction>> GetExpiredPendingAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PendingInteraction>>(Array.Empty<PendingInteraction>());
        }

        public Task AddAsync(PendingInteraction interaction, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PendingInteraction interaction, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> UpdateActiveStatusBySessionIdAsync(
            Guid sessionId,
            PendingInteractionStatus fromStatus,
            PendingInteractionStatus toStatus,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class FakeCliApprovalGrantService : ICliApprovalGrantService
    {
        public int PatternGrantCount { get; private set; }

        public Task<bool> IsApprovedAsync(
            string sessionId,
            string commandFingerprint,
            string commandPattern,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task GrantOnceAsync(
            Guid? userId,
            Guid? chatSessionId,
            string sessionId,
            string commandFingerprint,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task GrantPatternAsync(
            Guid? userId,
            Guid? chatSessionId,
            string sessionId,
            string commandPattern,
            CancellationToken cancellationToken = default)
        {
            PatternGrantCount++;
            return Task.CompletedTask;
        }
    }
}
