namespace DevNexus.Client.Services.Platform;

/// <summary>
/// MAUI 系统能力检测器实现
/// 负责检测剪贴板等平台支持情况
/// </summary>
public class SystemCapabilityDetector : ISystemCapabilityDetector
{
    public SystemCapabilityDetector()
    {
    }

    /// <inheritdoc />
    public async Task<bool> IsClipboardSupportedAsync()
    {
        try
        {
            // MAUI 剪贴板 API 在大多数平台上都可用
            return await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // 尝试写入测试数据来验证剪贴板是否可用
                Clipboard.SetTextAsync(string.Empty).GetAwaiter().GetResult();
                return true;
            });
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsClipboardReadSupportedAsync()
    {
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // 尝试读取剪贴板来验证是否支持读取
                var text = await Clipboard.Default.GetTextAsync();
                return text != null;
            });
        }
        catch (Exception)
        {
            return false;
        }
    }
}

