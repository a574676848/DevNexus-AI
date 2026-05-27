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
            Directory.GetCurrentDirectory());

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.UnsafeCommandRequiresApproval);
        result.FailureReason.Should().Be(ToolFailureReason.ApprovalRequired);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.RequestApproval);
        result.RequiresHumanIntervention.Should().BeTrue();
        result.CommandPattern.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Agent 自主决策模式可放行中风险命令。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldAllowMediumRisk_WhenAgentDecides()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = true,
            SafeBins = ["dotnet"]
        });

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-agent",
            "git",
            "status",
            Directory.GetCurrentDirectory(),
            AgentApprovalMode.AgentDecides);

        result.Allowed.Should().BeTrue();
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
            Directory.GetCurrentDirectory());

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.DangerousCommandRequiresApproval);
        result.RequiresHumanIntervention.Should().BeTrue();
    }

    /// <summary>
    /// Agent 自主决策模式仍会把高风险命令交给用户审批。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldRequireApprovalForHighRisk_WhenAgentDecides()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = true,
            SafeBins = ["rm"]
        });

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-agent-high",
            "rm",
            "-rf /",
            Directory.GetCurrentDirectory(),
            AgentApprovalMode.AgentDecides);

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.DangerousCommandRequiresApproval);
        result.RequiresHumanIntervention.Should().BeTrue();
    }

    /// <summary>
    /// 完全放权模式不触发人工审批。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldAllowHighRisk_WhenFullAccess()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = true,
            SafeBins = ["rm"],
            AlwaysProtectHighRisk = false
        });

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-full",
            "rm",
            "-rf /",
            Directory.GetCurrentDirectory(),
            AgentApprovalMode.FullAccess);

        result.Allowed.Should().BeTrue();
    }

    /// <summary>
    /// 系统级高风险保护开启时，完全放权仍会保留审批边界。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldProtectHighRisk_WhenFullAccessProtectionEnabled()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = true,
            SafeBins = ["rm"],
            AlwaysProtectHighRisk = true
        });

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-full-protected",
            "rm",
            "-rf /",
            Directory.GetCurrentDirectory(),
            AgentApprovalMode.FullAccess);

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.DangerousCommandRequiresApproval);
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
                Directory.GetCurrentDirectory());
            allowed.Allowed.Should().BeTrue();
        }

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-3",
            "dotnet",
            "test",
            Directory.GetCurrentDirectory());

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.RepeatedCommandLoop);
        result.FailureReason.Should().Be(ToolFailureReason.FatalExecutionError);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.Abort);
    }

    /// <summary>
    /// 本机工作目录不再受用户 tmp/project 边界限制。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldAllowLocalDirectoryOutsideUserWorkspace()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = false
        });
        var localDirectory = Directory.GetCurrentDirectory();

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-local",
            "dotnet",
            "--info",
            localDirectory);

        result.Allowed.Should().BeTrue();
        result.EffectiveWorkingDirectory.Should().Be(Path.GetFullPath(localDirectory));
    }

    /// <summary>
    /// 不存在的本机工作目录应返回明确的不可用裁决。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldRejectMissingLocalWorkingDirectory()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = false
        });
        var missingDirectory = Path.Combine(Path.GetTempPath(), "DevNexus-AI-missing-" + Guid.NewGuid().ToString("N"));

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-missing",
            "dotnet",
            "--info",
            missingDirectory);

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.WorkingDirectoryUnavailable);
        result.FailureReason.Should().Be(ToolFailureReason.PermissionDenied);
    }

    /// <summary>
    /// 无效的本机工作目录不能静默回落到服务默认目录。
    /// </summary>
    [Fact]
    public async Task EvaluateCommandAsync_ShouldRejectInvalidLocalWorkingDirectory()
    {
        var service = CreateService(new CliPolicyOptions
        {
            EnforceSafeBins = false
        });
        var invalidDirectory = "invalid" + '\0' + "path";

        var result = await service.EvaluateCommandAsync(
            UserId,
            "session-invalid",
            "dotnet",
            "--info",
            invalidDirectory);

        result.Allowed.Should().BeFalse();
        result.DecisionCode.Should().Be(CliExecutionPolicyDecisionCode.WorkingDirectoryUnavailable);
        result.FailureReason.Should().Be(ToolFailureReason.PermissionDenied);
    }

    private static CliExecutionPolicyService CreateService(CliPolicyOptions options)
    {
        return new CliExecutionPolicyService(
            new FakeCliApprovalGrantService(),
            Options.Create(options));
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
