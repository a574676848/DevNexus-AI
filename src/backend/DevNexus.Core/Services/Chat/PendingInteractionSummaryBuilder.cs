using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 挂起交互摘要构建器。
/// </summary>
public static class PendingInteractionSummaryBuilder
{
    private const string WarningTone = "warning";
    private const string DangerTone = "danger";
    private const string InfoTone = "info";

    /// <summary>
    /// 基于交互类型和说明构建摘要。
    /// </summary>
    public static PendingInteractionSummaryDto Build(
        PendingInteractionKind kind,
        string? title,
        string? description)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? ResolveLabel(kind) : title.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? ResolveDescription(kind)
            : description.Trim();

        return new PendingInteractionSummaryDto
        {
            Tone = ResolveTone(kind),
            Label = normalizedTitle,
            Description = normalizedDescription,
            InputPlaceholder = ResolveInputPlaceholder(kind),
            NextAction = ResolveNextAction(kind),
            BlocksMessageSend = true
        };
    }

    private static string ResolveTone(PendingInteractionKind kind)
    {
        return kind switch
        {
            PendingInteractionKind.Approval => WarningTone,
            PendingInteractionKind.Credential => DangerTone,
            PendingInteractionKind.OAuthCallback => InfoTone,
            _ => WarningTone
        };
    }

    private static string ResolveLabel(PendingInteractionKind kind)
    {
        return kind switch
        {
            PendingInteractionKind.Approval => "等待执行审批",
            PendingInteractionKind.Credential => "等待补充凭证",
            PendingInteractionKind.Confirmation => "等待确认",
            PendingInteractionKind.OAuthCallback => "等待外部授权完成",
            PendingInteractionKind.Clarification => "等待补充信息",
            _ => "等待用户处理"
        };
    }

    private static string ResolveDescription(PendingInteractionKind kind)
    {
        return kind switch
        {
            PendingInteractionKind.Approval => "当前会话正在等待审批，审批完成后才能继续发送。",
            PendingInteractionKind.Credential => "当前会话正在等待凭证补充，补充后才能继续执行。",
            PendingInteractionKind.Confirmation => "当前会话正在等待确认，确认后才能继续执行。",
            PendingInteractionKind.OAuthCallback => "当前会话正在等待外部授权回调完成。",
            PendingInteractionKind.Clarification => "当前会话正在等待补充信息，补充完成后才能继续发送。",
            _ => "当前会话仍有待处理交互，自动执行已暂停。"
        };
    }

    private static string ResolveInputPlaceholder(PendingInteractionKind kind)
    {
        return kind switch
        {
            PendingInteractionKind.Approval => "当前等待审批，审批通过后可继续",
            PendingInteractionKind.Credential => "请先补充凭证信息",
            PendingInteractionKind.Confirmation => "请先完成确认",
            PendingInteractionKind.OAuthCallback => "请先完成外部授权",
            _ => "请先完成上方待补充信息"
        };
    }

    private static string ResolveNextAction(PendingInteractionKind kind)
    {
        return kind switch
        {
            PendingInteractionKind.Approval => "ApproveOrDeny",
            PendingInteractionKind.Credential => "ProvideCredential",
            PendingInteractionKind.Confirmation => "ConfirmOrCancel",
            PendingInteractionKind.OAuthCallback => "CompleteExternalAuthorization",
            _ => "ProvideInput"
        };
    }
}
