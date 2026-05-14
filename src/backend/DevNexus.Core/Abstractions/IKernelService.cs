using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using DevNexus.Core.Models;
using DevNexus.Domain.Models;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 图片生成结果
/// </summary>
public record ImageGenerationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public required bool Success { get; init; }
    
    /// <summary>
    /// 生成的图片 URL（成功时有值）
    /// </summary>
    public string? ImageUrl { get; init; }
    
    /// <summary>
    /// Markdown 格式的图片链接
    /// </summary>
    public string? Markdown { get; init; }
    
    /// <summary>
    /// 使用的 Provider 名称
    /// </summary>
    public string? ProviderName { get; init; }
    
    /// <summary>
    /// 错误信息（失败时有值）
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Semantic Kernel 服务接口
/// 提供统一的 AI 聊天完成接口
/// </summary>
public interface IKernelService
{
    /// <summary>
    /// 流式聊天完成（使用用户选择的 Provider）
    /// </summary>
    IAsyncEnumerable<StreamingChatMessageContent> StreamChatCompletionAsync(
        ChatHistory chatHistory,
        Guid providerId,
        Guid? sessionId = null,
        Guid? messageId = null,
        Guid? userId = null,
        IEnumerable<SkillMatchResult>? matchedSkills = null,
        CancellationToken cancellationToken = default,
        bool enableAutoFunctionCalling = true,
        ModelInvocationScopeDto? auditScope = null,
        PromptOptimizationMetadataDto? promptMetadata = null);

    /// <summary>
    /// 非流式聊天完成（带 Token 审计）
    /// </summary>
    Task<ChatMessageContent> GetChatCompletionAsync(
        ChatHistory chatHistory,
        Guid providerId,
        Guid? sessionId = null,
        Guid? messageId = null,
        Guid? userId = null,
        IEnumerable<SkillMatchResult>? matchedSkills = null,
        CancellationToken cancellationToken = default,
        bool enableAutoFunctionCalling = true,
        ModelInvocationScopeDto? auditScope = null,
        PromptOptimizationMetadataDto? promptMetadata = null);

    /// <summary>
    /// Vision 聊天完成（支持图片消息）
    /// </summary>
    Task<ChatMessageContent> GetVisionChatCompletionAsync(
        string prompt,
        string imageDataUrl,
        Guid providerId,
        CancellationToken cancellationToken = default,
        ModelInvocationScopeDto? auditScope = null);

    /// <summary>
    /// 获取指定 Provider 的 Kernel 实例
    /// </summary>
    Task<Kernel> GetKernelAsync(Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成图片（自动选择支持 Text-to-Image 的 Provider）
    /// 如果当前 Provider 不支持，会自动降级到最高优先级的支持 TextToImage 的 Provider
    /// </summary>
    /// <param name="prompt">图片描述</param>
    /// <param name="width">图片宽度</param>
    /// <param name="height">图片高度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成结果，包含图片 URL 和使用的 Provider 信息</returns>
    Task<ImageGenerationResult> GenerateImageAsync(
        string prompt,
        int width = 1024,
        int height = 1024,
        CancellationToken cancellationToken = default,
        ModelInvocationScopeDto? auditScope = null);

    /// <summary>
    /// 简单文本生成（使用默认 Provider）
    /// 用于后台任务等不需要流式响应的场景
    /// </summary>
    /// <param name="prompt">提示词</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的文本内容</returns>
    Task<string> GenerateTextAsync(
        string prompt,
        CancellationToken cancellationToken = default,
        ModelInvocationScopeDto? auditScope = null);

    /// <summary>
    /// 在指定审计作用域中执行异步操作。
    /// </summary>
    Task<T> RunWithAuditScopeAsync<T>(ModelInvocationScopeDto scope, Func<Task<T>> action);

    /// <summary>
    /// 在指定审计作用域中执行异步操作。
    /// </summary>
    Task RunWithAuditScopeAsync(ModelInvocationScopeDto scope, Func<Task> action);
}
