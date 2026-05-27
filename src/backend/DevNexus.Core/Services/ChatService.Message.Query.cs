// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务消息查询与恢复能力。
/// </summary>
public partial class ChatService
{
    private const string InternalRepairPromptMetadataKey = "internalRepairPrompt";

    /// <inheritdoc />
    public async Task<List<ChatMessageDto>> GetChatMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting chat messages for session {SessionId}",
            sessionId);

        var messages = await _chatMessageRepository.ListBySessionAsync(sessionId, cancellationToken);
        messages = messages
            .Where(message => !IsInternalRepairPrompt(message) && !IsControlPlaneMessage(message))
            .ToList();

        if (messages.Count == 0)
        {
            return new List<ChatMessageDto>();
        }

        var artifacts = await _artifactService.GetSessionArtifactsAsync(sessionId, cancellationToken);
        var artifactsLookup = artifacts.GroupBy(a => a.MessageId).ToDictionary(g => g.Key, g => g.ToList());
        var terminalStreamsLookup = await LoadTerminalStreamsByMessageAsync(messages, cancellationToken);

        return messages.Select(message =>
        {
            var msgArtifacts = artifactsLookup.TryGetValue(message.Id, out var arts) ? arts : null;
            List<BlockDto>? chartBlocks = null;
            List<BlockDto>? interactiveBlocks = null;
            List<BlockDto>? orderedBlocks = null;

            if (msgArtifacts != null && msgArtifacts.Any())
            {
                var charts = msgArtifacts
                    .Where(a => a.Type?.Equals("chart", StringComparison.OrdinalIgnoreCase) == true)
                    .Select(a => new BlockDto
                    {
                        BlockType = BlockType.Chart,
                        Content = a.Content,
                        MessageId = a.MessageId != Guid.Empty ? a.MessageId : message.Id,
                        SessionId = sessionId,
                        Metadata = new Dictionary<string, object>
                        {
                            { ArtifactBlockMetadataConstants.Title, a.Name ?? ArtifactBlockMetadataConstants.DefaultChartTitle },
                            { ArtifactBlockMetadataConstants.ChartType, ArtifactBlockMetadataConstants.ChartTypeAuto }
                        }
                    })
                    .ToList();

                if (charts.Any())
                {
                    chartBlocks = charts;
                }

                var interactive = msgArtifacts
                    .Where(a => a.Type?.StartsWith("interactive-", StringComparison.OrdinalIgnoreCase) == true)
                    .Select(a =>
                    {
                        var cardType = a.Type?.Replace("interactive-", "", StringComparison.OrdinalIgnoreCase) ?? "unknown";
                        return new BlockDto
                        {
                            BlockType = BlockType.InteractiveCard,
                            Content = a.Content,
                            MessageId = a.MessageId != Guid.Empty ? a.MessageId : message.Id,
                            SessionId = sessionId,
                            Metadata = new Dictionary<string, object>
                            {
                                { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.NormalizeCardType(cardType) },
                                { ToolBlockMetadataConstants.Query, a.Name ?? string.Empty }
                            }
                        };
                    })
                    .ToList();

                if (interactive.Any())
                {
                    interactiveBlocks = interactive;
                }

                var ordered = new List<BlockDto>();
                ordered.AddRange(charts);
                ordered.AddRange(interactive);
                if (ordered.Any())
                {
                    orderedBlocks = ordered;
                }
            }

            var isInProgress = ChatConstants.IsInProgressStatus(message.Status);
            var textContent = GetContentString(message.Content, "text");
            if (isInProgress && string.IsNullOrEmpty(textContent))
            {
                textContent = GetContentString(message.Content, "text_partial");
            }

            var thinkingContent = GetContentString(message.Content, "thinking");
            if (isInProgress && string.IsNullOrEmpty(thinkingContent))
            {
                thinkingContent = GetContentString(message.Content, "thinking_partial");
            }

            var terminalStreams = terminalStreamsLookup.TryGetValue(message.Id, out var streams)
                ? streams
                : new List<TerminalStreamSnapshot>();

            var fullContentString = string.IsNullOrEmpty(thinkingContent)
                ? textContent
                : $"<think>{thinkingContent}</think>\n{textContent}";

            if (terminalStreams.Count > 0)
            {
                orderedBlocks ??= new List<BlockDto>();
                foreach (var stream in terminalStreams)
                {
                    if (stream.IsActive)
                    {
                        continue;
                    }

                    var terminalBlock = new BlockDto
                    {
                        BlockType = BlockType.Terminal,
                        Content = stream.Output ?? string.Empty,
                        MessageId = message.Id,
                        SessionId = sessionId,
                        IsLast = true,
                        Metadata = new Dictionary<string, object>
                        {
                            { TerminalBlockMetadataKeys.Status, stream.Status },
                            { TerminalBlockMetadataKeys.SessionKey, stream.SessionKey ?? string.Empty },
                            { TerminalBlockMetadataKeys.SessionState, stream.SessionState },
                            { TerminalBlockMetadataKeys.TerminalStreamId, stream.TerminalStreamId },
                            { TerminalBlockMetadataKeys.AttemptNumber, stream.AttemptNumber },
                            { TerminalBlockMetadataKeys.IsRetry, stream.IsRetry },
                            { TerminalBlockMetadataKeys.WaitingForInput, stream.WaitingForInput },
                            { TerminalBlockMetadataKeys.TerminationReason, stream.TerminationReason ?? CliSessionTerminationReasons.Completed },
                            { TerminalBlockMetadataKeys.RuntimeHost, stream.RuntimeHost ?? string.Empty },
                            { TerminalBlockMetadataKeys.IsActive, stream.IsActive },
                            { TerminalBlockMetadataKeys.HistoryOutputIncluded, true }
                        }
                    };

                    if (!string.IsNullOrWhiteSpace(stream.Command))
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.Command] = stream.Command;
                    }

                    if (!string.IsNullOrWhiteSpace(stream.WorkingDirectory))
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.WorkingDirectory] = stream.WorkingDirectory;
                    }

                    if (stream.ToolCallId.HasValue)
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.ToolCallId] = stream.ToolCallId.Value;
                    }

                    if (stream.ExitCode.HasValue)
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.ExitCode] = stream.ExitCode.Value;
                    }

                    if (stream.WaitingForInputSince.HasValue)
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.WaitingForInputSince] = stream.WaitingForInputSince.Value.ToString("O");
                    }

                    if (stream.StartedAt.HasValue)
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.StartedAt] = stream.StartedAt.Value.ToString("O");
                    }

                    if (stream.LastActivityAt.HasValue)
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.LastActivityAt] = stream.LastActivityAt.Value.ToString("O");
                    }

                    terminalBlock.Metadata[TerminalBlockMetadataKeys.HasArchivedOutput] = stream.HasArchivedOutput;
                    terminalBlock.Metadata[TerminalBlockMetadataKeys.OutputLength] = stream.OutputLength;
                    terminalBlock.Metadata[TerminalBlockMetadataKeys.OutputLineCount] = stream.OutputLineCount;

                    if (!string.IsNullOrWhiteSpace(stream.WatchSummary))
                    {
                        terminalBlock.Metadata[TerminalBlockMetadataKeys.WatchSummary] = stream.WatchSummary;
                    }

                    orderedBlocks.Add(terminalBlock);
                }
            }

            return new ChatMessageDto
            {
                Id = message.Id,
                ChatSessionId = message.ChatSessionId,
                ParentMessageId = message.ParentMessageId,
                SenderId = message.SenderId,
                SenderType = message.SenderType,
                Content = fullContentString,
                MessageType = message.MessageType,
                Status = message.Status,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
                Metadata = message.Metadata,
                Artifacts = msgArtifacts,
                ChartBlocks = chartBlocks,
                InteractiveBlocks = interactiveBlocks,
                OrderedBlocks = orderedBlocks
            };
        }).ToList();
    }

    private static bool IsInternalRepairPrompt(ChatMessage message)
    {
        if (message.Metadata == null
            || !message.Metadata.TryGetValue(InternalRepairPromptMetadataKey, out var value)
            || value == null)
        {
            return false;
        }

        return bool.TryParse(value.ToString(), out var isHidden) && isHidden;
    }

    private static bool IsControlPlaneMessage(ChatMessage message)
    {
        if (!ChatConstants.IsUserSender(message.SenderType)
            || message.Metadata == null
            || !message.Metadata.TryGetValue(ChatMessageMetadataKeys.ResumePendingInteraction, out var value)
            || value == null)
        {
            return false;
        }

        return value is bool boolValue
            ? boolValue
            : bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    /// <inheritdoc />
    public async Task<List<TerminalRecordDto>> GetActiveTerminalRecordsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var streams = await _terminalStreamRepository.GetActiveBySessionIdAsync(sessionId, cancellationToken);
        if (streams.Count == 0)
        {
            return new List<TerminalRecordDto>();
        }

        return streams.Select(stream => new TerminalRecordDto
        {
            RecordId = stream.Id,
            SessionId = sessionId,
            MessageId = stream.MessageId ?? Guid.Empty,
            TerminalStreamId = stream.Id,
            ToolCallId = stream.ToolCallId,
            PackageId = stream.PackageId,
            Command = stream.Command,
            WorkingDirectory = stream.WorkingDirectory,
            Status = stream.Status.ToWireValue(),
            SessionState = stream.SessionState.ToWireValue(),
            RuntimeHost = stream.RuntimeHost,
            ExitCode = stream.ExitCode,
            AttemptNumber = stream.AttemptNumber,
            IsRetry = stream.IsRetry,
            WaitingForInput = stream.WaitingForInput,
            WaitingForInputSince = stream.WaitingForInputSince,
            TerminationReason = stream.TerminationReason,
            StartedAt = stream.StartedAt,
            LastActivityAt = stream.LastActivityAt,
            Output = stream.Output ?? string.Empty,
            HasArchivedOutput = stream.HasArchivedOutput,
            OutputLength = stream.OutputLength,
            OutputLineCount = stream.OutputLineCount,
            WatchSummary = stream.WatchSummary,
            IsActive = stream.SessionState.IsActive()
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<TerminalOutputContentDto?> GetTerminalOutputAsync(
        Guid sessionId,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        var stream = await _terminalStreamRepository.GetByIdAsync(recordId, cancellationToken);
        if (stream == null || stream.ChatSessionId != sessionId)
        {
            return null;
        }

        return await _terminalOutputBuffer.ReadOutputAsync(recordId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PendingInteractionDto>> GetActivePendingInteractionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var interactions = await _pendingInteractionRepository.GetActiveBySessionIdAsync(sessionId, cancellationToken);
        if (interactions.Count == 0)
        {
            return new List<PendingInteractionDto>();
        }

        return interactions
            .Select(PendingInteractionDtoMapper.ToDto)
            .ToList();
    }

    private static string GetContentString(IDictionary<string, object> content, string key)
    {
        if (content.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

}
