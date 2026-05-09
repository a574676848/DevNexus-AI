using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Services.Storage;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class InputBox
{
    #region Drag & Drop (JS Interop)

    private async Task InitializeDragDropAsync()
    {
        try
        {
            var containerRef = await JS.InvokeAsync<IJSObjectReference>("document.getElementById", "input-box-container");
            await JS.InvokeVoidAsync("DevNexusDragDrop.initDragDrop", containerRef, _dotNetRef);
            await JS.InvokeVoidAsync("DevNexusDragDrop.initPasteHandler", _textareaRef, _dotNetRef);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.InitializeDragDropAsync");
        }
    }

    [JSInvokable]
    public async Task HandleFileDrop(FileDropDto file)
    {
        if (_isUploading) return;
        _isUploading = true;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            if (!ValidateFile(file)) return;

            using var stream = new MemoryStream(file.Data);
            var smartDoc = await FileUploadService.ParseStreamAsync(stream, file.Name, file.Type, _selectedProviderId, SessionId);

            if (smartDoc != null)
            {
                AddDocument(smartDoc, file.Name, DocumentSourceType.Uploaded);
            }
            else
            {
                await NotificationService.ShowAsync("解析失败", $"文件 {file.Name} 发起解析失败");
            }
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.HandleFileDrop", new Dictionary<string, object?>
            {
                ["FileName"] = file.Name,
                ["FileSize"] = file.Size
            });
            await NotificationService.ShowAsync("操作异常", $"处理拖放文件时发生错误: {ex.Message}");
        }
        finally
        {
            _isUploading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable]
    public async Task HandlePasteImage(PastedImageDto image)
    {
        if (_isUploading) return;
        _isUploading = true;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            if (image.Size > MaxFileSize)
            {
                await NotificationService.ShowAsync("图片过大", "图片超过大小限制 (20MB)");
                return;
            }

            var bytes = Convert.FromBase64String(image.Base64Data);
            using var stream = new MemoryStream(bytes);
            var smartDoc = await FileUploadService.ParseStreamAsync(stream, image.Name, image.Type, _selectedProviderId, SessionId);

            if (smartDoc != null)
            {
                AddDocument(smartDoc, image.Name, DocumentSourceType.Pasted);
            }
            else
            {
                await NotificationService.ShowAsync("解析失败", "图片解析发起失败");
            }
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.HandlePasteImage", new Dictionary<string, object?>
            {
                ["ImageName"] = image.Name,
                ["ImageSize"] = image.Size
            });
            await NotificationService.ShowAsync("操作异常", $"处理粘贴图片时发生错误: {ex.Message}");
        }
        finally
        {
            _isUploading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool ValidateFile(FileDropDto file)
    {
        if (file.Size > MaxFileSize)
        {
            NotificationService.ShowAsync("文件过大", $"文件 {file.Name} 超过大小限制 (20MB)").ConfigureAwait(false);
            return false;
        }

        if (!FileUploadService.IsFileSupported(file.Name))
        {
            NotificationService.ShowAsync("不支持的格式", $"不支持的文件类型: {file.Name}").ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private void AddDocument(SmartDocument smartDoc, string fileName, DocumentSourceType sourceType)
    {
        _pastedDocuments.Add(new PastedDocument
        {
            SmartDocument = smartDoc,
            SourceType = sourceType
        });
        AdjustHeightForDocuments();
        EnsureArtifactStatusPolling();
        StateHasChanged();
    }

    private async Task RetryPastedDocumentParseAsync(Guid documentId)
    {
        var targetDoc = _pastedDocuments.FirstOrDefault(doc => doc.Id == documentId);
        if (targetDoc == null)
        {
            return;
        }

        if (!targetDoc.HasFileAsset)
        {
            await NotificationService.ShowAsync("无法重试", $"{targetDoc.FileName} 缺少资产引用，无法直接重试解析。");
            return;
        }

        try
        {
            var previousDocument = targetDoc.SmartDocument;
            var retryDocument = await FileUploadService.RetryParseFromAssetAsync(previousDocument, _selectedProviderId, SessionId);
            if (retryDocument == null)
            {
                await NotificationService.ShowAsync("重试失败", $"{targetDoc.FileName} 重新发起解析失败。");
                return;
            }

            retryDocument.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in previousDocument.Metadata)
            {
                retryDocument.Metadata[pair.Key] = pair.Value;
            }

            retryDocument.TraceId = string.IsNullOrWhiteSpace(retryDocument.TraceId) ? previousDocument.TraceId : retryDocument.TraceId;
            retryDocument.Content ??= previousDocument.Content;
            retryDocument.Chunks = retryDocument.Chunks.Count > 0 ? retryDocument.Chunks : previousDocument.Chunks;
            retryDocument.Status = ParsingStatus.Processing;
            retryDocument.Metadata.Remove(SmartDocumentConstants.MetadataKeys.ParseErrorMessage);
            retryDocument.Metadata[SmartDocumentConstants.MetadataKeys.RetryCount] = ReadIntMetadata(previousDocument.Metadata, SmartDocumentConstants.MetadataKeys.RetryCount) + 1;
            retryDocument.Metadata[SmartDocumentConstants.MetadataKeys.LastRetryAt] = DateTime.UtcNow;

            targetDoc.SmartDocument = retryDocument;
            EnsureArtifactStatusPolling();
            StateHasChanged();
            await NotificationService.ShowAsync("已重新提交解析", $"{targetDoc.FileName} 正在重新解析。");
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.RetryPastedDocumentParseAsync", new Dictionary<string, object?>
            {
                ["DocumentId"] = documentId,
                ["FileName"] = targetDoc.FileName
            });
            await NotificationService.ShowAsync("重试失败", $"{targetDoc.FileName} 重新解析时发生错误：{ex.Message}");
        }
    }

    private Task HandleDocumentContextModeChangedAsync(DocumentContextModeChange change)
    {
        var targetDoc = _pastedDocuments.FirstOrDefault(doc => doc.Id == change.DocumentId);
        if (targetDoc == null)
        {
            return Task.CompletedTask;
        }

        targetDoc.SmartDocument.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        targetDoc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.ContextMode] = change.Mode.ToString();

        switch (change.Mode)
        {
            case DocumentContextMode.ExecuteOnly:
                targetDoc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.AssetOnlyContext] = true;
                targetDoc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.SemanticDisabledReason] = "已手动切换为仅执行上下文";
                break;
            case DocumentContextMode.SemanticOnly:
                targetDoc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.AssetOnlyContext] = false;
                targetDoc.SmartDocument.Metadata.Remove(SmartDocumentConstants.MetadataKeys.SemanticDisabledReason);
                break;
            case DocumentContextMode.Both:
                targetDoc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.AssetOnlyContext] = false;
                targetDoc.SmartDocument.Metadata.Remove(SmartDocumentConstants.MetadataKeys.SemanticDisabledReason);
                break;
        }

        StateHasChanged();
        return Task.CompletedTask;
    }

    #endregion

}

