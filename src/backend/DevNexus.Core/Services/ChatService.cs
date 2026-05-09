using DevNexus.Core.Services.Chat;
using DevNexus.Core.Abstractions.Observability;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Services.Swarm.Planning;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokenSources = new();

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

    /// <inheritdoc />
    public async Task<ChatMessageDto> StreamMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        ChannelWriter<BlockDto> blockWriter,
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

        // 创建用户消息
        var userMessage = await CreateUserMessageAsync(chatRequest, chatSession, userId, cancellationToken);

        // 创建 AI 消息实体
        var aiMessage = new ChatMessage
        {
            ChatSessionId = chatSession.Id,
            ParentMessageId = userMessage.Id,
            SenderId = ChatConstants.AssistantSenderId,
            SenderType = ChatConstants.RoleAssistant,
            Content = new Dictionary<string, object> { { "text", string.Empty } },
            MessageType = ChatConstants.MessageTypeText,
            Status = ChatConstants.StatusInProgress
        };

        await _chatMessageRepository.AddAsync(aiMessage, cancellationToken);

        // 创建取消令牌源
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokenSources[chatSession.Id] = cts;

        try
        {
            // === 1. System 1: 语义缓存快思考 (零延迟拦截) ===
            var matchResult = await _agentMemoryService.SearchExperienceAsync(
                chatRequest.Content,
                ExperienceType.QA,
                cts.Token);

            if (matchResult != null)
            {
                if (matchResult.Similarity >= MemoryConstants.ChatPerfectHitThreshold)
                {
                    _logger.LogInformation(
                        "[AI.Chat] Semantic Cache Hit | SessionId={SessionId} Score={Score} UUID={Id}",
                        chatSession.Id, matchResult.Similarity, matchResult.Experience.Id);
                    
                    // 命中则直接作为系统响应流出并触发提升，结束大模型消耗流程
                    var directContent = matchResult.Experience.SolutionSop;
                    await blockWriter.WriteAsync(new BlockDto
                    {
                        BlockType = BlockType.TextDelta,
                        Content = directContent,
                        MessageId = aiMessage.Id,
                        SessionId = chatSession.Id
                    }, cts.Token);
                    
                    // 异步触发效用评分提升
                    _ = Task.Run(() => _agentMemoryService.BoostExperienceAsync(matchResult.Experience.Id), CancellationToken.None);

                    aiMessage.Metadata ??= new Dictionary<string, object>();
                    aiMessage.Metadata[ChatMessageMetadataKeys.CacheHit] = true;
                    aiMessage.Metadata[ChatMessageMetadataKeys.Similarity] = matchResult.Similarity;
                    aiMessage.Content = new Dictionary<string, object> { { "text", directContent } };
                    
                    await _chatMessageRepository.UpdateAsync(aiMessage, cts.Token);

                    return new ChatMessageDto
                    {
                        Id = aiMessage.Id,
                        ChatSessionId = chatSession.Id,
                        SenderId = aiMessage.SenderId,
                        SenderType = aiMessage.SenderType,
                        Content = directContent,
                        MessageType = aiMessage.MessageType,
                        CreatedAt = aiMessage.CreatedAt,
                        Metadata = aiMessage.Metadata
                    };
                }
                else if (matchResult.Similarity >= MemoryConstants.ChatPartialHitThreshold)
                {
                    _logger.LogInformation(
                        "[AI.Chat] Partial Cache Hit (Few-Shot) | SessionId={SessionId} Score={Score}",
                        chatSession.Id, matchResult.Similarity);
                    
                    // 部分命中，附加 Context
                    chatRequest.Content = string.Format(PromptConstants.Experience.ChatFewShotPrompt, matchResult.Experience.SolutionSop, chatRequest.Content);
                }
            }

            // === 2. Swarm 自动评估与路由 ===
            if (chatRequest.EnableSwarm)
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
                    var swarmIntro = "🚀 **Swarm 多智能体集群已启动**\n\n"
                        + $"> 复杂度评分: **{complexity.CompositeScore:F1}** | 领域: **{complexity.PrimaryDomain}**\n\n"
                        + "您可以点击上方按钮查看实时执行拓扑图。\n\n---\n\n";
                    aiMessage.Content = new Dictionary<string, object> { { "text", swarmIntro } };
                    await _chatMessageRepository.UpdateAsync(aiMessage, cts.Token);

                    await ExecuteSwarmExecutionAsync(
                        aiMessage, chatSession, chatRequest.Content,
                        providerId, complexity, blockWriter, cts.Token);

                    var swarmThinking = aiMessage.Content.ContainsKey("thinking") ? aiMessage.Content["thinking"]?.ToString() ?? "" : "";
                    var swarmText = aiMessage.Content["text"]?.ToString() ?? string.Empty;
                    return new ChatMessageDto
                    {
                        Id = aiMessage.Id,
                        ChatSessionId = chatSession.Id,
                        SenderId = aiMessage.SenderId,
                        SenderType = aiMessage.SenderType,
                        Content = string.IsNullOrEmpty(swarmThinking) ? swarmText : $"<think>{swarmThinking}</think>\n{swarmText}",
                        MessageType = aiMessage.MessageType,
                        CreatedAt = aiMessage.CreatedAt,
                        Metadata = aiMessage.Metadata
                    };
                }

                _logger.LogDebug(
                    "[AI.Chat] Complexity below Swarm threshold | Score={Score}, using single-agent path",
                    complexity.CompositeScore);
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
            var finalThinkingContent = aiMessage.Content.ContainsKey("thinking") ? aiMessage.Content["thinking"]?.ToString() ?? "" : "";
            var finalTextContent = aiMessage.Content["text"]?.ToString() ?? string.Empty;
            return new Shared.DTOs.ChatMessageDto
            {
                Id = aiMessage.Id,
                ChatSessionId = chatSession.Id,
                SenderId = aiMessage.SenderId,
                SenderType = aiMessage.SenderType,
                Content = string.IsNullOrEmpty(finalThinkingContent) ? finalTextContent : $"<think>{finalThinkingContent}</think>\n{finalTextContent}",
                MessageType = aiMessage.MessageType,
                CreatedAt = aiMessage.CreatedAt,
                Metadata = aiMessage.Metadata
            };
        }
        finally
        {
            // 通知消费者：不会再有新 Block（正常/异常路径统一关闭）
            blockWriter.TryComplete();
            _cancellationTokenSources.TryRemove(chatSession.Id, out _);
            cts.Dispose();
        }
    }

}
