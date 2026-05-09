using System.Net.Http.Json;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 笔记供应商管理部分
/// </summary>
public partial class ApiService : INoteProviderApiService, INoteApiService
{
    #region 笔记供应商管理

    public async Task<IEnumerable<NoteProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note?includeDisabled={includeDisabled}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<NoteProviderResponse>>(cancellationToken: cancellationToken)
            ?? Enumerable.Empty<NoteProviderResponse>();
    }

    public async Task<NoteProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note/{id}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
            return null;
            
        return await response.Content.ReadFromJsonAsync<NoteProviderResponse>(cancellationToken: cancellationToken);
    }

    public async Task<NoteProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note/default";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
            return null;
            
        return await response.Content.ReadFromJsonAsync<NoteProviderResponse>(cancellationToken: cancellationToken);
    }

    public async Task<NoteProviderResponse> CreateProviderAsync(
        CreateNoteProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteProviderResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<NoteProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateNoteProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note/{id}";
        var response = await _httpClient.PutAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteProviderResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note/{id}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note/{id}/set-default";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<ValidateNoteProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note/{id}/validate";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ValidateNoteProviderResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<ValidateNoteProviderResponse> TestProviderConnectionAsync(
        CreateNoteProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/providers/note/test";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ValidateNoteProviderResponse>(cancellationToken: cancellationToken))!;
    }

    #endregion

    #region 笔记操作

    public async Task<SearchNotesResponse> SearchNotesAsync(
        SearchNotesRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/notes/search";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchNotesResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<NoteDto> CreateNoteAsync(
        CreateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/notes";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<NoteDto> UpdateNoteAsync(
        string noteId,
        UpdateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/notes/{noteId}";
        var response = await _httpClient.PutAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<bool> DeleteNoteAsync(
        string noteId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/notes/{noteId}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<NoteDto?> GetNoteAsync(
        string noteId,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/notes/{noteId}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken: cancellationToken);
    }

    #endregion
}
