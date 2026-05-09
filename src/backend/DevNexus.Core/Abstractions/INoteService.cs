using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 笔记服务抽象接口（支持多种笔记系统）
/// </summary>
public interface INoteService
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

    /// <summary>
    /// 验证连接
    /// </summary>
    Task<bool> ValidateConnectionAsync(
        CancellationToken cancellationToken = default);
}
