using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Shared.Services.UI;

public class ImagePreviewService : IImagePreviewService
{
    public string? CurrentImageUrl { get; private set; }
    public bool IsVisible { get; private set; }
    public event Action? OnStateChanged;

    public void Show(string imageUrl)
    {
        CurrentImageUrl = imageUrl;
        IsVisible = true;
        OnStateChanged?.Invoke();
    }

    public void Close()
    {
        IsVisible = false;
        CurrentImageUrl = null;
        OnStateChanged?.Invoke();
    }
}

