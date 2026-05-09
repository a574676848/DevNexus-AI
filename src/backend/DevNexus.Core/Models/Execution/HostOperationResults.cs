using DevNexus.Shared.Enums;

namespace DevNexus.Core.Models.Execution;

/// <summary>
/// 宿主内部结构化操作状态。
/// </summary>
public enum HostOperationStatus
{
    Success = 0,
    Failure = 1,
    Exception = 2,
    Info = 3,
    SecurityBlocked = 4
}

/// <summary>
/// 宿主内部结构化操作结果。
/// </summary>
public record HostOperationResult
{
    /// <summary>
    /// 状态。
    /// </summary>
    public HostOperationStatus Status { get; init; } = HostOperationStatus.Success;

    /// <summary>
    /// 说明文案。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool Succeeded => Status == HostOperationStatus.Success;

    /// <summary>
    /// 结构化失败原因。
    /// </summary>
    public ToolFailureReason FailureReason { get; init; } = ToolFailureReason.None;

    /// <summary>
    /// 建议动作。
    /// </summary>
    public ToolSuggestedAction SuggestedAction { get; init; } = ToolSuggestedAction.None;

    /// <summary>
    /// 是否需要人工介入。
    /// </summary>
    public bool RequiresHumanIntervention { get; init; }
}

/// <summary>
/// 宿主文本操作结果。
/// </summary>
public sealed record HostTextOperationResult : HostOperationResult
{
    /// <summary>
    /// 文本内容。
    /// </summary>
    public string? Text { get; init; }
}

/// <summary>
/// 宿主文件列表操作结果。
/// </summary>
public sealed record HostFileListOperationResult : HostOperationResult
{
    /// <summary>
    /// 文件列表。
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 宿主命令执行结果。
/// </summary>
public sealed record HostCommandExecutionResult : HostOperationResult
{
    /// <summary>
    /// 命令输出。
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// 退出码。成功时通常为 0。
    /// </summary>
    public int ExitCode { get; init; }
}

/// <summary>
/// 结构化宿主结果到文本协议的统一格式化器。
/// </summary>
public static class HostOperationTextFormatter
{
    /// <summary>
    /// 将通用操作结果格式化为工具文本。
    /// </summary>
    public static string Format(HostOperationResult result)
    {
        return result.Status switch
        {
            HostOperationStatus.Success => TaggedExecutionText.Success(result.Message),
            HostOperationStatus.Info => TaggedExecutionText.Info(result.Message),
            HostOperationStatus.SecurityBlocked => TaggedExecutionText.SecurityBlocked(result.Message),
            HostOperationStatus.Exception => TaggedExecutionText.Exception(result.Message),
            _ => TaggedExecutionText.Failure(result.Message)
        };
    }

    /// <summary>
    /// 将文本操作结果格式化为工具文本。
    /// </summary>
    public static string FormatText(HostTextOperationResult result, bool preferTextOnSuccess = true)
    {
        if (result.Succeeded)
        {
            if (preferTextOnSuccess && !string.IsNullOrWhiteSpace(result.Text))
            {
                return result.Text;
            }

            return string.IsNullOrWhiteSpace(result.Message)
                ? result.Text ?? string.Empty
                : result.Message;
        }

        if (result.Status == HostOperationStatus.Info && !string.IsNullOrWhiteSpace(result.Text))
        {
            return result.Text;
        }

        return Format(result);
    }

    /// <summary>
    /// 将文件列表操作结果格式化为工具文本或原始列表。
    /// </summary>
    public static object FormatFileList(HostFileListOperationResult result)
    {
        return result.Succeeded ? result.Files : Format(result);
    }

    /// <summary>
    /// 将命令执行结果格式化为工具文本。
    /// </summary>
    public static string FormatCommand(HostCommandExecutionResult result)
    {
        if (result.Succeeded)
        {
            return result.Output;
        }

        var message = string.IsNullOrWhiteSpace(result.Output)
            ? result.Message
            : $"{result.Message}\n{result.Output}";

        return result.Status switch
        {
            HostOperationStatus.SecurityBlocked => TaggedExecutionText.SecurityBlocked(message),
            HostOperationStatus.Exception => TaggedExecutionText.Exception(message),
            HostOperationStatus.Info => TaggedExecutionText.Info(message),
            _ => TaggedExecutionText.Failure(message)
        };
    }
}
