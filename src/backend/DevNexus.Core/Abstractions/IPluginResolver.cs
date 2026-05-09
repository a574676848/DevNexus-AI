namespace DevNexus.Core.Abstractions;

/// <summary>
/// Plugin 解析器 - 从 DI 容器按名称解析已注册的 Semantic Kernel Plugin
/// </summary>
public interface IPluginResolver
{
    /// <summary>
    /// 按名称获取 Plugin 实例
    /// </summary>
    /// <param name="pluginName">Plugin 注册名</param>
    /// <param name="sessionId">会话 ID（有状态 Plugin 需要）</param>
    /// <param name="userId">用户 ID（有状态 Plugin 需要）</param>
    /// <returns>Plugin 实例，不存在则返回 null</returns>
    object? Resolve(string pluginName, Guid? sessionId = null, Guid? userId = null);

    /// <summary>
    /// 获取所有可用 Plugin 名称
    /// </summary>
    IReadOnlyList<string> GetAvailablePluginNames();
}
