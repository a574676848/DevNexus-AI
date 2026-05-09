using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 笔记供应商 API 服务接口
/// </summary>
public interface INoteProviderApiService
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
    /// 测试供应商连接
    /// </summary>
    Task<ValidateNoteProviderResponse> TestProviderConnectionAsync(
        CreateNoteProviderRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 笔记操作 API 服务接口
/// </summary>
public interface INoteApiService
{
    /// <summary>
    /// 搜索笔记
    /// </summary>
    Task<SearchNotesResponse> SearchNotesAsync(
        SearchNotesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建笔记
    /// </summary>
    Task<NoteDto> CreateNoteAsync(
        CreateNoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新笔记
    /// </summary>
    Task<NoteDto> UpdateNoteAsync(
        string noteId,
        UpdateNoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除笔记
    /// </summary>
    Task<bool> DeleteNoteAsync(
        string noteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单个笔记
    /// </summary>
    Task<NoteDto?> GetNoteAsync(
        string noteId,
        CancellationToken cancellationToken = default);
}

