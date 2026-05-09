using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Services.Notes;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services;

/// <summary>
/// 笔记服务工厂实现
/// </summary>
public class NoteServiceFactory : INoteServiceFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public NoteServiceFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    public INoteService CreateNoteService(NoteProvider provider, string accessToken)
    {
        return provider.Type switch
        {
            NoteProviderType.Memos => new MemosNoteService(
                provider.Endpoint,
                accessToken,
                _httpClientFactory,
                _loggerFactory.CreateLogger<MemosNoteService>()),
            // 未来可以扩展其他类型
            // NoteProviderType.Notion => new NotionNoteService(...),
            // NoteProviderType.Obsidian => new ObsidianNoteService(...),
            _ => throw new NotSupportedException($"Note provider type {provider.Type} is not supported")
        };
    }

    public INoteService CreateNoteService(NoteProviderResponse provider, string accessToken)
    {
        return provider.Type switch
        {
            NoteProviderType.Memos => new MemosNoteService(
                provider.Endpoint,
                accessToken,
                _httpClientFactory,
                _loggerFactory.CreateLogger<MemosNoteService>()),
            _ => throw new NotSupportedException($"Note provider type {provider.Type} is not supported")
        };
    }
}
