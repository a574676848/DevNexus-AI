using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 笔记供应商响应
/// </summary>
public class NoteProviderResponse
{
    public Guid Id { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public NoteProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime? LastValidatedAt { get; set; }
    public ValidationStatus ValidationStatus { get; set; }
    public string? ValidationError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建笔记供应商请求
/// </summary>
public class CreateNoteProviderRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public NoteProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int Priority { get; set; } = 100;
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// 更新笔记供应商请求
/// </summary>
public class UpdateNoteProviderRequest
{
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Endpoint { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDefault { get; set; }
    public int? Priority { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// 验证供应商响应
/// </summary>
public class ValidateNoteProviderResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ValidatedAt { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// 笔记内容
/// </summary>
public class NoteDto
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Visibility { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 创建笔记请求
/// </summary>
public class CreateNoteRequest
{
    public string Content { get; set; } = string.Empty;
    public string? Visibility { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// 更新笔记请求
/// </summary>
public class UpdateNoteRequest
{
    public string Content { get; set; } = string.Empty;
    public string? Visibility { get; set; }
}

/// <summary>
/// 搜索笔记请求
/// </summary>
public class SearchNotesRequest
{
    public string Query { get; set; } = string.Empty;
    public int PageSize { get; set; } = 10;
    public string? PageToken { get; set; }
}

/// <summary>
/// 搜索笔记响应
/// </summary>
public class SearchNotesResponse
{
    public List<NoteDto> Notes { get; set; } = new();
    public string? NextPageToken { get; set; }
}

