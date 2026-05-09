using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;
using System.Text.Json;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 挂起交互定义。
/// </summary>
internal sealed record PendingInteractionDefinition
{
    /// <summary>
    /// 挂起交互类型。
    /// </summary>
    public PendingInteractionKind Kind { get; init; } = PendingInteractionKind.Unknown;

    /// <summary>
    /// 标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 说明文案。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 请求数据定义。
    /// </summary>
    public Dictionary<string, object>? RequestedData { get; init; }
}

/// <summary>
/// 挂起交互定义构造器。
/// 统一根据工具执行记录推导交互类型、文案与字段定义。
/// </summary>
internal static class PendingInteractionDefinitionBuilder
{
    /// <summary>
    /// 构造挂起交互定义。
    /// </summary>
    public static PendingInteractionDefinition Build(ToolExecutionRecord toolRecord, string? evaluationFeedback)
    {
        var kind = ResolveKind(toolRecord);
        return new PendingInteractionDefinition
        {
            Kind = kind,
            Title = ResolveTitle(kind),
            Description = ResolveDescription(toolRecord, evaluationFeedback),
            RequestedData = BuildRequestedData(toolRecord)
        };
    }

    private static PendingInteractionKind ResolveKind(ToolExecutionRecord toolRecord)
    {
        return toolRecord.SuggestedAction switch
        {
            ToolSuggestedAction.RequestApproval => PendingInteractionKind.Approval,
            ToolSuggestedAction.RefreshCredential or ToolSuggestedAction.PromptUserInput => PendingInteractionKind.Credential,
            _ => PendingInteractionKind.Clarification
        };
    }

    private static string ResolveTitle(PendingInteractionKind kind)
    {
        return kind switch
        {
            PendingInteractionKind.Approval => "等待执行审批",
            PendingInteractionKind.Credential => "等待补充凭证",
            PendingInteractionKind.Confirmation => "等待确认",
            PendingInteractionKind.OAuthCallback => "等待外部授权完成",
            _ => "等待补充信息"
        };
    }

    private static string ResolveDescription(ToolExecutionRecord toolRecord, string? evaluationFeedback)
    {
        var baseDescription =
            !string.IsNullOrWhiteSpace(toolRecord.UserMessage) ? toolRecord.UserMessage! :
            !string.IsNullOrWhiteSpace(toolRecord.ErrorSummary) ? toolRecord.ErrorSummary! :
            !string.IsNullOrWhiteSpace(evaluationFeedback) ? evaluationFeedback! :
            "当前执行需要人工介入，已暂停自动修复。";

        if (toolRecord.SuggestedAction != ToolSuggestedAction.RequestApproval)
        {
            return baseDescription;
        }

        var approvalSummary = BuildApprovalSummary(toolRecord.Arguments);
        return string.IsNullOrWhiteSpace(approvalSummary)
            ? baseDescription
            : $"{baseDescription}\n{approvalSummary}";
    }

    private static string BuildApprovalSummary(string? serializedArguments)
    {
        if (string.IsNullOrWhiteSpace(serializedArguments))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedArguments);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            var command = document.RootElement.TryGetProperty("command", out var commandProperty)
                ? commandProperty.GetString()
                : null;
            var arguments = document.RootElement.TryGetProperty("arguments", out var argumentsProperty)
                ? argumentsProperty.GetString()
                : null;
            var workingDirectory = document.RootElement.TryGetProperty("workingDirectory", out var workingDirectoryProperty)
                ? workingDirectoryProperty.GetString()
                : null;

            var fullCommand = string.IsNullOrWhiteSpace(arguments)
                ? command
                : $"{command} {arguments}".Trim();

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(fullCommand))
            {
                lines.Add($"命令：{fullCommand}");
            }

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                lines.Add($"目录：{workingDirectory}");
            }

            return string.Join('\n', lines);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, object>? BuildRequestedData(ToolExecutionRecord toolRecord)
    {
        var approval = BuildApprovalData(toolRecord.Arguments);

        if (string.IsNullOrWhiteSpace(toolRecord.RequestedUserInputKind)
            && string.IsNullOrWhiteSpace(toolRecord.RequestedUserInputLabel)
            && approval == null)
        {
            return null;
        }

        var requestedData = new Dictionary<string, object>();
        if (approval != null)
        {
            requestedData["approval"] = approval;
        }

        if (!string.IsNullOrWhiteSpace(toolRecord.RequestedUserInputKind)
            || !string.IsNullOrWhiteSpace(toolRecord.RequestedUserInputLabel))
        {
            requestedData["fields"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["key"] = "value",
                    ["type"] = toolRecord.RequestedUserInputKind ?? "text",
                    ["label"] = toolRecord.RequestedUserInputLabel ?? "必要输入",
                    ["required"] = true
                }
            };
        }

        return requestedData;
    }

    private static Dictionary<string, object>? BuildApprovalData(string? serializedArguments)
    {
        if (string.IsNullOrWhiteSpace(serializedArguments))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedArguments);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var fingerprint = document.RootElement.TryGetProperty("commandFingerprint", out var fingerprintProperty)
                ? fingerprintProperty.GetString()
                : null;
            var pattern = document.RootElement.TryGetProperty("commandPattern", out var patternProperty)
                ? patternProperty.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(fingerprint) && string.IsNullOrWhiteSpace(pattern))
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                ["commandFingerprint"] = fingerprint ?? string.Empty,
                ["commandPattern"] = pattern ?? string.Empty,
                ["defaultApprovalScope"] = "approve-once"
            };
        }
        catch
        {
            return null;
        }
    }
}
