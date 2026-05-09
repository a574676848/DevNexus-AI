using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions.Chat;

/// <summary>
/// 聊天输入区文件桥接服务。
/// </summary>
public interface IComposerFileBridgeService
{
    /// <summary>
    /// 当有文件资产被加入当前会话输入区时触发。
    /// </summary>
    event Func<Guid, FileAssetDto, Task>? FileAssetQueued;

    /// <summary>
    /// 将文件资产加入指定会话的输入区。
    /// </summary>
    Task QueueFileAssetAsync(Guid sessionId, FileAssetDto asset);
}