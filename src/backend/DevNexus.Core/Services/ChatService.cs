using DevNexus.Core.Services.Chat;
using DevNexus.Core.Abstractions.Observability;
using DevNexus.Core.Services.Swarm;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Services.Swarm.Planning;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务实现 - 主文件（AI响应编排）
/// </summary>
/// <remarks>
/// 此服务使用 partial class 拆分为多个文件：
/// - ChatService.cs: 核心逻辑、构造函数、AI响应编排
/// - ChatService.Session.cs: 会话生命周期管理
/// - ChatService.Message.cs: 消息处理
/// </remarks>
public partial class ChatService : IChatService
{
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ChatService> _logger;
    private readonly IKernelService _IKernelService;
    private readonly ILLMProviderManagementService _llmProviderService;
    private readonly IArtifactService _artifactService;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IChatSessionDeletionCoordinator _chatSessionDeletionCoordinator;
    private readonly IChatMessageCompletionCoordinator _chatMessageCompletionCoordinator;
    private readonly IExecutionStrategyExecutor _executionStrategyExecutor;
    private readonly IUnitOfWorkTransactionFactory _unitOfWorkTransactionFactory;
    private readonly ChatGenerationCancellationRegistry _generationCancellationRegistry = new();

    private const string ConcurrentGenerationMessage = "当前会话已有生成任务正在运行，请等待完成或取消后再发送。";

    // New Services
    private readonly ChatHistoryService _chatHistoryService;
    private readonly ChatSearchService _chatSearchService;
    private readonly ChatStreamingPreparationService _chatStreamingPreparationService;
    private readonly ChatStreamingFinalizer _chatStreamingFinalizer;
    private readonly ChatAgentLoopCoordinator _chatAgentLoopCoordinator;
    private readonly ChatThinkingPersistenceCoordinator _thinkingPersistenceCoordinator;
    private readonly ChatSwarmFinalizer _chatSwarmFinalizer;
    private readonly ToolBlockExecutionCoordinator _toolBlockExecutionCoordinator;
    private readonly Core.Abstractions.IAgentMemoryService _agentMemoryService;

    // Swarm 集群服务
    private readonly IComplexityEvaluator _complexityEvaluator;
    private readonly ISwarmOrchestrator _swarmOrchestrator;
    private readonly ISwarmEventService _swarmEventService;
    private readonly Swarm.ISwarmSessionControlService _swarmSessionControlService;

    // Reasoning Extraction Service
    private readonly IReasoningExtractionService _reasoningExtractor;

    // Evaluation Services (Phase 1 & 4)
    private readonly Chat.AgentLoopExecutor _agentLoopExecutor;

    // Observability Services (P1-4) - 符合洋葱架构，只依赖接口
    private readonly IDistributedTracingService _tracingService;
    private readonly IAgentLoopMetricsCollector _metricsCollector;

    // Terminal Stream Repository (Phase 8)
    private readonly ITerminalStreamRepository _terminalStreamRepository;
    private readonly ITerminalOutputBuffer _terminalOutputBuffer;
    private readonly IPendingInteractionRepository _pendingInteractionRepository;

    // 记忆沉淀配置
    private const int MemoryConsolidationMessageThreshold = 10;
    private static readonly TimeSpan MemoryConsolidationDelay = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <summary>
    /// 构造函数
    /// </summary>
    public ChatService(
        IChatSessionRepository chatSessionRepository,
        IChatMessageRepository chatMessageRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChatService> logger,
        IKernelService IKernelService,
        ILLMProviderManagementService llmProviderService,
        IArtifactService artifactService,
        IBackgroundJobService backgroundJobService,
        IChatSessionDeletionCoordinator chatSessionDeletionCoordinator,
        IChatMessageCompletionCoordinator chatMessageCompletionCoordinator,
        IExecutionStrategyExecutor executionStrategyExecutor,
        IUnitOfWorkTransactionFactory unitOfWorkTransactionFactory,
        ChatHistoryService chatHistoryService,
        ChatSearchService chatSearchService,
        ChatStreamingPreparationService chatStreamingPreparationService,
        ChatStreamingFinalizer chatStreamingFinalizer,
        ChatAgentLoopCoordinator chatAgentLoopCoordinator,
        ChatThinkingPersistenceCoordinator thinkingPersistenceCoordinator,
        ChatSwarmFinalizer chatSwarmFinalizer,
        ToolBlockExecutionCoordinator toolBlockExecutionCoordinator,
        IComplexityEvaluator complexityEvaluator,
        ISwarmOrchestrator swarmOrchestrator,
        ISwarmEventService swarmEventService,
        Swarm.ISwarmSessionControlService swarmSessionControlService,
        IReasoningExtractionService reasoningExtractor,
        Core.Abstractions.IAgentMemoryService agentMemoryService,
        Chat.AgentLoopExecutor agentLoopExecutor,
        IDistributedTracingService tracingService,
        IAgentLoopMetricsCollector metricsCollector,
        ITerminalStreamRepository terminalStreamRepository,
        ITerminalOutputBuffer terminalOutputBuffer,
        IPendingInteractionRepository pendingInteractionRepository)
    {
        _chatSessionRepository = chatSessionRepository;
        _chatMessageRepository = chatMessageRepository;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _IKernelService = IKernelService;
        _llmProviderService = llmProviderService;
        _artifactService = artifactService;
        _backgroundJobService = backgroundJobService;
        _chatSessionDeletionCoordinator = chatSessionDeletionCoordinator;
        _chatMessageCompletionCoordinator = chatMessageCompletionCoordinator;
        _executionStrategyExecutor = executionStrategyExecutor;
        _unitOfWorkTransactionFactory = unitOfWorkTransactionFactory;
        _reasoningExtractor = reasoningExtractor;

        // Injected Services
        _chatHistoryService = chatHistoryService;
        _chatSearchService = chatSearchService;
        _chatStreamingPreparationService = chatStreamingPreparationService;
        _chatStreamingFinalizer = chatStreamingFinalizer;
        _chatAgentLoopCoordinator = chatAgentLoopCoordinator;
        _thinkingPersistenceCoordinator = thinkingPersistenceCoordinator;
        _chatSwarmFinalizer = chatSwarmFinalizer;
        _toolBlockExecutionCoordinator = toolBlockExecutionCoordinator;
        _agentMemoryService = agentMemoryService;

        // Swarm Services
        _complexityEvaluator = complexityEvaluator;
        _swarmOrchestrator = swarmOrchestrator;
        _swarmEventService = swarmEventService;
        _swarmSessionControlService = swarmSessionControlService;

        // Evaluation Services
        _agentLoopExecutor = agentLoopExecutor;

        // Observability Services
        _tracingService = tracingService;
        _metricsCollector = metricsCollector;

        // Terminal Stream Repository
        _terminalStreamRepository = terminalStreamRepository;
        _terminalOutputBuffer = terminalOutputBuffer;
        _pendingInteractionRepository = pendingInteractionRepository;
    }

    private async Task<ChatSession> EnsureSessionProviderBindingAsync(
        ChatSession chatSession,
        CancellationToken cancellationToken)
    {
        if (chatSession.LLMProviderId.HasValue)
        {
            return chatSession;
        }

        var defaultProvider = await _llmProviderService.GetDefaultProviderAsync(cancellationToken);
        if (defaultProvider == null)
        {
            throw new InvalidOperationException(
                "No LLM provider configured. Please set a default provider in the database or select one for this session.");
        }

        chatSession.LLMProviderId = defaultProvider.Id;
        chatSession.UpdatedAt = DateTime.UtcNow;
        await _chatSessionRepository.UpdateAsync(chatSession, cancellationToken);

        _logger.LogInformation(
            "[AI.Chat] Bound default session LLM Provider | SessionId={SessionId} LLMProviderId={LLMProviderId}",
            chatSession.Id,
            chatSession.LLMProviderId);

        return chatSession;
    }

    private async Task<(ChatMessage UserMessage, ChatMessage AiMessage)> CreateNewTurnMessagesAsync(
        ChatRequest chatRequest,
        ChatSession chatSession,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userMessage = await CreateUserMessageAsync(chatRequest, chatSession, userId, cancellationToken);
        var aiMessage = new ChatMessage
        {
            ChatSessionId = chatSession.Id,
            ParentMessageId = userMessage.Id,
            SenderId = ChatConstants.AssistantSenderId,
            SenderType = ChatConstants.RoleAssistant,
            Content = new Dictionary<string, object> { { ChatMessageContentKeys.Text, string.Empty } },
            MessageType = ChatConstants.MessageTypeText,
            Status = ChatConstants.StatusInProgress
        };

        await _chatMessageRepository.AddAsync(aiMessage, cancellationToken);
        return (userMessage, aiMessage);
    }

    private async Task<(ChatMessage UserMessage, ChatMessage AiMessage)> ResolvePendingInteractionResumeTurnAsync(
        ChatRequest chatRequest,
        ChatSession chatSession,
        Guid pendingInteractionId,
        CancellationToken cancellationToken)
    {
        var interaction = await _pendingInteractionRepository.GetByIdAsync(pendingInteractionId, cancellationToken)
            ?? throw new InvalidOperationException("挂起交互不存在。");

        if (interaction.SessionId != chatSession.Id || interaction.Status != PendingInteractionStatus.Resolved)
        {
            throw new InvalidOperationException("挂起交互未解决或与当前会话不匹配。");
        }

        if (!interaction.MessageId.HasValue || interaction.MessageId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("挂起交互缺少原始 AI 消息。");
        }

        var aiMessage = await _chatMessageRepository.GetByIdAsync(interaction.MessageId.Value, cancellationToken)
            ?? throw new InvalidOperationException("原始 AI 消息不存在。");

        if (aiMessage.ChatSessionId != chatSession.Id
            || !string.Equals(aiMessage.SenderType, ChatConstants.RoleAssistant, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("原始 AI 消息与当前会话不匹配。");
        }

        aiMessage.Status = ChatConstants.StatusInProgress;
        aiMessage.UpdatedAt = DateTime.UtcNow;
        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);

        var controlResumeContent = BuildControlResumeContent(interaction);
        return (CreateControlResumeUserMessage(chatRequest, chatSession, aiMessage, controlResumeContent), aiMessage);
    }

    private static ChatMessage CreateControlResumeUserMessage(
        ChatRequest chatRequest,
        ChatSession chatSession,
        ChatMessage aiMessage,
        string controlResumeContent)
    {
        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = chatSession.Id,
            ParentMessageId = aiMessage.ParentMessageId,
            SenderId = chatSession.UserId,
            SenderType = ChatConstants.RoleUser,
            Content = new Dictionary<string, object>
            {
                { ChatMessageContentKeys.Text, controlResumeContent }
            },
            MessageType = ChatConstants.MessageTypeText,
            Status = ChatConstants.StatusCompleted,
            Metadata = chatRequest.Metadata == null
                ? null
                : new Dictionary<string, object>(chatRequest.Metadata, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string BuildControlResumeContent(PendingInteraction interaction)
    {
        var action = interaction.ResolutionData != null
            && interaction.ResolutionData.TryGetValue(PendingInteractionMetadataKeys.ResolutionAction, out var actionValue)
                ? actionValue?.ToString()
                : null;

        return action switch
        {
            PendingInteractionResolutionActions.ApproveOnce => "我已允许本次命令执行，请继续。",
            PendingInteractionResolutionActions.ApprovePattern => "我已允许当前会话中的同类命令继续执行，请继续。",
            _ => "我已补充所需信息，请继续。"
        };
    }

    private static bool IsPendingInteractionResumeRequest(ChatRequest request, out Guid pendingInteractionId)
    {
        pendingInteractionId = Guid.Empty;
        return request.Metadata != null
            && TryGetBoolMetadata(request.Metadata, ChatMessageMetadataKeys.ResumePendingInteraction)
            && TryGetGuidMetadata(request.Metadata, ChatMessageMetadataKeys.PendingInteractionId, out pendingInteractionId);
    }

    private static bool TryGetBoolMetadata(IReadOnlyDictionary<string, object> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
            && value != null
            && (value is bool boolValue
                ? boolValue
                : bool.TryParse(value.ToString(), out var parsed) && parsed);
    }

    private static bool TryGetGuidMetadata(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        out Guid value)
    {
        value = Guid.Empty;
        return metadata.TryGetValue(key, out var rawValue)
            && rawValue != null
            && Guid.TryParse(rawValue.ToString(), out value)
            && value != Guid.Empty;
    }

    private static string GetMessageContentText(ChatMessage message, string key)
    {
        if (!message.Content.TryGetValue(key, out var value) || value == null)
        {
            return string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<ChatMessageDto> StreamMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        ChannelWriter<BlockDto> blockWriter,
        Func<ChatMessageDto, CancellationToken, Task>? onUserMessageAccepted = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[AI.Chat] Streaming message for user {UserId} in session {SessionId}",
            userId,
            chatRequest.SessionId);

        // 获取或创建会话
        var chatSession = await GetOrCreateChatSessionAsync(chatRequest, userId, cancellationToken);
        if (chatRequest.LLMProviderId.HasValue)
        {
            chatSession.LLMProviderId = chatRequest.LLMProviderId;
        }
        chatSession = await EnsureSessionProviderBindingAsync(chatSession, cancellationToken);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_generationCancellationRegistry.TryRegister(chatSession.Id, cts))
        {
            cts.Dispose();
            throw new InvalidOperationException(ConcurrentGenerationMessage);
        }

        try
        {
            var isControlResume = IsPendingInteractionResumeRequest(chatRequest, out var pendingInteractionId);
            var (userMessage, aiMessage) = isControlResume
                ? await ResolvePendingInteractionResumeTurnAsync(chatRequest, chatSession, pendingInteractionId, cts.Token)
                : await CreateNewTurnMessagesAsync(chatRequest, chatSession, userId, cts.Token);

            if (!isControlResume && onUserMessageAccepted != null)
            {
                var userMessageDto = await BuildAcceptedUserMessageDtoAsync(userMessage, cts.Token);
                await onUserMessageAccepted(userMessageDto, cts.Token);
            }

            if (!isControlResume)
            {
                // === 1. System 1: 语义缓存快思考 (零延迟拦截) ===
                var matchResult = await _agentMemoryService.SearchExperienceAsync(
                    chatRequest.Content,
                    ExperienceType.QA,
                    cts.Token);

                if (matchResult != null)
                {
                    var replayDecision = SystemExperienceReplayPolicy.Decide(matchResult);
                    if (replayDecision.ShouldAnswerDirectly)
                    {
                        _logger.LogInformation(
                            "[AI.Chat] Semantic Cache Hit | SessionId={SessionId} Score={Score} UUID={Id}",
                            chatSession.Id, matchResult.Similarity, matchResult.Experience.Id);

                        return await CompleteSystemExperienceReplayAsync(
                            aiMessage,
                            chatSession,
                            userId,
                            matchResult,
                            replayDecision,
                            blockWriter,
                            cts.Token);
                    }
                    else if (replayDecision.ShouldInjectDynamicContext)
                    {
                        _logger.LogInformation(
                            "[AI.Chat] Partial Cache Hit (DynamicContext) | SessionId={SessionId} Score={Score}",
                            chatSession.Id, matchResult.Similarity);

                        chatRequest.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        chatRequest.Metadata[ChatMessageMetadataKeys.SystemExperienceContext] =
                            SystemExperienceReplayContextBuilder.Build(matchResult);
                        SystemExperienceReplayMetadata.Apply(chatRequest.Metadata, replayDecision);
                    }
                }
            }

            // === 2. Swarm 自动评估与路由 ===
            if (!isControlResume && chatRequest.EnableSwarm)
            {
                var providerId = chatSession.LLMProviderId
                    ?? throw new InvalidOperationException("Swarm 路径要求聊天会话已绑定 LLM Provider。");
                var complexity = await _complexityEvaluator.EvaluateAsync(
                    chatRequest.Content, providerId, null, cts.Token);

                if (_complexityEvaluator.ShouldEscalateToSwarm(complexity))
                {
                    _logger.LogInformation(
                        "[AI.Swarm] Escalating to Swarm | SessionId={SessionId} Score={Score} Domain={Domain}",
                        chatSession.Id, complexity.CompositeScore, complexity.PrimaryDomain);

                    // 预先更新消息状态和内容以便客户端断线重连/刷新时能够无缝恢复 Swarm 看板
                    aiMessage.Metadata ??= new Dictionary<string, object>();
                    aiMessage.Metadata[ChatMessageMetadataKeys.SwarmMode] = true;
                    aiMessage.Content = new Dictionary<string, object>
                    {
                        { ChatMessageContentKeys.Text, SwarmChatPresentation.BuildStartedMessage() }
                    };
                    await _chatMessageRepository.UpdateAsync(aiMessage, cts.Token);

                    await ExecuteSwarmExecutionAsync(
                        aiMessage, chatSession, chatRequest.Content,
                        providerId, complexity, blockWriter, cts.Token);

                    var swarmThinking = aiMessage.Content.ContainsKey(ChatMessageContentKeys.Thinking) ? aiMessage.Content[ChatMessageContentKeys.Thinking]?.ToString() ?? "" : "";
                    var swarmText = aiMessage.Content[ChatMessageContentKeys.Text]?.ToString() ?? string.Empty;
                    return new ChatMessageDto
                    {
                        Id = aiMessage.Id,
                        ChatSessionId = chatSession.Id,
                        SenderId = aiMessage.SenderId,
                        SenderType = aiMessage.SenderType,
                        Content = swarmText,
                        TextContent = swarmText,
                        ThinkingContent = string.IsNullOrEmpty(swarmThinking) ? null : swarmThinking,
                        MessageType = aiMessage.MessageType,
                        CreatedAt = aiMessage.CreatedAt,
                        Metadata = aiMessage.Metadata
                    };
                }

                if (complexity.IsEvaluationFallback)
                {
                    _logger.LogWarning(
                        "[AI.Swarm] Complexity evaluation fallback, using single-agent path | SessionId={SessionId} Reason={Reason}",
                        chatSession.Id,
                        complexity.EvaluationFailureReason ?? "unknown");
                }
                else
                {
                    _logger.LogDebug(
                        "[AI.Chat] Complexity below Swarm threshold | Score={Score}, using single-agent path",
                        complexity.CompositeScore);
                }
            }

            // === 常规单 Agent 流式路径 ===
            await StreamAiResponseAsync(
                aiMessage,
                chatSession,
                userId,
                userMessage,
                chatRequest,
                blockWriter,
                cts.Token);

            // 返回最终生成的 AI 消息 DTO（包含 thinking 以保证客户端 ParseContent 能正确渲染思考过程）
            var finalThinkingContent = aiMessage.Content.ContainsKey(ChatMessageContentKeys.Thinking) ? aiMessage.Content[ChatMessageContentKeys.Thinking]?.ToString() ?? "" : "";
            var finalTextContent = aiMessage.Content[ChatMessageContentKeys.Text]?.ToString() ?? string.Empty;
            return new Shared.DTOs.ChatMessageDto
            {
                Id = aiMessage.Id,
                ChatSessionId = chatSession.Id,
                SenderId = aiMessage.SenderId,
                SenderType = aiMessage.SenderType,
                Content = finalTextContent,
                TextContent = finalTextContent,
                ThinkingContent = string.IsNullOrEmpty(finalThinkingContent) ? null : finalThinkingContent,
                MessageType = aiMessage.MessageType,
                CreatedAt = aiMessage.CreatedAt,
                Metadata = aiMessage.Metadata
            };
        }
        finally
        {
            // 通知消费者：不会再有新 Block（正常/异常路径统一关闭）
            blockWriter.TryComplete();
            _generationCancellationRegistry.Complete(chatSession.Id, cts);
            cts.Dispose();
        }
    }

    private async Task<ChatMessageDto> BuildAcceptedUserMessageDtoAsync(
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        var artifacts = await _artifactService.GetMessageArtifactsAsync(message.Id, cancellationToken);

        return new ChatMessageDto
        {
            Id = message.Id,
            ChatSessionId = message.ChatSessionId,
            ParentMessageId = message.ParentMessageId,
            SenderId = message.SenderId,
            SenderType = message.SenderType,
            Content = GetMessageContentText(message, ChatMessageContentKeys.Text),
            TextContent = GetMessageContentText(message, ChatMessageContentKeys.Text),
            MessageType = message.MessageType,
            Status = message.Status,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
            Metadata = message.Metadata,
            Artifacts = artifacts.Count > 0 ? artifacts : null
        };
    }
}
