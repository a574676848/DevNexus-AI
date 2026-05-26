using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using DevNexus.Core.Services;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevNexus.Core.Tests.Services.Cli;

/// <summary>
/// CLI 执行策略服务测试。
/// </summary>
public sealed class CliExecutionPolicyServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    /// <summary>
    /// 未在安全命令中的命令应要求审批并返回稳定裁决码。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldRequireApproval_WhenCommandIsNotSafeBin()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = true,
            SafeBins = ["dotnet"]
        });

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-1",
            "git",
            "push origin main",
            "C:\\workspace\\project");

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.UnsafeCommandRequiresApproval);
        result.FailureReason.Should().Be(ToolFailureReason.ApprovalRequired);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.RequestApproval);
        result.RequiresHumanIntervention.Should().BeTrue();
        result.CommandPattern.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 高风险命令即使在 safe bins 中也应要求审批。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldRequireApproval_WhenCommandMatchesDangerousPattern()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = true,
            SafeBins = ["rm"]
        });

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-2",
            "rm",
            "-rf /",
            "C:\\workspace\\project");

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.DangerousCommandRequiresApproval);
        result.RequiresHumanIntervention.Should().BeTrue();
    }

    /// <summary>
    /// 同一会话中重复执行同一命令应触发循环保护。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldAbort_WhenCommandLoops()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = false
        });

        for (var index = 0; index < 2; index++)
        {
            var allowed = await service.EvaluateCommandAsync(
                UserId,
                "session-3",
                "dotnet",
                "test",
                "C:\\workspace\\project");
            allowed.Allowed.Should().BeTrue();
        }

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-3",
            "dotnet",
            "test",
            "C:\\workspace\\project");

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.RepeatedCommandLoop);
        result.FailureReason.Should().Be(ToolFailureReason.FatalExecutionError);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.Abort);
    }

    private static CliExecutionPolicyService CreateService(CliPolicyOptions options)
    {
        return new CliExecutionPolicyService(
            new FakeUserStoragePathService(),
            new FakeSkillRuntimePathResolver(),
            new FakeCliApprovalGrantService(),
            Options.Create(options));
    }

    private sealed class FakeUserStoragePathService : IUserStoragePathService
    {
        public void InitializeUserStorage(Guid userId)
        {
        }

        public string GetUserTempPath(Guid userId) => "C:\\workspace\\tmp";

        public string GetUserProjectPath(Guid userId) => "C:\\workspace\\project";

        public bool IsUserPathAccessible(Guid userId, string path)
        {
            return path.StartsWith("C:\\workspace", StringComparison.OrdinalIgnoreCase);
        }

        public bool ValidateUserPathAccess(Guid userId, string path)
        {
            return path.StartsWith("C:\\workspace", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class FakeSkillRuntimePathResolver : ISkillRuntimePathResolver
    {
        public string? TryResolveAccessiblePath(Guid userId, string requestedPath) => null;
    }

    private sealed class FakeCliApprovalGrantService : ICliApprovalGrantService
    {
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
            return Task.CompletedTask;
        }
    }
}
