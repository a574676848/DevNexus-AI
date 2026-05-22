using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 工具目录服务。
/// </summary>
public interface IToolCatalogService
{
    /// <summary>
    /// 获取所有工具。
    /// </summary>
    IReadOnlyList<ToolCatalogItemDto> GetAllTools();

    /// <summary>
    /// 获取可直接暴露给普通会话的工具清单。
    /// </summary>
    IReadOnlyList<ToolCatalogItemDto> GetDirectTools();

    /// <summary>
    /// 获取需要按 Skill 或领域场景延迟暴露的工具清单。
    /// </summary>
    IReadOnlyList<ToolCatalogItemDto> GetDeferredTools();

    /// <summary>
    /// 将模型或 Skill 传入的工具名称解析为目录中的规范插件名。
    /// </summary>
    string? ResolvePluginName(string? requestedName);

    /// <summary>
    /// 计算工具 Schema 与排序指纹。
    /// </summary>
    string ComputeSchemaHash();
}
