using DevNexus.Core.Abstractions;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DevNexus.Core.Services.LLM;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务实现
/// </summary>
public class ChatService : IChatService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<ChatService> _logger;
    private readonly KernelService _kernelService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellationTokenSources = new();
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="distributedCache">分布式缓存</param>
    /// <param name="logger">日志记录器</param>
    public ChatService(
        ApplicationDbContext dbContext,
        IDistributedCache distributedCache,
        ILogger<ChatService> logger,
        KernelService kernelService,
        ILoggerFactory loggerFactory)
    {
        _dbContext = dbContext;
        _distributedCache = distributedCache;
        _logger = logger;
        _kernelService = kernelService;
        _loggerFactory = loggerFactory;
    }
    
    /// <inheritdoc />
    public async Task<Guid> CreateChatSessionAsync(
        Guid userId,
        string title,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating chat session for user {UserId} with title {Title}",
            userId,
            title);
        
        var chatSession = new ChatSession
        {
            UserId = userId,
            Title = title,
            IsActive = true
        };
        
        await _dbContext.ChatSessions.AddAsync(chatSession, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return chatSession.Id;
    }
    
    /// <inheritdoc />
    public async Task SendMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending message for user {UserId} in session {SessionId}",
            userId,
            chatRequest.SessionId);
        
        // 获取或创建会话
        var chatSession = await GetOrCreateChatSessionAsync(chatRequest, userId, cancellationToken);
        
        // 创建用户消息
        var userMessage = await CreateUserMessageAsync(chatRequest, chatSession, userId, cancellationToken);
        
        // 生成AI响应
        await GenerateAiResponseAsync(
            userMessage,
            chatSession,
            userId,
            cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task CancelMessageGenerationAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Cancelling message generation for session {SessionId}",
            sessionId);
        
        if (_cancellationTokenSources.TryGetValue(sessionId, out var cts))
        {
            cts.Cancel();
            _cancellationTokenSources.Remove(sessionId);
            
            // 更新最后一条AI消息状态为已取消
            var lastAiMessage = await _dbContext.ChatMessages
                .Where(m => m.ChatSessionId == sessionId && m.SenderType == "assistant")
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (lastAiMessage != null)
            {
                lastAiMessage.Status = "cancelled";
                lastAiMessage.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
    
    /// <inheritdoc />
    public async Task<List<ChatSessionDto>> GetChatSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Getting chat sessions for user {UserId}",
            userId);
        
        var sessions = await _dbContext.ChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);
        
        // 转换为DTO
        return sessions.Select(session => new ChatSessionDto
        {
            Id = session.Id,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            IsActive = session.IsActive,
            MessageCount = session.Messages.Count
        }).ToList();
    }
    
    /// <inheritdoc />
    public async Task<List<ChatMessageDto>> GetChatMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Getting chat messages for session {SessionId}",
            sessionId);
        
        var messages = await _dbContext.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        
        // 转换为DTO
        return messages.Select(message => new ChatMessageDto
        {
            Id = message.Id,
            ChatSessionId = message.ChatSessionId,
            ParentMessageId = message.ParentMessageId,
            SenderId = message.SenderId,
            SenderType = message.SenderType,
            Content = JsonSerializer.Serialize(message.Content),
            MessageType = message.MessageType,
            Status = message.Status,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
            Metadata = message.Metadata
        }).ToList();
    }
    
    /// <summary>
    /// 获取或创建聊天会话
    /// </summary>
    /// <param name="chatRequest">聊天请求</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聊天会话</returns>
    private async Task<ChatSession> GetOrCreateChatSessionAsync(
        ChatRequest chatRequest,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (chatRequest.SessionId.HasValue)
        {
            // 检查会话是否存在且属于该用户
            var existingSession = await _dbContext.ChatSessions
                .FirstOrDefaultAsync(
                    s => s.Id == chatRequest.SessionId.Value && s.UserId == userId,
                    cancellationToken);
            
            if (existingSession != null)
            {
                return existingSession;
            }
        }
        
        // 创建新会话
        return await CreateNewChatSessionAsync(chatRequest.Content, userId, cancellationToken);
    }
    
    /// <summary>
    /// 创建新的聊天会话
    /// </summary>
    /// <param name="messageContent">消息内容</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聊天会话</returns>
    private async Task<ChatSession> CreateNewChatSessionAsync(
        string messageContent,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 使用消息前50个字符作为会话标题
        var sessionTitle = messageContent.Length > 50
            ? messageContent[..50] + "..."
            : messageContent;
        
        var chatSession = new ChatSession
        {
            UserId = userId,
            Title = sessionTitle,
            IsActive = true
        };
        
        await _dbContext.ChatSessions.AddAsync(chatSession, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return chatSession;
    }
    
    /// <summary>
    /// 创建用户消息
    /// </summary>
    /// <param name="chatRequest">聊天请求</param>
    /// <param name="chatSession">聊天会话</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聊天消息</returns>
    private async Task<ChatMessage> CreateUserMessageAsync(
        ChatRequest chatRequest,
        ChatSession chatSession,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var message = new ChatMessage
        {
            ChatSessionId = chatSession.Id,
            ParentMessageId = chatRequest.ParentMessageId,
            SenderId = userId,
            SenderType = "user",
            Content = new Dictionary<string, object>
            {
                { "text", chatRequest.Content }
            },
            MessageType = chatRequest.MessageType,
            Status = "completed"
        };
        
        await _dbContext.ChatMessages.AddAsync(message, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return message;
    }
    
    /// <summary>
    /// 生成AI响应
    /// </summary>
    /// <param name="userMessage">用户消息</param>
    /// <param name="chatSession">聊天会话</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    private async Task GenerateAiResponseAsync(
        ChatMessage userMessage,
        ChatSession chatSession,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 创建AI消息
        var aiMessage = new ChatMessage
        {
            ChatSessionId = chatSession.Id,
            ParentMessageId = userMessage.Id,
            SenderId = Guid.Parse("00000000-0000-0000-0000-000000000000"), // AI发送者ID
            SenderType = "assistant",
            Content = new Dictionary<string, object>
            {
                { "text", string.Empty }
            },
            MessageType = "text",
            Status = "in_progress"
        };
        
        await _dbContext.ChatMessages.AddAsync(aiMessage, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // 模拟AI思考和响应生成
        await SimulateAiResponseGenerationAsync(
            aiMessage,
            chatSession,
            userId,
            cancellationToken);
    }
    
    /// <summary>
    /// 模拟AI响应生成
    /// </summary>
    /// <param name="aiMessage">AI消息</param>
    /// <param name="chatSession">聊天会话</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    private async Task SimulateAiResponseGenerationAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 模拟思考时间
            await Task.Delay(1000, cancellationToken);
            
            // 模拟AI响应
            var aiResponse = "这是一个AI生成的响应。\n\n我可以帮助你：\n1. 解答技术问题\n2. 生成代码\n3. 提供建议\n4. 进行对话";
            
            // 更新AI消息内容
            aiMessage.Content = new Dictionary<string, object>
            {
                { "text", aiResponse }
            };
            aiMessage.Status = "completed";
            aiMessage.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "AI response generation cancelled for message {MessageId}",
                aiMessage.Id);
            
            // 更新消息状态为已取消
            aiMessage.Status = "cancelled";
            aiMessage.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating AI response for message {MessageId}",
                aiMessage.Id);
            
            // 更新消息状态为错误
            aiMessage.Status = "error";
            aiMessage.Content = new Dictionary<string, object>
            {
                { "text", $"生成响应时发生错误：{ex.Message}" }
            };
            aiMessage.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
    
    /// <inheritdoc />
    public async Task StreamMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        Func<BlockDto, Task> onBlockReceived,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AI.Chat] Streaming message for user {UserId} in session {SessionId}",
            userId,
            chatRequest.SessionId);
        
        // 获取或创建会话
        var chatSession = await GetOrCreateChatSessionAsync(chatRequest, userId, cancellationToken);
        
        // 创建用户消息
        var userMessage = await CreateUserMessageAsync(chatRequest, chatSession, userId, cancellationToken);
        
        // 创建 AI 消息实体
        var aiMessage = new ChatMessage
        {
            ChatSessionId = chatSession.Id,
            ParentMessageId = userMessage.Id,
            SenderId = Guid.Parse("00000000-0000-0000-0000-000000000000"),
            SenderType = "assistant",
            Content = new Dictionary<string, object> { { "text", string.Empty } },
            MessageType = "text",
            Status = "in_progress"
        };
        
        await _dbContext.ChatMessages.AddAsync(aiMessage, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // 创建取消令牌源
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokenSources[chatSession.Id] = cts;
        
        try
        {
            // 流式生成 AI 响应
            await StreamAiResponseAsync(
                aiMessage,
                chatSession,
                userId,
                userMessage,
                onBlockReceived,
                cts.Token);
        }
        finally
        {
            _cancellationTokenSources.Remove(chatSession.Id);
            cts.Dispose();
        }
    }
    
    /// <summary>
    /// 流式生成 AI 响应
    /// </summary>
    private async Task StreamAiResponseAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        Guid userId,
        ChatMessage userMessage,
        Func<BlockDto, Task> onBlockReceived,
        CancellationToken cancellationToken)
    {
        try
        {
            // 构建聊天历史
            var chatHistory = await BuildChatHistoryAsync(chatSession.Id, cancellationToken);
            chatHistory.AddUserMessage(userMessage.Content["text"].ToString() ?? string.Empty);
            
            // 创建 Block 解析器
            var blockParser = new BlockParser(_loggerFactory.CreateLogger<BlockParser>());
            var fullResponse = new System.Text.StringBuilder();
            
            _logger.LogInformation(
                "[AI.Chat] Starting streaming completion | SessionId={SessionId} MessageId={MessageId}",
                chatSession.Id,
                aiMessage.Id);
            
            // 流式调用 LLM
            await foreach (var streamContent in _kernelService.StreamChatCompletionAsync(
                chatHistory,
                cancellationToken))
            {
                var content = streamContent.Content;
                if (string.IsNullOrEmpty(content))
                    continue;
                
                fullResponse.Append(content);
                
                // 解析并发送 Block
                var blocks = blockParser.Parse(content, aiMessage.Id);
                foreach (var block in blocks)
                {
                    await onBlockReceived(block);
                }
            }
            
            // 发送最后的 Block
            var finalBlocks = blockParser.Finish(aiMessage.Id);
            foreach (var block in finalBlocks)
            {
                await onBlockReceived(block);
            }
            
            // 更新 AI 消息内容
            aiMessage.Content = new Dictionary<string, object>
            {
                { "text", fullResponse.ToString() }
            };
            aiMessage.Status = "completed";
            aiMessage.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            
            _logger.LogInformation(
                "[AI.Chat] Streaming completed | SessionId={SessionId} MessageId={MessageId} Length={Length}",
                chatSession.Id,
                aiMessage.Id,
                fullResponse.Length);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "[AI.Chat] Streaming cancelled | SessionId={SessionId} MessageId={MessageId}",
                chatSession.Id,
                aiMessage.Id);
            
            aiMessage.Status = "cancelled";
            aiMessage.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[AI.Chat] Streaming error | SessionId={SessionId} MessageId={MessageId}",
                chatSession.Id,
                aiMessage.Id);
            
            aiMessage.Status = "error";
            aiMessage.Content = new Dictionary<string, object>
            {
                { "text", $"生成响应时发生错误：{ex.Message}" }
            };
            aiMessage.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            
            throw;
        }
    }
    
    /// <summary>
    /// 构建聊天历史
    /// </summary>
    private async Task<ChatHistory> BuildChatHistoryAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var chatHistory = new ChatHistory();
        
        // 获取最近 20 条消息
        var recentMessages = await _dbContext.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        
        foreach (var message in recentMessages)
        {
            var content = message.Content.ContainsKey("text")
                ? message.Content["text"].ToString() ?? string.Empty
                : string.Empty;
            
            if (message.SenderType == "user")
            {
                chatHistory.AddUserMessage(content);
            }
            else if (message.SenderType == "assistant")
            {
                chatHistory.AddAssistantMessage(content);
            }
        }
        
        return chatHistory;
    }
}
