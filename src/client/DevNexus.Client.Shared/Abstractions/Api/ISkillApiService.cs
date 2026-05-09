using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// Skill 管理 API 服务接口
/// </summary>
public interface ISkillApiService
{
    /// <summary>
    /// 获取当前用户可用的所有 Skill
    /// </summary>
    Task<List<SkillDto>> GetAvailableSkillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 Skill 详细指令内容
    /// </summary>
    Task<SkillDetailResponse?> GetSkillDetailAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试 Skill 匹配
    /// </summary>
    Task<List<SkillMatchTestResult>> TestMatchAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 热重载所有 Skill
    /// </summary>
    Task<SkillReloadResponse> ReloadSkillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 上传 Skill 压缩包 (.zip)，支持批量导入
    /// </summary>
    Task<List<SkillDto>> UploadSkillAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除 Skill
    /// </summary>
    Task DeleteSkillAsync(string name, CancellationToken cancellationToken = default);
}

public record SkillDetailResponse(SkillDto Skill, string Instruction);
public record SkillMatchTestResult(string SkillName, double Score, string Method, string Description);
public record SkillReloadResponse(string Message, int Count, List<string> Skills);

