namespace DevNexus.Client.Shared.Abstractions;

public interface IImagePreviewService
{
    string? CurrentImageUrl { get; }
    bool IsVisible { get; }
    event Action? OnStateChanged;
    void Show(string imageUrl);
    void Close();
}

