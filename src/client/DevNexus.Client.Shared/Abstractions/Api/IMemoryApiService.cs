using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 记忆管理 API 服务接口
/// </summary>
public interface IMemoryApiService
{
    /// <summary>
    /// 获取当前用户的画像事实列表
    /// </summary>
    Task<List<UserFactDto>> GetUserFactsAsync();

    /// <summary>
    /// 添加用户画像事实
    /// </summary>
    /// <param name="request">添加请求</param>
    Task<UserFactDto> AddUserFactAsync(AddUserFactRequest request);

    /// <summary>
    /// 删除用户画像事实
    /// </summary>
    /// <param name="factId">事实ID</param>
    Task DeleteUserFactAsync(Guid factId);

    /// <summary>
    /// 切换事实的固定状态
    /// </summary>
    /// <param name="factId">事实ID</param>
    Task TogglePinFactAsync(Guid factId);

    /// <summary>
    /// 获取情境记忆时间线
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    Task<List<EpisodicMemoryDto>> GetMemoryTimelineAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// 搜索相关的情境记忆
    /// </summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="topK">返回数量（默认20条）</param>
    Task<List<EpisodicMemoryDto>> SearchMemoriesAsync(string query, int topK = 20);

    // ==============================================
    // 智能体系统经验管理 (System 1/2 Shared Memory)
    // ==============================================

    Task<PagedResult<SystemExperienceDto>> GetSystemExperiencesAsync(DevNexus.Shared.Enums.ExperienceType? type, string? search, int page = 1, int pageSize = 20);
    Task TogglePinSystemExperienceAsync(Guid id);
    Task UpdateSystemExperienceScoreAsync(Guid id, double score);
    Task DeleteSystemExperienceAsync(Guid id);
}

