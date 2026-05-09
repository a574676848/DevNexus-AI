namespace DevNexus.Core.Abstractions;

/// <summary>
/// 将 Skill 源目录映射到当前用户可访问的运行时沙箱目录。
/// </summary>
public interface ISkillRuntimePathResolver
{
    /// <summary>
    /// 如果请求路径位于 Skill 内容根下，则返回当前用户可访问的镜像路径；否则返回 null。
    /// </summary>
    string? TryResolveAccessiblePath(Guid userId, string requestedPath);
}