using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class InputBox
{
    private static readonly TimeSpan ArtifactPollingMaxDuration = TimeSpan.FromSeconds(90);
    private DateTime? _artifactPollingStartedAt;

    private string? ApplyArtifactStatusToDocument(PastedDocument targetDoc, ArtifactStatusDto status)
    {
        targetDoc.SmartDocument.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var previousStatus = targetDoc.SmartDocument.Status;
        var nextStatus = MapArtifactStatus(status.Status, status.Success);
        if (!string.IsNullOrWhiteSpace(status.Status))
        {
            targetDoc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.SemanticPipelineStage] = status.Status;
        }

        targetDoc.SmartDocument.TraceId = string.IsNullOrWhiteSpace(targetDoc.SmartDocument.TraceId)
            ? status.TraceId
            : targetDoc.SmartDocument.TraceId;
        targetDoc.SmartDocument.Status = nextStatus;

        if (status.SmartDocument != null)
        {
            var mergedMetadata = new Dictionary<string, object>(targetDoc.SmartDocument.Metadata, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in status.SmartDocument.Metadata)
            {
                mergedMetadata[pair.Key] = pair.Value;
            }

            targetDoc.SmartDocument.FileName = string.IsNullOrWhiteSpace(status.SmartDocument.FileName)
                ? targetDoc.SmartDocument.FileName
                : status.SmartDocument.FileName;
            targetDoc.SmartDocument.MimeType = string.IsNullOrWhiteSpace(status.SmartDocument.MimeType)
                ? targetDoc.SmartDocument.MimeType
                : status.SmartDocument.MimeType;
            targetDoc.SmartDocument.SizeBytes = status.SmartDocument.SizeBytes > 0
                ? status.SmartDocument.SizeBytes
                : targetDoc.SmartDocument.SizeBytes;
            targetDoc.SmartDocument.Chunks = status.SmartDocument.Chunks;
            targetDoc.SmartDocument.ParsedAt = status.SmartDocument.ParsedAt;
            targetDoc.SmartDocument.Content = status.SmartDocument.Content ?? targetDoc.SmartDocument.Content;
            targetDoc.SmartDocument.Metadata = mergedMetadata;
        }

        if (nextStatus == ParsingStatus.Failed)
        {
            targetDoc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.ParseErrorMessage] = status.ErrorMessage ?? "未知错误";
            return previousStatus == ParsingStatus.Failed ? null : status.ErrorMessage ?? "未知错误";
        }

        targetDoc.SmartDocument.Metadata.Remove(SmartDocumentConstants.MetadataKeys.ParseErrorMessage);
        return null;
    }

    private void EnsureArtifactStatusPolling()
    {
        if (!HasPendingUploads)
        {
            StopArtifactStatusPolling();
            return;
        }

        if (_artifactStatusPollingTask is { IsCompleted: false })
        {
            return;
        }

        StopArtifactStatusPolling();
        _artifactStatusPollingCts = new CancellationTokenSource();
        _artifactPollingStartedAt = DateTime.UtcNow;
        _artifactStatusPollingTask = PollArtifactStatusLoopAsync(_artifactStatusPollingCts.Token);
    }

    private void StopArtifactStatusPolling()
    {
        if (_artifactStatusPollingCts == null)
        {
            return;
        }

        try
        {
            _artifactStatusPollingCts.Cancel();
        }
        catch
        {
            // 忽略取消阶段异常
        }
        finally
        {
            _artifactStatusPollingCts.Dispose();
            _artifactStatusPollingCts = null;
            _artifactStatusPollingTask = null;
            _artifactPollingStartedAt = null;
        }
    }

    private async Task PollArtifactStatusLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await PollArtifactStatusesOnceAsync(cancellationToken);

                if (!HasPendingUploads)
                {
                    break;
                }

                await Task.Delay(ArtifactStatusPollIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.PollArtifactStatusLoopAsync");
        }
        finally
        {
            if (!HasPendingUploads)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task PollArtifactStatusesOnceAsync(CancellationToken cancellationToken)
    {
        if (_artifactPollingStartedAt.HasValue
            && DateTime.UtcNow - _artifactPollingStartedAt.Value > ArtifactPollingMaxDuration)
        {
            foreach (var doc in _pastedDocuments
                         .Where(doc => doc.SmartDocument.Status is ParsingStatus.Processing or ParsingStatus.Pending))
            {
                doc.SmartDocument.Status = ParsingStatus.Failed;
                doc.SmartDocument.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                doc.SmartDocument.Metadata[SmartDocumentConstants.MetadataKeys.ParseErrorMessage] = "解析超时，已停止自动轮询，请手动重试。";
            }

            StopArtifactStatusPolling();
            await NotificationService.ShowDeduplicatedAsync(
                "解析超时",
                "部分附件长时间未完成解析，已停止自动轮询，请手动重试。",
                suppressSeconds: 10,
                dedupeKey: "artifact-polling-timeout");
            return;
        }

        var processingDocs = _pastedDocuments
            .Where(doc => doc.SmartDocument.Status is ParsingStatus.Processing or ParsingStatus.Pending)
            .Where(doc => !string.IsNullOrWhiteSpace(doc.SmartDocument.TraceId))
            .Take(ArtifactStatusPollBurstSize)
            .ToList();

        foreach (var doc in processingDocs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await ApiService.GetParseStatusAsync(doc.SmartDocument.TraceId!);
            if (status == null)
            {
                continue;
            }

            var failedMessage = ApplyArtifactStatusToDocument(doc, status);
            if (!string.IsNullOrWhiteSpace(failedMessage))
            {
                await NotificationService.ShowDeduplicatedAsync(
                    "解析失败",
                    $"文件 {doc.FileName} 解析失败：{failedMessage}",
                    suppressSeconds: 10,
                    dedupeKey: $"artifact-parse-failed:{doc.FileName}:{doc.SmartDocument.TraceId}");
            }
        }

        await InvokeAsync(async () =>
        {
            await TryDispatchPendingSendAsync();
            StateHasChanged();
        });
    }

    private static ParsingStatus MapArtifactStatus(string? status, bool success)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return success ? ParsingStatus.Completed : ParsingStatus.Processing;
        }

        var normalizedStatus = ArtifactStatusConstants.Normalize(status, string.Empty);

        if (normalizedStatus is ArtifactStatusConstants.Indexing or ArtifactStatusConstants.Parsed)
        {
            return ParsingStatus.Processing;
        }

        if (ArtifactStatusConstants.IsCompleted(normalizedStatus))
        {
            return ParsingStatus.Completed;
        }

        if (normalizedStatus == ArtifactStatusConstants.Processing)
        {
            return ParsingStatus.Processing;
        }

        if (normalizedStatus == ArtifactStatusConstants.Pending)
        {
            return ParsingStatus.Pending;
        }

        if (ArtifactStatusConstants.IsFailed(normalizedStatus))
        {
            return ParsingStatus.Failed;
        }

        return success ? ParsingStatus.Completed : ParsingStatus.Processing;
    }

    private static int ReadIntMetadata(Dictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var number) => number,
            JsonElement json when json.ValueKind == JsonValueKind.String && int.TryParse(json.GetString(), out var parsed) => parsed,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }
}
