// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using DevNexus.Domain.Models;
using DevNexus.Core.Services.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DevNexus.Infrastructure.Services.LLM;

public partial class KernelService
{
    /// <summary>
    /// 流式聊天完成（使用用户选择的 Provider）
    /// </summary>
    /// <param name="chatHistory">聊天历史</param>
    /// <param name="providerId">用户选择的 Provider ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="enableAutoFunctionCalling">是否启用自动函数调用</param>
    public async IAsyncEnumerable<StreamingChatMessageContent> StreamChatCompletionAsync(
        ChatHistory chatHistory,
        Guid providerId,
        Guid? sessionId = null,
        Guid? messageId = null,
        Guid? userId = null,
        IEnumerable<SkillMatchResult>? matchedSkills = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        bool enableAutoFunctionCalling = true,
        ModelInvocationScopeDto? auditScope = null,
        PromptOptimizationMetadataDto? promptMetadata = null)
    {
        var previousContext = TokenAuditContext.Current;
        Kernel kernel;

        if (sessionId.HasValue)
        {
            // 会话级: 获取缓存的会话专用 Kernel (Session 隐式绑定了 User，安全)
            kernel = await GetKernelForSessionAsync(providerId, sessionId.Value, userId, cancellationToken);

            // Register plugins with session context if available
            RegisterPlugins(kernel, sessionId.Value, userId);
        }
        else
        {
            // 全局级: 获取共享 Kernel
            var sharedKernel = await GetKernelAsync(providerId, cancellationToken);

            // CRITICAL: 必须克隆 Kernel！
            // 否则在共享实例上注册用户级 Plugin 会导致并发请求时的用户上下文污染 (串号)
            kernel = sharedKernel.Clone();

            if (userId.HasValue)
            {
                RegisterKnowledgeBasePlugin(kernel, userId.Value);
            }
        }

        // 注册与匹配到的 Skill 相关的插件
        if (matchedSkills != null)
        {
            RegisterSkillPlugins(kernel, matchedSkills, sessionId, userId);
        }

        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var stopwatch = Stopwatch.StartNew();
        var estimatedOutputTokens = 0;
        StreamingChatMessageContent? lastChunk = null;

        // 获取 Provider 信息并设置 Token 审计上下文（供函数调用过滤器使用）
        var providerInfo = _providerFactory.GetCurrentProviderInfo();
        var streamingContext = BuildAuditContext(previousContext, new ModelInvocationScopeDto
        {
            OwnerType = auditScope?.OwnerType ?? (userId.HasValue ? ModelInvocationOwnerTypes.User : ModelInvocationOwnerTypes.System),
            OwnerUserId = auditScope?.OwnerUserId ?? userId,
            SessionId = auditScope?.SessionId ?? sessionId,
            MessageId = auditScope?.MessageId ?? messageId,
            SceneCode = auditScope?.SceneCode ?? ModelInvocationSceneCodes.ChatMessageReply,
            SceneCategory = auditScope?.SceneCategory ?? ModelInvocationSceneCategories.UserFacing,
            ResourceType = auditScope?.ResourceType ?? (messageId.HasValue ? ModelInvocationResourceTypes.Message : ModelInvocationResourceTypes.Session),
            ResourceId = auditScope?.ResourceId ?? messageId?.ToString() ?? sessionId?.ToString()
        }, ModelInvocationKinds.StreamingChat);
        TokenAuditContext.Current = AttachProviderInfo(
            streamingContext,
            providerInfo?.ModelName ?? "unknown",
            providerInfo?.ProviderName ?? "unknown",
            providerInfo?.ProviderId ?? "unknown",
            providerInfo?.LLMProviderId ?? Guid.Empty,
            promptMetadata);

        var autoFunctionCallingEnabled = ShouldEnableAutoFunctionCalling(enableAutoFunctionCalling);

        _logger.LogDebug(
            "[AI.Kernel] Starting streaming chat completion | ProviderId={ProviderId} MessageCount={Count} AutoFC={AutoFC}",
            providerId,
            chatHistory.Count,
            autoFunctionCallingEnabled);

        PromptExecutionSettings? executionSettings = null;
        if (autoFunctionCallingEnabled)
        {
            executionSettings = new PromptExecutionSettings
            {
                FunctionChoiceBehavior = ToolInvocationConcurrencyPolicy.CreateAutoFunctionChoiceBehavior(
                    _toolCatalogService.GetAllTools(),
                    kernel.Plugins.Select(plugin => plugin.Name))
            };
        }

        IAsyncEnumerable<StreamingChatMessageContent> streamingContents;
        try
        {
            streamingContents = chatCompletion.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "[AI.Kernel] Streaming chat completion cancelled before enumeration | ProviderId={ProviderId}",
                providerId);
            TokenAuditContext.Current = previousContext; // 清理上下文
            yield break;
        }
        catch (Exception ex)
        {
            TokenAuditContext.Current = previousContext; // 清理上下文
            LogApiError(ex, providerId, chatHistory);
            throw;
        }

        try
        {
            await foreach (var content in streamingContents)
            {
                lastChunk = content;
                if (!string.IsNullOrEmpty(content.Content))
                {
                    estimatedOutputTokens += EstimateTokenCount(content.Content);
                }

                yield return content;
            }

            stopwatch.Stop();

            // 优先从 Metadata 提取实际 Token 使用量，否则使用估算值
            var tokenUsage = ExtractTokenUsageFromMetadata(lastChunk?.Metadata);
            var inputTokenCount = tokenUsage.InputTokens ?? EstimateChatHistoryTokens(chatHistory);
            var outputTokenCount = tokenUsage.OutputTokens ?? estimatedOutputTokens;
            var tokenSource = tokenUsage.InputTokens.HasValue ? "Actual" : "Estimated";

            _logger.LogDebug(
                "[AI.TokenAudit] Streaming completion finished | " +
                "ProviderId={ProviderId} InputTokens={InputTokens} OutputTokens={OutputTokens} " +
                "TotalTokens={TotalTokens} Duration={Duration}ms Source={Source}",
                providerId,
                inputTokenCount,
                outputTokenCount,
                inputTokenCount + outputTokenCount,
                stopwatch.ElapsedMilliseconds,
                tokenSource);

            _tokenAuditService.RecordStreamingCompletion(
                sessionId,
                messageId,
                userId,
                providerInfo?.ModelName ?? "unknown",
                providerInfo?.ProviderName ?? "unknown",
                ModelInvocationProviderTypes.Llm,
                providerInfo?.LLMProviderId.ToString() ?? Guid.Empty.ToString(),
                inputTokenCount,
                outputTokenCount,
                stopwatch.ElapsedMilliseconds,
                invocationKind: ModelInvocationKinds.StreamingChat,
                sceneCode: auditScope?.SceneCode ?? ModelInvocationSceneCodes.ChatMessageReply,
                sceneCategory: auditScope?.SceneCategory ?? ModelInvocationSceneCategories.UserFacing,
                resourceType: auditScope?.ResourceType ?? (messageId.HasValue ? ModelInvocationResourceTypes.Message : ModelInvocationResourceTypes.Session),
                resourceId: auditScope?.ResourceId ?? messageId?.ToString() ?? sessionId?.ToString(),
                usageSource: tokenUsage.InputTokens.HasValue ? ModelInvocationUsageSources.Actual : ModelInvocationUsageSources.Estimated,
                cachedPromptTokens: tokenUsage.CachedPromptTokens,
                promptCacheKey: promptMetadata?.PromptCacheKey,
                stablePrefixHash: promptMetadata?.StablePrefixHash,
                toolSchemaHash: promptMetadata?.ToolSchemaHash,
                dynamicContextTokens: promptMetadata?.DynamicContextTokens,
                historyTokens: promptMetadata?.HistoryTokens,
                cacheMarkerCandidateCount: promptMetadata?.CacheMarkerCandidateCount,
                cacheDoubleMarkerReady: promptMetadata?.CacheDoubleMarkerReady);
        }
        finally
        {
            // 清理审计上下文，防止请求间串扰
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <summary>
    /// 非流式聊天完成（带 Token 审计）
    /// </summary>
    public async Task<ChatMessageContent> GetChatCompletionAsync(
        ChatHistory chatHistory,
        Guid providerId,
        Guid? sessionId = null,
        Guid? messageId = null,
        Guid? userId = null,
        IEnumerable<SkillMatchResult>? matchedSkills = null,
        CancellationToken cancellationToken = default,
        bool enableAutoFunctionCalling = true,
        ModelInvocationScopeDto? auditScope = null,
        PromptOptimizationMetadataDto? promptMetadata = null)
    {
        var previousContext = TokenAuditContext.Current;
        try
        {
            var kernel = await GetKernelAsync(providerId, cancellationToken);

            // 注册 Skill 绑定的插件
            if (matchedSkills != null)
            {
                RegisterSkillPlugins(kernel, matchedSkills, sessionId, userId);
            }

            var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
            OpenAIPromptExecutionSettings? executionSettings = null;
            if (ShouldEnableAutoFunctionCalling(enableAutoFunctionCalling))
            {
                executionSettings = new OpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = ToolInvocationConcurrencyPolicy.CreateAutoFunctionChoiceBehavior(
                        _toolCatalogService.GetAllTools(),
                        kernel.Plugins.Select(plugin => plugin.Name))
                };
            }

            var stopwatch = Stopwatch.StartNew();
            var providerInfo = _providerFactory.GetCurrentProviderInfo();
            TokenAuditContext.Current = new TokenAuditContext
            {
                OwnerType = auditScope?.OwnerType ?? (userId.HasValue ? ModelInvocationOwnerTypes.User : ModelInvocationOwnerTypes.System),
                OwnerUserId = auditScope?.OwnerUserId ?? userId,
                SessionId = auditScope?.SessionId ?? sessionId,
                MessageId = auditScope?.MessageId ?? messageId,
                InvocationKind = ModelInvocationKinds.ChatCompletion,
                SceneCode = auditScope?.SceneCode ?? ModelInvocationSceneCodes.ChatMessageReply,
                SceneCategory = auditScope?.SceneCategory ?? ModelInvocationSceneCategories.UserFacing,
                ResourceType = auditScope?.ResourceType ?? (messageId.HasValue ? ModelInvocationResourceTypes.Message : ModelInvocationResourceTypes.Session),
                ResourceId = auditScope?.ResourceId ?? messageId?.ToString() ?? sessionId?.ToString(),
                ModelName = providerInfo?.ModelName ?? "unknown",
                ProviderType = ModelInvocationProviderTypes.Llm,
                ProviderName = providerInfo?.ProviderName ?? "unknown",
                ProviderId = providerInfo?.ProviderId ?? "unknown",
                LLMProviderId = providerInfo?.LLMProviderId ?? Guid.Empty,
                PromptCacheKey = promptMetadata?.PromptCacheKey,
                StablePrefixHash = promptMetadata?.StablePrefixHash,
                ToolSchemaHash = promptMetadata?.ToolSchemaHash,
                DynamicContextTokens = promptMetadata?.DynamicContextTokens,
                HistoryTokens = promptMetadata?.HistoryTokens,
                StablePrefixManifest = promptMetadata?.StablePrefixManifest
                    ?? Array.Empty<PromptFragmentManifestItemDto>(),
                DynamicContextManifest = promptMetadata?.DynamicContextManifest
                    ?? Array.Empty<PromptFragmentManifestItemDto>()
            };

            var result = await chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: executionSettings,
                kernel: kernel,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            // 优先从 Metadata 提取实际 Token 使用量，否则使用估算值
            var tokenUsage = ExtractTokenUsageFromMetadata(result.Metadata);
            var inputTokenCount = tokenUsage.InputTokens ?? EstimateChatHistoryTokens(chatHistory);
            var outputTokenCount = tokenUsage.OutputTokens ?? EstimateTokenCount(result.Content ?? string.Empty);
            var tokenSource = tokenUsage.InputTokens.HasValue ? "Actual" : "Estimated";

            _logger.LogDebug(
                "[AI.TokenAudit] Non-streaming completion finished | " +
                "ProviderId={ProviderId} InputTokens={InputTokens} OutputTokens={OutputTokens} " +
                "TotalTokens={TotalTokens} Duration={Duration}ms Source={Source}",
                providerId,
                inputTokenCount,
                outputTokenCount,
                inputTokenCount + outputTokenCount,
                stopwatch.ElapsedMilliseconds,
                tokenSource);

            try
            {
                _tokenAuditService.RecordStreamingCompletion(
                    sessionId,
                    messageId,
                    userId,
                    providerInfo?.ModelName ?? "unknown",
                    providerInfo?.ProviderName ?? "unknown",
                    ModelInvocationProviderTypes.Llm,
                    providerInfo?.LLMProviderId.ToString() ?? Guid.Empty.ToString(),
                    inputTokenCount,
                    outputTokenCount,
                    stopwatch.ElapsedMilliseconds,
                    invocationKind: ModelInvocationKinds.ChatCompletion,
                    sceneCode: auditScope?.SceneCode ?? ModelInvocationSceneCodes.ChatMessageReply,
                    sceneCategory: auditScope?.SceneCategory ?? ModelInvocationSceneCategories.UserFacing,
                    resourceType: auditScope?.ResourceType ?? (messageId.HasValue ? ModelInvocationResourceTypes.Message : ModelInvocationResourceTypes.Session),
                    resourceId: auditScope?.ResourceId ?? messageId?.ToString() ?? sessionId?.ToString(),
                    usageSource: tokenUsage.InputTokens.HasValue ? ModelInvocationUsageSources.Actual : ModelInvocationUsageSources.Estimated,
                    cachedPromptTokens: tokenUsage.CachedPromptTokens,
                    promptCacheKey: promptMetadata?.PromptCacheKey,
                    stablePrefixHash: promptMetadata?.StablePrefixHash,
                    toolSchemaHash: promptMetadata?.ToolSchemaHash,
                    dynamicContextTokens: promptMetadata?.DynamicContextTokens,
                    historyTokens: promptMetadata?.HistoryTokens,
                    cacheMarkerCandidateCount: promptMetadata?.CacheMarkerCandidateCount,
                    cacheDoubleMarkerReady: promptMetadata?.CacheDoubleMarkerReady);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AI.TokenAudit] Failed to record completion audit");
            }

            return result;
        }
        catch (Exception ex)
        {
            LogApiError(ex, providerId, chatHistory);
            throw;
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <summary>
    /// Vision 聊天完成（支持图片消息）
    /// </summary>
    public async Task<ChatMessageContent> GetVisionChatCompletionAsync(
        string prompt,
        string imageDataUrl,
        Guid providerId,
        CancellationToken cancellationToken = default,
        ModelInvocationScopeDto? auditScope = null)
    {
        var previousContext = TokenAuditContext.Current;
        var kernel = await GetKernelAsync(providerId, cancellationToken);
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var stopwatch = Stopwatch.StartNew();
        var providerInfo = _providerFactory.GetCurrentProviderInfo();

        TokenAuditContext.Current = new TokenAuditContext
        {
            OwnerType = auditScope?.OwnerType ?? ModelInvocationOwnerTypes.System,
            OwnerUserId = auditScope?.OwnerUserId,
            SessionId = auditScope?.SessionId,
            MessageId = auditScope?.MessageId,
            InvocationKind = ModelInvocationKinds.Vision,
            SceneCode = auditScope?.SceneCode ?? ModelInvocationSceneCodes.VisionImageUnderstanding,
            SceneCategory = auditScope?.SceneCategory ?? ModelInvocationSceneCategories.Parsing,
            ResourceType = auditScope?.ResourceType ?? ModelInvocationResourceTypes.Artifact,
            ResourceId = auditScope?.ResourceId,
            ModelName = providerInfo?.ModelName ?? "unknown",
            ProviderType = ModelInvocationProviderTypes.Llm,
            ProviderName = providerInfo?.ProviderName ?? "unknown",
            ProviderId = providerInfo?.ProviderId ?? "unknown",
            LLMProviderId = providerInfo?.LLMProviderId ?? Guid.Empty
        };

        _logger.LogInformation(
            "[AI.Kernel] Starting Vision chat completion | ProviderId={ProviderId}",
            providerId);

        // 构建包含图片的聊天历史
        var chatHistory = new ChatHistory();

        ImageContent imageContent;
        if (imageDataUrl.StartsWith("data:"))
        {
            var dataUriParts = imageDataUrl.Split(',', 2);
            if (dataUriParts.Length == 2)
            {
                var mimeTypePart = dataUriParts[0];
                var base64Data = dataUriParts[1];
                var mimeType = "image/png";
                if (mimeTypePart.StartsWith("data:") && mimeTypePart.Contains(";"))
                {
                    mimeType = mimeTypePart.Substring(5, mimeTypePart.IndexOf(';') - 5);
                }
                var imageBytes = Convert.FromBase64String(base64Data);
                imageContent = new ImageContent(new BinaryData(imageBytes), mimeType);
            }
            else
            {
                imageContent = new ImageContent(new Uri(imageDataUrl));
            }
        }
        else
        {
            imageContent = new ImageContent(new Uri(imageDataUrl));
        }

        chatHistory.AddUserMessage(new ChatMessageContentItemCollection
        {
            new TextContent(prompt),
            imageContent
        });

        try
        {
            var result = await chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            var tokenUsage = ExtractTokenUsageFromMetadata(result.Metadata);
            var inputTokenCount = tokenUsage.InputTokens ?? EstimateTokenCount(prompt);
            var outputTokenCount = tokenUsage.OutputTokens ?? EstimateTokenCount(result.Content ?? string.Empty);

            _tokenAuditService.RecordStreamingCompletion(
                null,
                null,
                null,
                providerInfo?.ModelName ?? "unknown",
                providerInfo?.ProviderName ?? "unknown",
                ModelInvocationProviderTypes.Llm,
                providerInfo?.LLMProviderId.ToString() ?? Guid.Empty.ToString(),
                inputTokenCount,
                outputTokenCount,
                stopwatch.ElapsedMilliseconds,
                invocationKind: ModelInvocationKinds.Vision,
                sceneCode: auditScope?.SceneCode ?? ModelInvocationSceneCodes.VisionImageUnderstanding,
                sceneCategory: auditScope?.SceneCategory ?? ModelInvocationSceneCategories.Parsing,
                resourceType: auditScope?.ResourceType ?? ModelInvocationResourceTypes.Artifact,
                resourceId: auditScope?.ResourceId,
                usageSource: tokenUsage.InputTokens.HasValue ? ModelInvocationUsageSources.Actual : ModelInvocationUsageSources.Estimated,
                cachedPromptTokens: tokenUsage.CachedPromptTokens);

            _logger.LogInformation(
                "[AI.TokenAudit] Vision completion finished | ProviderId={ProviderId} Tokens={Tokens}",
                providerId, inputTokenCount + outputTokenCount);

            return result;
        }
        catch (Exception ex)
        {
            LogApiError(ex, providerId, chatHistory);
            throw;
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <summary>
    /// 简单文本生成（使用默认 Provider）
    /// </summary>
    public async Task<string> GenerateTextAsync(
        string prompt,
        CancellationToken cancellationToken = default,
        ModelInvocationScopeDto? auditScope = null)
    {
        var previousContext = TokenAuditContext.Current;
        var kernel = await GetKernelAsync(cancellationToken);
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var stopwatch = Stopwatch.StartNew();

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        _logger.LogDebug(
            "[AI.Kernel] Generating text | Provider={Provider}",
            _currentProvider?.ProviderName ?? "unknown");

        try
        {
            var providerInfo = _providerFactory.GetCurrentProviderInfo();
            TokenAuditContext.Current = new TokenAuditContext
            {
                OwnerType = auditScope?.OwnerType ?? TokenAuditContext.Current?.OwnerType ?? ModelInvocationOwnerTypes.System,
                OwnerUserId = auditScope?.OwnerUserId ?? TokenAuditContext.Current?.OwnerUserId,
                SessionId = auditScope?.SessionId ?? TokenAuditContext.Current?.SessionId,
                MessageId = auditScope?.MessageId ?? TokenAuditContext.Current?.MessageId,
                InvocationKind = ModelInvocationKinds.ChatCompletion,
                SceneCode = auditScope?.SceneCode ?? TokenAuditContext.Current?.SceneCode ?? ModelInvocationSceneCodes.SystemOther,
                SceneCategory = auditScope?.SceneCategory ?? TokenAuditContext.Current?.SceneCategory ?? ModelInvocationSceneCategories.Other,
                ResourceType = auditScope?.ResourceType ?? TokenAuditContext.Current?.ResourceType ?? ModelInvocationResourceTypes.None,
                ResourceId = auditScope?.ResourceId ?? TokenAuditContext.Current?.ResourceId,
                ModelName = providerInfo?.ModelName ?? "unknown",
                ProviderType = ModelInvocationProviderTypes.Llm,
                ProviderName = providerInfo?.ProviderName ?? "unknown",
                ProviderId = providerInfo?.ProviderId ?? "unknown",
                LLMProviderId = providerInfo?.LLMProviderId ?? Guid.Empty
            };

            var result = await chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);
            stopwatch.Stop();

            var tokenUsage = ExtractTokenUsageFromMetadata(result.Metadata);
            var inputTokenCount = tokenUsage.InputTokens ?? EstimateTokenCount(prompt);
            var outputTokenCount = tokenUsage.OutputTokens ?? EstimateTokenCount(result.Content ?? string.Empty);

            _tokenAuditService.RecordStreamingCompletion(
                TokenAuditContext.Current?.SessionId,
                TokenAuditContext.Current?.MessageId,
                TokenAuditContext.Current?.OwnerUserId,
                providerInfo?.ModelName ?? "unknown",
                providerInfo?.ProviderName ?? "unknown",
                ModelInvocationProviderTypes.Llm,
                providerInfo?.LLMProviderId.ToString() ?? Guid.Empty.ToString(),
                inputTokenCount,
                outputTokenCount,
                stopwatch.ElapsedMilliseconds,
                invocationKind: ModelInvocationKinds.ChatCompletion,
                sceneCode: TokenAuditContext.Current?.SceneCode ?? ModelInvocationSceneCodes.SystemOther,
                sceneCategory: TokenAuditContext.Current?.SceneCategory ?? ModelInvocationSceneCategories.Other,
                resourceType: TokenAuditContext.Current?.ResourceType ?? ModelInvocationResourceTypes.None,
                resourceId: TokenAuditContext.Current?.ResourceId,
                usageSource: tokenUsage.InputTokens.HasValue ? ModelInvocationUsageSources.Actual : ModelInvocationUsageSources.Estimated,
                cachedPromptTokens: tokenUsage.CachedPromptTokens);

            return result.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            LogApiError(ex, Guid.Empty, chatHistory);
            throw WrapNonJsonResponseException(ex);
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    private bool ShouldEnableAutoFunctionCalling(bool requested)
    {
        if (!requested)
        {
            return false;
        }

        if (_currentProvider?.SupportsAutoFunctionCalling != false)
        {
            return true;
        }

        _logger.LogWarning(
            "[AI.Kernel] Auto function calling disabled for provider {ProviderName} because the current provider integration is not runtime-compatible.",
            _currentProvider.ProviderName);

        return false;
    }
}
