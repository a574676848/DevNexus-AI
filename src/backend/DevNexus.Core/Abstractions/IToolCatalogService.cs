using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 工具目录服务。
/// </summary>
public interface IToolCatalogService
{
    /// <summary>
    /// 获取稳定排序后的核心工具清单。
    /// </summary>
    IReadOnlyList<ToolCatalogItemDto> GetCoreTools();

    /// <summary>
    /// 获取稳定排序后的领域工具清单。
    /// </summary>
    IReadOnlyList<ToolCatalogItemDto> GetDomainTools();

    /// <summary>
    /// 获取所有工具。
    /// </summary>
    IReadOnlyList<ToolCatalogItemDto> GetAllTools();

    /// <summary>
    /// 计算工具 Schema 与排序指纹。
    /// </summary>
    string ComputeSchemaHash();
}
