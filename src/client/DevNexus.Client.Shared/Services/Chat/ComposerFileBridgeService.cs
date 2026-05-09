using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// 聊天输入区文件桥接服务实现。
/// </summary>
public class ComposerFileBridgeService : IComposerFileBridgeService
{
    /// <inheritdoc />
    public event Func<Guid, FileAssetDto, Task>? FileAssetQueued;

    /// <inheritdoc />
    public async Task QueueFileAssetAsync(Guid sessionId, FileAssetDto asset)
    {
        if (FileAssetQueued == null)
        {
            return;
        }

        var handlers = FileAssetQueued.GetInvocationList()
            .Cast<Func<Guid, FileAssetDto, Task>>()
            .ToArray();

        foreach (var handler in handlers)
        {
            await handler(sessionId, asset);
        }
    }
}