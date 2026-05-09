using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 用户记忆服务接口
/// 管理用户的长期记忆（语义记忆 + 情境记忆）
/// </summary>
public interface IUserMemoryService
{
    #region 显性语义记忆 (UserFacts)

    /// <summary>
    /// 获取用户的高权重事实（用于 Prompt 注入）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="minConfidence">最低置信度阈值（默认 3）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户事实列表</returns>
    Task<List<UserFactDto>> GetUserFactsAsync(
        Guid userId,
        int minConfidence = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有事实（管理界面使用）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户事实列表</returns>
    Task<List<UserFactDto>> GetAllUserFactsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加或更新用户事实
    /// 如果存在语义相似的事实，则更新权重；否则新增
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="category">分类</param>
    /// <param name="content">内容</param>
    /// <param name="sourceSessionId">来源会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户事实DTO</returns>
    Task<UserFactDto> UpsertFactAsync(
        Guid userId,
        string category,
        string content,
        Guid? sourceSessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 固定/取消固定用户事实
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="factId">事实ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> TogglePinFactAsync(
        Guid userId,
        Guid factId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除用户事实
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="factId">事实ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteFactAsync(
        Guid userId,
        Guid factId,
        CancellationToken cancellationToken = default);

    #endregion

    #region 隐性情境记忆 (Episodic - Qdrant)

    /// <summary>
    /// 检索与当前问题相关的历史记忆
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="query">当前用户问题</param>
    /// <param name="topK">返回数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>相关历史记忆列表</returns>
    Task<List<EpisodicMemoryDto>> SearchEpisodicMemoriesAsync(
        Guid userId,
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 存储新的情境记忆（对话摘要）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="summary">对话摘要</param>
    /// <param name="tags">技术标签</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveEpisodicMemoryAsync(
        Guid userId,
        Guid sessionId,
        string summary,
        List<string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的记忆时间线
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>情境记忆列表</returns>
    Task<List<EpisodicMemoryDto>> GetMemoryTimelineAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    #endregion

    #region 记忆检索与注入

    /// <summary>
    /// 构建记忆上下文（用于 Prompt 注入）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="currentQuery">当前用户问题</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>记忆上下文</returns>
    Task<MemoryContext> BuildMemoryContextAsync(
        Guid userId,
        string currentQuery,
        CancellationToken cancellationToken = default);

    #endregion
}
