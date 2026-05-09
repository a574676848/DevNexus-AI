using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// 笔记插件 (Semantic Kernel Plugin)
/// 支持通过笔记服务（如 Memos）检索、创建和更新笔记
/// </summary>
public class NotePlugin : SessionContextPluginBase
{
    /// <summary>
    /// 笔记服务上下文
    /// </summary>
    private sealed class NotePluginContext
    {
        /// <summary>
        /// 用户 ID
        /// </summary>
        public Guid UserId { get; init; }

        /// <summary>
        /// 集成 ID
        /// </summary>
        public Guid IntegrationId { get; init; }

        /// <summary>
        /// 笔记服务实例
        /// </summary>
        public required INoteService NoteService { get; init; }
    }

    private readonly IUserIntegrationService _integrationService;
    private readonly INoteProviderManagementService _noteProviderManagementService;
    private readonly INoteServiceFactory _noteServiceFactory;
    private readonly ILogger<NotePlugin> _logger;

    public NotePlugin(
        IUserIntegrationService integrationService,
        INoteProviderManagementService noteProviderManagementService,
        INoteServiceFactory noteServiceFactory,
        ILogger<NotePlugin> logger)
        : base(logger)
    {
        _integrationService = integrationService;
        _noteProviderManagementService = noteProviderManagementService;
        _noteServiceFactory = noteServiceFactory;
        _logger = logger;
    }

    /// <summary>
    /// 设置上下文 (每次请求前调用)
    /// </summary>
    public void SetContext(Guid sessionId, Guid userId)
    {
        SetSessionContext(sessionId, userId, nameof(NotePlugin));
    }

    /// <summary>
    /// 搜索笔记
    /// </summary>
    [KernelFunction, Description("搜索笔记系统中的内容，返回相关笔记列表。")]
    public async Task<string> SearchNotesAsync(
        [Description("搜索关键词")] string query,
        [Description("返回最大数量")] int count = 5)
    {
        try
        {
            var userId = GetSessionUserId();

            _logger.LogDebug("[NotePlugin] SearchNotesAsync called | Query={Query}, Count={Count}, UserId={UserId}",
                query, count, userId);

            // 检查上下文是否已设置
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("[NotePlugin] Context not set. Cannot search notes.");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "系统内部错误：用户上下文未初始化。请确保在聊天会话中调用此功能。"
                });
            }

            // 获取用户的默认笔记集成
            var integration = await _integrationService.GetDefaultIntegrationAsync(userId, IntegrationType.NoteSystem);
            if (integration == null)
            {
                _logger.LogWarning("[NotePlugin] No note integration found for UserId={UserId}", userId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "未配置笔记系统集成。请在个人设置中添加笔记系统集成。"
                });
            }

            // 检查集成是否关联了Provider
            if (!integration.ProviderId.HasValue)
            {
                _logger.LogWarning("[NotePlugin] Integration has no provider | IntegrationId={IntegrationId}", integration.Id);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "笔记集成配置不完整，请重新配置。"
                });
            }

            var context = await CreateNotePluginContextAsync(userId, integration);

            // 搜索笔记
            var request = new SearchNotesRequest
            {
                Query = query,
                PageSize = count
            };

            var response = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.SearchNotesAsync(request));

            _logger.LogInformation("[NotePlugin] Search completed | Found {Count} notes", response.Notes.Count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = response.Notes.Count,
                notes = response.Notes.Select(n => new
                {
                    id = n.Id,
                    content = n.Content.Length > 200 ? n.Content.Substring(0, 200) + "..." : n.Content,
                    createdAt = n.CreatedAt,
                    visibility = n.Visibility
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotePlugin] Error searching notes");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"搜索笔记失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 创建笔记
    /// </summary>
    [KernelFunction, Description("在笔记系统中创建一条新笔记。")]
    public async Task<string> CreateNoteAsync(
        [Description("笔记内容")] string content,
        [Description("可见性 (PUBLIC, PRIVATE, PROTECTED)")] string visibility = "PRIVATE")
    {
        try
        {
            var userId = GetSessionUserId();

            _logger.LogDebug("[NotePlugin] CreateNoteAsync called | Content length={Length}, Visibility={Visibility}, UserId={UserId}",
                content.Length, visibility, userId);

            // 检查上下文是否已设置
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("[NotePlugin] Context not set. Cannot create note.");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "系统内部错误：用户上下文未初始化。请确保在聊天会话中调用此功能。"
                });
            }

            // 获取用户的默认笔记集成
            var integration = await _integrationService.GetDefaultIntegrationAsync(userId, IntegrationType.NoteSystem);
            if (integration == null)
            {
                _logger.LogWarning("[NotePlugin] No note integration found for UserId={UserId}", userId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "未配置笔记系统集成。请在个人设置中添加笔记系统集成。"
                });
            }

            // 检查集成是否关联了Provider
            if (!integration.ProviderId.HasValue)
            {
                _logger.LogWarning("[NotePlugin] Integration has no provider | IntegrationId={IntegrationId}", integration.Id);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "笔记集成配置不完整，请重新配置。"
                });
            }

            var context = await CreateNotePluginContextAsync(userId, integration);

            // 创建笔记
            var request = new CreateNoteRequest
            {
                Content = content,
                Visibility = visibility
            };

            var note = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.CreateNoteAsync(request));

            _logger.LogInformation("[NotePlugin] Note created successfully | NoteId={NoteId}", note.Id);

            return JsonSerializer.Serialize(new
            {
                success = true,
                note = new
                {
                    id = note.Id,
                    content = note.Content.Length > 200 ? note.Content.Substring(0, 200) + "..." : note.Content,
                    createdAt = note.CreatedAt,
                    visibility = note.Visibility
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotePlugin] Error creating note");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"创建笔记失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 创建带附件引用的笔记（WBS-003：Memos 文件 URL 集成）
    /// 直接引用 DevNexus 存储的文件 URL，无需额外上传到 Memos
    /// 支持所有文件类型（图片、文档、代码等）
    /// </summary>
    [KernelFunction, Description("创建带附件引用的笔记（支持图片、文档、代码等）")]
    public async Task<string> CreateNoteWithAttachmentsAsync(
        [Description("笔记主要内容")] string content,
        [Description("附件 URL 列表，多个 URL 用逗号分隔。例如：url1,url2,url3")] string attachmentUrls = "",
        [Description("可见性 (PUBLIC, PRIVATE, PROTECTED)")] string visibility = "PRIVATE")
    {
        try
        {
            var userId = GetSessionUserId();

            _logger.LogDebug("[NotePlugin] CreateNoteWithAttachmentsAsync called | Content length={Length}, AttachmentCount={Count}, UserId={UserId}",
                content.Length,
                string.IsNullOrEmpty(attachmentUrls) ? 0 : attachmentUrls.Split(',').Length,
                userId);

            // 检查上下文是否已设置
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("[NotePlugin] Context not set. Cannot create note with attachments.");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "系统内部错误：用户上下文未初始化。请确保在聊天会话中调用此功能。"
                });
            }

            // 获取用户的默认笔记集成
            var integration = await _integrationService.GetDefaultIntegrationAsync(userId, IntegrationType.NoteSystem);
            if (integration == null)
            {
                _logger.LogWarning("[NotePlugin] No note integration found for UserId={UserId}", userId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "未配置笔记系统集成。请在个人设置中添加笔记系统集成。"
                });
            }

            // 获取Provider实体
            if (!integration.ProviderId.HasValue)
            {
                _logger.LogWarning("[NotePlugin] IntegrationProviderId is null");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "笔记系统配置不完整，请检查集成设置。"
                });
            }

            var context = await CreateNotePluginContextAsync(userId, integration);

            // 解析并格式化附件 URL
            var attachmentMarkdownLinks = new List<string>();
            if (!string.IsNullOrEmpty(attachmentUrls))
            {
                var urls = attachmentUrls.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var url in urls)
                {
                    var cleanUrl = url.Trim();
                    if (string.IsNullOrEmpty(cleanUrl)) continue;

                    // 根据文件类型生成对应的 Markdown 语法
                    var markdown = IsImageUrl(cleanUrl)
                        ? $"![Attachment]({cleanUrl})"  // 图片：使用 ![](url) 嵌入
                        : $"[文件]({cleanUrl})";         // 其他：使用 [](url) 链接

                    attachmentMarkdownLinks.Add(markdown);
                    _logger.LogDebug("[NotePlugin] Formatted attachment URL | Type={Type} Url={Url}",
                        IsImageUrl(cleanUrl) ? "Image" : "File", cleanUrl);
                }
            }

            // 构建完整笔记内容
            // 格式：用户内容 + 分隔符 + 附件链接
            var fullContent = content;
            if (attachmentMarkdownLinks.Any())
            {
                fullContent += "\n\n---\n\n**附件：**\n\n" + string.Join("\n\n", attachmentMarkdownLinks);
            }

            // 创建笔记
            var request = new CreateNoteRequest
            {
                Content = fullContent,
                Visibility = visibility
            };

            var note = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.CreateNoteAsync(request));

            _logger.LogInformation("[NotePlugin] Note with attachments created successfully | NoteId={NoteId} AttachmentCount={Count}",
                note.Id, attachmentUrls?.Split(',').Length ?? 0);

            return JsonSerializer.Serialize(new
            {
                success = true,
                noteId = note.Id,
                noteUrl = $"/notes/{note.Id}",  // 返回笔记 URL（根据系统配置调整）
                message = "笔记创建成功，附件已作为链接引用"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotePlugin] Error creating note with attachments");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"创建笔记失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 更新笔记
    /// </summary>
    [KernelFunction, Description("更新现有笔记的内容和可见性。")]
    public async Task<string> UpdateNoteAsync(
        [Description("笔记ID，格式为 'memos/xxx'")] string noteId,
        [Description("新的笔记内容")] string content,
        [Description("可见性 (PUBLIC, PRIVATE, PROTECTED)")] string visibility = "PRIVATE")
    {
        try
        {
            var userId = GetSessionUserId();

            _logger.LogDebug("[NotePlugin] UpdateNoteAsync called | NoteId={NoteId}, Content length={Length}, Visibility={Visibility}, UserId={UserId}",
                noteId, content.Length, visibility, userId);

            // 检查上下文是否已设置
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("[NotePlugin] Context not set. Cannot update note.");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "系统内部错误：用户上下文未初始化。请确保在聊天会话中调用此功能。"
                });
            }

            // 验证 noteId 格式
            if (string.IsNullOrEmpty(noteId) || !noteId.StartsWith("memos/"))
            {
                _logger.LogWarning("[NotePlugin] Invalid noteId format | NoteId={NoteId}", noteId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "无效的笔记ID格式。笔记ID应该类似于 'memos/123'。"
                });
            }

            // 获取用户的默认笔记集成
            var integration = await _integrationService.GetDefaultIntegrationAsync(userId, IntegrationType.NoteSystem);
            if (integration == null)
            {
                _logger.LogWarning("[NotePlugin] No note integration found for UserId={UserId}", userId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "未配置笔记系统集成。请在个人设置中添加笔记系统集成。"
                });
            }

            // 检查集成是否关联了Provider
            if (!integration.ProviderId.HasValue)
            {
                _logger.LogWarning("[NotePlugin] Integration has no provider | IntegrationId={IntegrationId}", integration.Id);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "笔记集成配置不完整，请重新配置。"
                });
            }

            var context = await CreateNotePluginContextAsync(userId, integration);

            // 更新笔记
            var request = new UpdateNoteRequest
            {
                Content = content,
                Visibility = visibility
            };

            var note = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.UpdateNoteAsync(noteId, request));

            _logger.LogInformation("[NotePlugin] Note updated successfully | NoteId={NoteId}", note.Id);

            return JsonSerializer.Serialize(new
            {
                success = true,
                note = new
                {
                    id = note.Id,
                    content = note.Content.Length > 200 ? note.Content.Substring(0, 200) + "..." : note.Content,
                    updatedAt = note.UpdatedAt,
                    visibility = note.Visibility
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotePlugin] Error updating note | NoteId={NoteId}", noteId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"更新笔记失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 删除笔记
    /// </summary>
    [KernelFunction, Description("删除指定的笔记。")]
    public async Task<string> DeleteNoteAsync(
        [Description("笔记ID，格式为 'memos/xxx'")] string noteId)
    {
        try
        {
            var userId = GetSessionUserId();

            _logger.LogDebug("[NotePlugin] DeleteNoteAsync called | NoteId={NoteId}, UserId={UserId}",
                noteId, userId);

            // 检查上下文是否已设置
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("[NotePlugin] Context not set. Cannot delete note.");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "系统内部错误：用户上下文未初始化。请确保在聊天会话中调用此功能。"
                });
            }

            // 验证 noteId 格式
            if (string.IsNullOrEmpty(noteId) || !noteId.StartsWith("memos/"))
            {
                _logger.LogWarning("[NotePlugin] Invalid noteId format | NoteId={NoteId}", noteId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "无效的笔记ID格式。笔记ID应该类似于 'memos/123'。"
                });
            }

            // 获取用户的默认笔记集成
            var integration = await _integrationService.GetDefaultIntegrationAsync(userId, IntegrationType.NoteSystem);
            if (integration == null)
            {
                _logger.LogWarning("[NotePlugin] No note integration found for UserId={UserId}", userId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "未配置笔记系统集成。请在个人设置中添加笔记系统集成。"
                });
            }

            // 检查集成是否关联了Provider
            if (!integration.ProviderId.HasValue)
            {
                _logger.LogWarning("[NotePlugin] Integration has no provider | IntegrationId={IntegrationId}", integration.Id);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "笔记集成配置不完整，请重新配置。"
                });
            }

            var context = await CreateNotePluginContextAsync(userId, integration);

            // 删除笔记
            var result = await ExecuteWithUsageTrackingAsync(
                context,
                noteService => noteService.DeleteNoteAsync(noteId));

            if (result)
            {
                _logger.LogInformation("[NotePlugin] Note deleted successfully | NoteId={NoteId}", noteId);
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "笔记已成功删除。"
                });
            }
            else
            {
                _logger.LogWarning("[NotePlugin] Failed to delete note | NoteId={NoteId}", noteId);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "删除笔记失败，请检查笔记ID是否正确。"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NotePlugin] Error deleting note | NoteId={NoteId}", noteId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"删除笔记失败: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// 判断 URL 是否指向图片文件
    /// </summary>
    private static bool IsImageUrl(string url)
    {
        var path = new Uri(url).AbsolutePath.ToLowerInvariant();
        return path.EndsWith(".png") || path.EndsWith(".jpg")
            || path.EndsWith(".jpeg") || path.EndsWith(".gif")
            || path.EndsWith(".webp") || path.EndsWith(".bmp")
            || path.EndsWith(".svg");
    }

    /// <summary>
    /// 创建笔记插件执行上下文
    /// </summary>
    private async Task<NotePluginContext> CreateNotePluginContextAsync(Guid userId, UserIntegration integration)
    {
        var accessToken = await _integrationService.GetDecryptedCredentialAsync(userId, integration.Id);
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("[NotePlugin] Failed to get credential for IntegrationId={IntegrationId}", integration.Id);
            throw new InvalidOperationException("无法获取笔记系统凭证，请检查配置。");
        }

        if (!integration.ProviderId.HasValue)
        {
            _logger.LogWarning("[NotePlugin] Integration has no provider | IntegrationId={IntegrationId}", integration.Id);
            throw new InvalidOperationException("笔记集成配置不完整，请重新配置。");
        }

        var provider = await _noteProviderManagementService.GetProviderByIdAsync(integration.ProviderId.Value);
        if (provider == null)
        {
            _logger.LogWarning("[NotePlugin] Provider not found | ProviderId={ProviderId}", integration.ProviderId.Value);
            throw new InvalidOperationException("笔记系统供应商不存在，请联系管理员。");
        }

        return new NotePluginContext
        {
            UserId = userId,
            IntegrationId = integration.Id,
            NoteService = _noteServiceFactory.CreateNoteService(provider, accessToken)
        };
    }

    /// <summary>
    /// 在笔记调用成功后记录集成使用情况
    /// </summary>
    private async Task<T> ExecuteWithUsageTrackingAsync<T>(
        NotePluginContext context,
        Func<INoteService, Task<T>> action)
    {
        var result = await action(context.NoteService);

        // 只有真实调用成功后才累计使用次数，避免仅解析配置就污染统计口径。
        await _integrationService.RecordUsageAsync(context.UserId, context.IntegrationId);
        return result;
    }
}
