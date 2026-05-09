using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.JSInterop;

using DevNexus.Client.Shared.Helpers;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class InputBox
{
    #region Helpers

    private void AdjustHeightForDocuments()
    {
        if (_resizablePanel == null) return;

        var currentHeight = _resizablePanel.CurrentSize;

        if (_pastedDocuments.Any() && currentHeight < 200)
        {
            _resizablePanel.SetSize(200);
        }
        else if (!_pastedDocuments.Any() && currentHeight > 160 && currentHeight <= 200)
        {
            _resizablePanel.SetSize(160);
        }
    }

    private async Task FocusInputAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("focusElement", _textareaRef);
        }
        catch { /* Ignore */ }
    }

    private void RequestTextareaSync(bool moveCaretToEnd = false)
    {
        _shouldSyncTextareaValue = true;
        _shouldMoveCaretToEndAfterSync = _shouldMoveCaretToEndAfterSync || moveCaretToEnd;
    }

    private async Task SyncTextareaValueAsync(bool moveCaretToEnd = false)
    {
        try
        {
            await JS.InvokeVoidAsync("devnexus.setTextareaValue", _textareaRef, _content, moveCaretToEnd);
        }
        catch { /* Ignore */ }
    }

    private async Task HandleQueuedFileAssetAsync(Guid sessionId, FileAssetDto asset)
    {
        if (!SessionId.HasValue || SessionId.Value != sessionId)
        {
            return;
        }

        if (_pastedDocuments.Any(doc => doc.FileAssetId == asset.FileAssetId))
        {
            await NotificationService.ShowAsync("文件已存在", $"{asset.OriginalFileName} 已在当前输入区中。");
            return;
        }

        _pastedDocuments.Add(CreateAssetOnlyDocument(asset));
        AdjustHeightForDocuments();
        await NotificationService.ShowAsync("已加入下一轮", $"{asset.OriginalFileName} 已加入当前输入区，可继续处理。");
        await InvokeAsync(StateHasChanged);
    }

    private static PastedDocument CreateAssetOnlyDocument(FileAssetDto asset)
    {
        var smartDocument = new SmartDocument
        {
            FileId = asset.FileAssetId.ToString(),
            FileName = asset.OriginalFileName,
            MimeType = asset.ContentType,
            SizeBytes = asset.SizeBytes,
            CreatedAt = asset.CreatedAt,
            ParsedAt = asset.UpdatedAt,
            Status = ParsingStatus.Completed,
            Content = new TextDocumentContent
            {
                Text = string.Empty,
                Format = "asset-reference",
                PageCount = 0
            },
            Metadata = new Dictionary<string, object>
            {
                [SmartDocumentConstants.MetadataKeys.FileAssetId] = asset.FileAssetId,
                [SmartDocumentConstants.MetadataKeys.CurrentVersionId] = asset.CurrentVersionId,
                [SmartDocumentConstants.MetadataKeys.SourceUrl] = asset.FileUrl,
                [SmartDocumentConstants.MetadataKeys.StorageProvider] = asset.StorageProvider,
                [SmartDocumentConstants.MetadataKeys.ObjectKey] = asset.ObjectKey,
                [SmartDocumentConstants.MetadataKeys.FileAssetStatus] = asset.Status.ToString(),
                [SmartDocumentConstants.MetadataKeys.ContextMode] = DocumentContextMode.ExecuteOnly.ToString(),
                [SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = SmartDocumentConstants.SemanticPipelineStages.NotRequested,
                [SmartDocumentConstants.MetadataKeys.AssetOnlyContext] = true,
                [SmartDocumentConstants.MetadataKeys.OriginScope] = SmartDocumentConstants.OriginScopes.TaskOutput,
                [SmartDocumentConstants.MetadataKeys.SourceType] = asset.SourceType
            }
        };

        return new PastedDocument
        {
            SmartDocument = smartDocument,
            SourceType = DocumentSourceType.Uploaded
        };
    }

    private string BuildTerminalInputPlaceholder()
    {
        var command = CurrentTerminalCommand;
        var prefix = "终端等待输入，按 Enter 发送";

        if (string.IsNullOrWhiteSpace(command))
        {
            return prefix;
        }

        return $"{prefix} · {command}";
    }

    private string BuildActiveTerminalPlaceholder()
    {
        return "终端运行中，可继续观察输出";
    }

    private string BuildPendingApprovalPlaceholder()
    {
        if (!string.IsNullOrWhiteSpace(CurrentRuntime?.PrimaryPendingInteractionTitle))
        {
            return CurrentRuntime.PrimaryPendingInteractionTitle!;
        }

        if (!string.IsNullOrWhiteSpace(CurrentRuntime?.PrimaryPendingInteractionDescription))
        {
            return CurrentRuntime.PrimaryPendingInteractionDescription!;
        }

        return "当前等待审批，审批通过后可继续。";
    }

    private string BuildPendingInputPlaceholder()
    {
        if (!string.IsNullOrWhiteSpace(CurrentRuntime?.PrimaryPendingInteractionTitle))
        {
            return CurrentRuntime.PrimaryPendingInteractionTitle!;
        }

        if (!string.IsNullOrWhiteSpace(CurrentRuntime?.PrimaryPendingInteractionDescription))
        {
            return CurrentRuntime.PrimaryPendingInteractionDescription!;
        }

        return "请先完成上方待补充信息。";
    }

    /// <summary>
    /// 将统一会话主状态映射为输入框占位文案，避免不同入口对同一状态表达不一致。
    /// </summary>
    private string? GetCurrentSessionRunPlaceholder()
    {
        if (!SessionId.HasValue || SessionId.Value == Guid.Empty)
        {
            return null;
        }

        return ChatState.GetSessionRunPresentation(SessionId.Value).InputPlaceholder;
    }

    #endregion

}
