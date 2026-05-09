using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 笔记供应商管理服务接口
/// </summary>
public interface INoteProviderManagementService
{
    /// <summary>
    /// 获取所有笔记供应商
    /// </summary>
    Task<IEnumerable<NoteProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据ID获取供应商
    /// </summary>
    Task<NoteProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据ProviderId获取供应商
    /// </summary>
    Task<NoteProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取默认供应商
    /// </summary>
    Task<NoteProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建新供应商
    /// </summary>
    Task<NoteProviderResponse> CreateProviderAsync(
        CreateNoteProviderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新供应商
    /// </summary>
    Task<NoteProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateNoteProviderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除供应商
    /// </summary>
    Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置默认供应商
    /// </summary>
    Task<bool> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证供应商配置
    /// </summary>
    Task<ValidateNoteProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试供应商连接（创建前测试）
    /// </summary>
    Task<ValidateNoteProviderResponse> TestProviderConnectionAsync(
        CreateNoteProviderRequest request,
        CancellationToken cancellationToken = default);
}
