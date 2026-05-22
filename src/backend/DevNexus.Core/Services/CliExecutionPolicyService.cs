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

    private readonly IUserStoragePathService _userStoragePathService;
    private readonly ISkillRuntimePathResolver _skillRuntimePathResolver;
    private readonly ICliApprovalGrantService _cliApprovalGrantService;
    private readonly CliPolicyOptions _options;
    private readonly ConcurrentDictionary<string, CommandLoopWindow> _commandFingerprints = new();

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliExecutionPolicyService(
        IUserStoragePathService userStoragePathService,
        ISkillRuntimePathResolver skillRuntimePathResolver,
        ICliApprovalGrantService cliApprovalGrantService,
        IOptions<CliPolicyOptions> options)
    {
        _userStoragePathService = userStoragePathService;
        _skillRuntimePathResolver = skillRuntimePathResolver;
        _cliApprovalGrantService = cliApprovalGrantService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string ResolveWorkingDirectory(Guid userId, string? requestedWorkingDirectory)
    {
        var userTempPath = _userStoragePathService.GetUserTempPath(userId);

        if (string.IsNullOrWhiteSpace(requestedWorkingDirectory))
        {
            return userTempPath;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(requestedWorkingDirectory);
        }
        catch
        {
            return userTempPath;
        }

        if (_userStoragePathService.ValidateUserPathAccess(userId, normalizedPath))
        {
            return normalizedPath;
        }

        var mirroredPath = _skillRuntimePathResolver.TryResolveAccessiblePath(userId, normalizedPath);
        return !string.IsNullOrWhiteSpace(mirroredPath)
            ? mirroredPath
            : userTempPath;
    }

    /// <inheritdoc />
    public async Task<CliExecutionPolicyResult> EvaluateCommandAsync(
        Guid userId,
        string sessionId,
        string command,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var effectiveWorkingDirectory = ResolveWorkingDirectory(userId, workingDirectory);
        if (!_userStoragePathService.ValidateUserPathAccess(userId, effectiveWorkingDirectory)
            && string.IsNullOrWhiteSpace(_skillRuntimePathResolver.TryResolveAccessiblePath(userId, effectiveWorkingDirectory)))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.WorkingDirectoryOutOfScope,
                $"指定工作目录 '{workingDirectory}' 不在允许范围内。",
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

        if (_options.EnforceSafeBins && !string.IsNullOrWhiteSpace(commandRoot) && !IsSafeBin(commandRoot))
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

        if (DangerousCommandPatterns.Any(pattern => lowered.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
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

        var externalPathViolation = DetectExternalPathViolation(userId, fullCommand);
        if (!string.IsNullOrWhiteSpace(externalPathViolation))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.ExternalPathViolation,
                externalPathViolation,
                ToolFailureReason.PermissionDenied,
                ToolSuggestedAction.Fallback,
                commandFingerprint: commandFingerprint,
                commandPattern: commandPattern);
        }

        if (StrictInlineEvalPatterns.Any(pattern => pattern.IsMatch(fullCommand)))
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
    public CliExecutionPolicyResult EvaluateCodeContent(string language, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return CliExecutionPolicyResult.Block(
                CliExecutionPolicyDecisionCode.EmptyCodeContent,
                "代码内容不能为空。",
                ToolFailureReason.FatalExecutionError,
                ToolSuggestedAction.Abort);
        }

        if (DangerousCodePatterns.Any(pattern => code.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
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

    private string? DetectExternalPathViolation(Guid userId, string command)
    {
        foreach (var token in command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = token.Trim('"', '\'', ')', ']', '}');
            if (!LooksLikeAbsolutePath(candidate))
            {
                continue;
            }

            string normalized;
            try
            {
                normalized = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (_userStoragePathService.ValidateUserPathAccess(userId, normalized))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(_skillRuntimePathResolver.TryResolveAccessiblePath(userId, normalized)))
            {
                continue;
            }

            return $"命令包含越界绝对路径，已被策略层拦截：{candidate}";
        }

        return null;
    }

    private static bool LooksLikeAbsolutePath(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (Regex.IsMatch(token, @"^[A-Za-z]:\\"))
        {
            return true;
        }

        if (!token.StartsWith('/'))
        {
            return false;
        }

        if (token.Length <= 2 && Regex.IsMatch(token, @"^/[A-Za-z]$"))
        {
            return false;
        }

        return token.Length > 1;
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
