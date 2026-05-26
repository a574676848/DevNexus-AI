using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// InputBox 基础发送链路。
/// 负责普通聊天输入的最小提交流程。
/// </summary>
public partial class InputBox
{
    private string BuildDevModePlaceholder()
    {
        return "输入消息，或键入 / 调用技能";
    }

    private async Task HandleSendAsync()
    {
        if (_isSending || (string.IsNullOrWhiteSpace(_content) && !_pastedDocuments.Any()))
        {
            return;
        }

        _isSending = true;

        try
        {
            var attachmentArtifacts = await CreateComposerAttachmentArtifactsAsync();
            var attachmentUrls = ResolveAttachmentUrls(attachmentArtifacts);
            var submission = new ChatComposerSubmission
            {
                Content = _content.Trim(),
                ProviderId = _selectedProviderId,
                ArtifactIds = ExtractArtifactIds(attachmentArtifacts),
                Artifacts = attachmentArtifacts.Count > 0 ? attachmentArtifacts : null,
                EnableRag = _enableRag,
                SelectedSkillName = _selectedSlashSkill?.Name,
                Metadata = BuildComposerMetadata(attachmentUrls)
            };

            _content = string.Empty;
            _pastedDocuments.Clear();
            AdjustHeightForDocuments();
            RequestTextareaSync();

            if (OnSendWithProvider.HasDelegate)
            {
                await OnSendWithProvider.InvokeAsync(submission);
            }
            else if (OnSend.HasDelegate)
            {
                await OnSend.InvokeAsync(submission.Content);
            }
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.HandleSendAsync");
            await NotificationService.ShowAsync("发送失败", "消息或附件提交失败，请稍后重试。");
        }
        finally
        {
            _pendingSendRequest = null;
            _isSending = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<List<ArtifactDto>> CreateComposerAttachmentArtifactsAsync()
    {
        var artifacts = new List<ArtifactDto>();
        foreach (var document in _pastedDocuments)
        {
            var created = await ApiService.CreateArtifactAsync(new CreateArtifactRequestDto
            {
                Type = ResolveComposerAttachmentArtifactType(document),
                Name = document.FileName,
                Content = JsonSerializer.Serialize(document.SmartDocument),
                FileAssetId = document.FileAssetId,
                FileVersionId = document.CurrentVersionId,
                SessionId = SessionId,
                Metadata = BuildAttachmentMetadata(document)
            });

            artifacts.Add(created);
        }

        return artifacts;
    }

    private static string ResolveComposerAttachmentArtifactType(PastedDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.ContentType) &&
            !string.Equals(document.ContentType, ArtifactBlockMetadataConstants.TypeUnknown, StringComparison.OrdinalIgnoreCase))
        {
            return document.ContentType;
        }

        if (document.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ArtifactBlockMetadataConstants.TypeImage;
        }

        if (document.MimeType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return ArtifactBlockMetadataConstants.TypeJson;
        }

        if (document.MimeType.Contains("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return ArtifactBlockMetadataConstants.TypeMarkdown;
        }

        if (document.MimeType.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            return ArtifactBlockMetadataConstants.TypeText;
        }

        return ArtifactBlockMetadataConstants.TypeDocument;
    }

    private static List<Guid>? ExtractArtifactIds(List<ArtifactDto> artifacts)
    {
        var ids = artifacts
            .Select(artifact => artifact.ArtifactId)
            .Where(artifactId => artifactId != Guid.Empty)
            .Distinct()
            .ToList();

        return ids.Count == 0 ? null : ids;
    }

    private static List<string> ResolveAttachmentUrls(List<ArtifactDto> artifacts)
    {
        return artifacts
            .Select(artifact => TryReadMetadataString(artifact.Metadata, SmartDocumentConstants.MetadataKeys.SourceUrl))
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, object>? BuildAttachmentMetadata(PastedDocument document)
    {
        var metadata = new Dictionary<string, object>(document.SmartDocument.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [SmartDocumentConstants.MetadataKeys.SourceType] = document.SourceType.ToString()
        };

        return metadata.Count == 0 ? null : metadata;
    }

    private static string? TryReadMetadataString(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var rawValue) || rawValue == null)
        {
            return null;
        }

        return rawValue is JsonElement element && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : rawValue.ToString();
    }

    private Task TryDispatchPendingSendAsync()
    {
        return Task.CompletedTask;
    }
}
