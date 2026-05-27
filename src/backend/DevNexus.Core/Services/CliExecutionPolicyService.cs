using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Options;

namespace DevNexus.Core.Services;

/// <summary>
/// CLI 执行策略服务实现。
/// </summary>
public sealed class CliExecutionPolicyService : ICliExecutionPolicyService
{
    private static readonly string[] DangerousCommandPatterns =
    [
        "rm -rf /",
        "rm -rf /*",
        "mkfs",
        "dd if=/dev/zero",
        "format c:",
        "del /f /s /q c:\\",
        "shutdown /s",
        "reboot",
        "poweroff"
    ];

    private static readonly string[] DangerousCodePatterns =
    [
        "rm -rf /",
        "rm -rf /*",
        ":(){ :|:& };:",
        "mkfs",
        "dd if=/dev/zero",
        "chmod -R 777 /",
        "chown -R",
        "format c:",
        "del /f /s /q c:\\",
        "wget http",
        "curl http",
        "System.Diagnostics.Process.Start",
        "child_process.exec"
    ];

    private static readonly Regex[] StrictInlineEvalPatterns =
    [
        new(@"(?i)^\s*python(?:\d+(?:\.\d+)*)?\s+(-c|/c)\b", RegexOptions.Compiled),
        new(@"(?i)^\s*node\s+(-e|--eval)\b", RegexOptions.Compiled),
        new(@"(?i)^\s*(bash|sh)\s+-c\b", RegexOptions.Compiled),
        new(@"(?i)^\s*cmd(?:\.exe)?\s+/c\b", RegexOptions.Compiled),
        new(@"(?i)^\s*(pwsh|powershell)(?:\.exe)?\s+(-c|-command)\b", RegexOptions.Compiled)
    ];

    private readonly ICliApprovalGrantService _cliApprovalGrantService;
    private readonly CliPolicyOptions _options;
    private readonly ConcurrentDictionary<string, CommandLoopWindow> _commandFingerprints = new();

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliExecutionPolicyService(
        ICliApprovalGrantService cliApprovalGrantService,
        IOptions<CliPolicyOptions> options)
    {
        _cliApprovalGrantService = cliApprovalGrantService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string ResolveWorkingDirectory(Guid userId, string? requestedWorkingDirectory)
    {
        if (string.IsNullOrWhiteSpace(requestedWorkingDirectory))
        {
            return Directory.GetCurrentDirectory();
        }

        try
        {
            return Path.GetFullPath(requestedWorkingDirectory);
        }
        catch
        {
            return requestedWorkingDirectory.Trim();
        }
    }

    /// <inheritdoc />
    public async Task<CliExecutionPolicyResult> EvaluateCommandAsync(
        Guid userId,
        string sessionId,
        string command,
        string arguments,
        string workingDirectory,
        AgentApprovalMode approvalMode = AgentApprovalMode.AskUser,
        CancellationToken cancellationToken = default)
    {
        var effectiveWorkingDirectory = ResolveWorkingDirectory(userId, workingDirectory);
        if (!Directory.Exists(effectiveWorkingDirectory))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.WorkingDirectoryUnavailable,
                $"指定工作目录不存在或无法访问：{effectiveWorkingDirectory}",
                ToolFailureReason.PermissionDenied,
                ToolSuggestedAction.Fallback);
        }

        var fullCommand = string.IsNullOrWhiteSpace(arguments)
            ? command.Trim()
            : $"{command.Trim()} {arguments.Trim()}";
        var lowered = fullCommand.ToLowerInvariant();
        var commandFingerprint = BuildCommandFingerprint(effectiveWorkingDirectory, fullCommand);
        var commandPattern = BuildCommandPattern(effectiveWorkingDirectory, fullCommand);
        var commandRoot = ResolveCommandRoot(command, arguments);

        if (await _cliApprovalGrantService.IsApprovedAsync(
                sessionId,
                commandFingerprint,
                commandPattern,
                cancellationToken))
        {
            return CliExecutionPolicyResult.Allow(effectiveWorkingDirectory);
        }

        if (MatchesPermanentAllowlist(commandRoot, fullCommand, commandPattern))
        {
            return CliExecutionPolicyResult.Allow(effectiveWorkingDirectory);
        }

        if (_options.EnforceSafeBins
            && !string.IsNullOrWhiteSpace(commandRoot)
            && !IsSafeBin(commandRoot)
            && RequiresHumanApproval(approvalMode, isHighRisk: false))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.UnsafeCommandRequiresApproval,
                $"当前命令不在 safeBins/allowlist 中，需审批后才能执行：{fullCommand}",
                ToolFailureReason.ApprovalRequired,
                ToolSuggestedAction.RequestApproval,
                requiresHumanIntervention: true,
                commandFingerprint: commandFingerprint,
                commandPattern: commandPattern);
        }

        if (DangerousCommandPatterns.Any(pattern => lowered.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            && RequiresHumanApproval(approvalMode, isHighRisk: true))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.DangerousCommandRequiresApproval,
                $"当前命令命中高风险模式，已被策略层拦截：{fullCommand}",
                ToolFailureReason.ApprovalRequired,
                ToolSuggestedAction.RequestApproval,
                requiresHumanIntervention: true,
                commandFingerprint: commandFingerprint,
                commandPattern: commandPattern);
        }

        if (StrictInlineEvalPatterns.Any(pattern => pattern.IsMatch(fullCommand))
            && RequiresHumanApproval(approvalMode, isHighRisk: false))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.StrictInlineEvalRequiresApproval,
                $"当前命令命中 strict inline eval 策略，不能直接执行：{fullCommand}",
                ToolFailureReason.ApprovalRequired,
                ToolSuggestedAction.RequestApproval,
                requiresHumanIntervention: true,
                commandFingerprint: commandFingerprint,
                commandPattern: commandPattern);
        }

        if (IsLooping(sessionId, effectiveWorkingDirectory, fullCommand))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.RepeatedCommandLoop,
                $"检测到重复命令循环，已阻止继续执行：{fullCommand}",
                ToolFailureReason.FatalExecutionError,
                ToolSuggestedAction.Abort,
                commandFingerprint: commandFingerprint,
                commandPattern: commandPattern);
        }

        return CliExecutionPolicyResult.Allow(effectiveWorkingDirectory);
    }

    /// <inheritdoc />
    public CliExecutionPolicyResult EvaluateCodeContent(
        string language,
        string code,
        AgentApprovalMode approvalMode = AgentApprovalMode.AskUser)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.EmptyCodeContent,
                "代码内容不能为空。",
                ToolFailureReason.FatalExecutionError,
                ToolSuggestedAction.Abort);
        }

        if (DangerousCodePatterns.Any(pattern => code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            && RequiresHumanApproval(approvalMode, isHighRisk: true))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.DangerousCodeRequiresApproval,
                $"代码内容命中高风险模式，已被策略层拦截：{language}",
                ToolFailureReason.ApprovalRequired,
                ToolSuggestedAction.RequestApproval,
                requiresHumanIntervention: true);
        }

        return CliExecutionPolicyResult.Allow(string.Empty);
    }

    private bool RequiresHumanApproval(AgentApprovalMode approvalMode, bool isHighRisk)
    {
        return approvalMode switch
        {
            AgentApprovalMode.AskUser => true,
            AgentApprovalMode.AgentDecides => isHighRisk,
            AgentApprovalMode.FullAccess => isHighRisk && _options.AlwaysProtectHighRisk,
            _ => true
        };
    }

    private bool IsLooping(string sessionId, string workingDirectory, string command)
    {
        var now = DateTime.UtcNow;
        var fingerprint = $"{sessionId}|{workingDirectory}|{command}".ToLowerInvariant();

        var window = _commandFingerprints.AddOrUpdate(
            fingerprint,
            _ => new CommandLoopWindow(1, now, now),
            (_, existing) =>
            {
                if (now - existing.LastSeenAt > TimeSpan.FromMinutes(2))
                {
                    return new CommandLoopWindow(1, now, now);
                }

                return existing with
                {
                    Count = existing.Count + 1,
                    LastSeenAt = now
                };
            });

        return window.Count >= 3;
    }

    private bool IsSafeBin(string commandRoot)
    {
        return _options.SafeBins.Any(bin => string.Equals(
            NormalizeCommandToken(bin),
            commandRoot,
            StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesPermanentAllowlist(string commandRoot, string fullCommand, string commandPattern)
    {
        if (_options.PermanentAllowedCommandPatterns.Length == 0)
        {
            return false;
        }

        return _options.PermanentAllowedCommandPatterns.Any(pattern =>
        {
            var normalizedPattern = pattern.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedPattern))
            {
                return false;
            }

            return string.Equals(normalizedPattern, commandRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedPattern, commandPattern, StringComparison.OrdinalIgnoreCase)
                || fullCommand.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ResolveCommandRoot(string command, string arguments)
    {
        var commandText = string.IsNullOrWhiteSpace(command)
            ? arguments
            : command;
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return string.Empty;
        }

        var token = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        return NormalizeCommandToken(token);
    }

    private static string NormalizeCommandToken(string token)
    {
        var normalized = token.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(normalized))
        {
            normalized = Path.GetFileName(normalized);
        }

        return normalized.ToLowerInvariant();
    }

    private static string BuildCommandFingerprint(string workingDirectory, string command)
    {
        return $"{workingDirectory}|{command}".ToLowerInvariant();
    }

    private static string BuildCommandPattern(string workingDirectory, string command)
    {
        var commandRoot = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? command;
        return $"{workingDirectory}|{commandRoot}".ToLowerInvariant();
    }

    private sealed record CommandLoopWindow(int Count, DateTime FirstSeenAt, DateTime LastSeenAt);
}
