using DevNexus.Domain.Models;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// Skill 注册中心 - 负责扫描、加载、缓存 Skill 元数据
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// 全局状态版本戳，发生任何加载/删除/重载时递增
    /// 用于下游（如 KernelService）惰性失效缓存
    /// </summary>
    long StateVersion { get; }

    /// <summary>
    /// 初始化：扫描所有 Skill 目录并加载元数据
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有已启用的 Skill 元数据（L1 层，低开销）
    /// </summary>
    IReadOnlyList<SkillMetadata> GetAllEnabled();

    /// <summary>
    /// 获取用户可用的 Skill（BuiltIn + Shared + 用户私有）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    IReadOnlyList<SkillMetadata> GetAvailableSkills(Guid userId);

    /// <summary>
    /// 按名称获取 Skill
    /// </summary>
    /// <param name="name">Skill 名称</param>
    SkillMetadata? GetByName(string name);

    /// <summary>
    /// 加载 SKILL.md 正文（L2 指令内容，首次读取后缓存）
    /// </summary>
    /// <param name="skillName">Skill 名称</param>
    /// <param name="ct">取消令牌</param>
    Task<string> LoadInstructionAsync(string skillName, CancellationToken ct = default);

    /// <summary>
    /// 热重载：重新扫描目录，更新缓存
    /// </summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 注册新 Skill（用户通过 API 上传时调用）
    /// </summary>
    /// <param name="skillDirectory">Skill 目录路径</param>
    /// <param name="scope">作用域</param>
    /// <param name="userId">用户 ID（User 级 Skill 时需要）</param>
    /// <param name="ct">取消令牌</param>
    Task<SkillMetadata> RegisterAsync(
        string skillDirectory,
        Shared.Enums.SkillScope scope,
        Guid? userId = null,
        CancellationToken ct = default);

    /// <summary>
    /// 解压并导入 Skill 压缩包（支持单个或多个 Skill）
    /// </summary>
    /// <param name="archiveStream">ZIP 文件流</param>
    /// <param name="scope">导入作用域 (Shared 或 User)</param>
    /// <param name="userId">当作用域为 User 时的用户 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<List<SkillMetadata>> ImportSkillArchiveAsync(
        Stream archiveStream,
        Shared.Enums.SkillScope scope,
        Guid? userId = null,
        CancellationToken ct = default);

    /// <summary>
    /// 移除 Skill
    /// </summary>
    /// <param name="skillName">Skill 名称</param>
    /// <param name="ct">取消令牌</param>
    Task RemoveAsync(string skillName, CancellationToken ct = default);
}
