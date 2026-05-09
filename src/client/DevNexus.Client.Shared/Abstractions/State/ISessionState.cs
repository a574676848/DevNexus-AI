using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 会话状态管理接口
/// </summary>
public interface ISessionState
{
    /// <summary>
    /// 状态变更事件
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// 会话列表 (只读)
    /// </summary>
    IReadOnlyList<ChatSessionDto> Sessions { get; }

    /// <summary>
    /// 当前选中的会话
    /// </summary>
    ChatSessionDto? CurrentSession { get; }

    /// <summary>
    /// 是否正在加载
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// 设置会话列表
    /// </summary>
    void SetSessions(IEnumerable<ChatSessionDto> sessions);

    /// <summary>
    /// 添加会话
    /// </summary>
    void AddSession(ChatSessionDto session);

    /// <summary>
    /// 更新会话
    /// </summary>
    void UpdateSession(ChatSessionDto session);

    /// <summary>
    /// 删除会话
    /// </summary>
    void RemoveSession(Guid sessionId);

    /// <summary>
    /// 设置当前会话
    /// </summary>
    void SetCurrentSession(Guid sessionId);

    /// <summary>
    /// 设置加载状态
    /// </summary>
    void SetLoading(bool loading);

    /// <summary>
    /// 清空所有会话
    /// </summary>
    void Clear();
}

