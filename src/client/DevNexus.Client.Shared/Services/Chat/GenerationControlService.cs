using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// 生成控制服务实现 - 处理AI生成的生命周期管理
/// </summary>
public class GenerationControlService : IGenerationControlService
{
    private readonly IApiService _apiService;
    private readonly ISignalRService _signalR;
    private readonly IChatState _chatState;
    private readonly ILogger<GenerationControlService> _logger;

    public GenerationControlService(
        IApiService apiService,
        ISignalRService signalR,
        IChatState chatState,
        ILogger<GenerationControlService> logger)
    {
        _apiService = apiService;
        _signalR = signalR;
        _chatState = chatState;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> HandleSendWithProviderAsync(
        string content, Guid sessionId, Guid? providerId,
        List<Guid>? artifactIds, List<ArtifactDto>? artifacts, bool enableRag,
        string? selectedSkillName = null, Dictionary<string, object>? metadata = null)
    {
        // 允许空内容但有附件的情况（如仅发送文件）
        if (string.IsNullOrWhiteSpace(content) && (artifacts == null || !artifacts.Any())) return Task.FromResult(false);

        var hasActiveGeneration = _chatState.GetSessionRunControl(sessionId).IsGenerationLike;
        if (!hasActiveGeneration)
        {
            // 清空 ChatState 中该会话的旧 Blocks，防止状态恢复逻辑错误加载上一轮内容
            _chatState.Clear(sessionId);

            // 设置统一运行态
            _chatState.SetSessionGeneratingOptimistic(sessionId, true);
        }

        try
        {
            // 发送消息（包含 Provider ID、Artifact IDs 和 EnableRag）
            var request = new ChatRequest
            {
                SessionId = sessionId,
                Content = content,
                LLMProviderId = providerId,
                ArtifactIds = artifactIds,
                EnableRag = enableRag,
                EnableSwarm = string.IsNullOrWhiteSpace(selectedSkillName) && metadata?.ContainsKey("toolId") != true,
                SelectedSkillName = selectedSkillName,
                Metadata = BuildRequestMetadata(selectedSkillName, metadata)
            };

            _ = Task.Run(async () => 
            {
                try 
                {
                    await _signalR.SendMessageAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[GenerationControl] 发送消息失败");
                }
                finally
                {
                    // ★ 兜底：SendMessageAsync 返回意味着服务端方法已完成
                    // 正常流程下结构化运行时事件已触发并重置了状态
                    // 此处仅在事件未送达（如 Redis 全面故障）时兜底重置，防止 UI 永久卡死
                    if (!hasActiveGeneration && _chatState.GetSessionRunControl(sessionId).IsGenerationLike)
                    {
                        _logger.LogWarning(
                            "[GenerationControl] 生成状态兜底重置（事件可能未送达） | SessionId={SessionId}",
                            sessionId);
                        _chatState.SetSessionGeneratingOptimistic(sessionId, false);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GenerationControl] 发送消息失败");
            _chatState.SetSessionGeneratingOptimistic(sessionId, false);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private static Dictionary<string, object>? BuildRequestMetadata(
        string? selectedSkillName,
        Dictionary<string, object>? metadata)
    {
        Dictionary<string, object>? merged = null;
        if (metadata?.Count > 0)
        {
            merged = new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(selectedSkillName))
        {
            merged ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            merged[ChatMessageMetadataKeys.SelectedSkillName] = selectedSkillName.Trim();
        }

        return merged;
    }

    /// <inheritdoc />
    public async Task<bool> HandleSendAsync(string content, Guid sessionId)
    {
        return await HandleSendWithProviderAsync(content, sessionId, null, null, null, true);
    }

    /// <inheritdoc />
    public async Task<ChatMessageDto?> HandleCancelAsync(Guid sessionId, List<BlockDto> currentBlocks, Guid currentMessageId)
    {
        ChatMessageDto? solidifiedMessage = null;

        try
        {
            // ⚠️ 会话统一运行态已由组件在调用前回落并触发 StateHasChanged()，
            // 确保取消按钮立即响应，此处不再重复设置。

            // 1. 如果有活动的 Swarm 会话，立即中止并清除状态
            if (_chatState.IsSwarmActive(sessionId))
            {
                _chatState.SetSwarmActive(sessionId, false);
                try
                {
                    await _apiService.AbortSwarmSessionAsync(sessionId);
                    _logger.LogInformation("[HandleCancel] Swarm session {SessionId} aborted successfully", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HandleCancel] 中止 Swarm 编排请求发送失败");
                }
            }

            // 3. 将已生成的内容（包含思考和正文）固化为消息，防止消失
            if (currentBlocks.Any())
            {
                var textContent = string.Join("", currentBlocks
                    .Where(b => b.BlockType == BlockType.TextDelta).Select(b => b.Content));
                var thoughtContent = MetadataHelper.JoinThoughtSegments(currentBlocks
                    .Where(b => b.BlockType == BlockType.Thinking).Select(b => b.Content));

                // 构建正文内容；thinking 仅通过结构化字段传递。
                var fullContent = textContent;

                if (!string.IsNullOrEmpty(fullContent) || !string.IsNullOrEmpty(thoughtContent))
                {
                    solidifiedMessage = new ChatMessageDto
                    {
                        Id = currentMessageId,
                        ChatSessionId = sessionId,
                        SenderType = ChatConstants.RoleAssistant,
                        Content = fullContent + "\n\n(已停止生成)",
                        TextContent = fullContent + "\n\n(已停止生成)",
                        ThinkingContent = string.IsNullOrEmpty(thoughtContent) ? null : thoughtContent,
                        CreatedAt = DateTime.UtcNow
                    };
                }

                // 清理 ChatState 中该会话的缓冲区
                _chatState.Clear(sessionId);
            }

            // 4. 后台发送取消请求
            try
            {
                await _signalR.CancelGenerationAsync(sessionId);
                _logger.LogInformation("[HandleCancel] Cancel generation request sent for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HandleCancel] 取消生成请求发送失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HandleCancel] 处理取消生成失败");
        }

        return solidifiedMessage;
    }
}
