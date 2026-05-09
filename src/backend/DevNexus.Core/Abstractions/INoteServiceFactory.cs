using DevNexus.Shared.DTOs;
using DevNexus.Domain.Entities;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 笔记服务工厂接口
/// </summary>
public interface INoteServiceFactory
{
    /// <summary>
    /// 根据供应商和用户凭证创建笔记服务实例
    /// </summary>
    /// <param name="provider">笔记供应商配置（包含endpoint等全局配置）</param>
    /// <param name="accessToken">用户的解密后的访问令牌</param>
    INoteService CreateNoteService(NoteProvider provider, string accessToken);

    /// <summary>
    /// 根据供应商响应模型和用户凭证创建笔记服务实例。
    /// </summary>
    INoteService CreateNoteService(NoteProviderResponse provider, string accessToken);
}
