namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 系统能力检测器接口
/// </summary>
public interface ISystemCapabilityDetector
{
    Task<bool> IsClipboardSupportedAsync();
    Task<bool> IsClipboardReadSupportedAsync();
}

