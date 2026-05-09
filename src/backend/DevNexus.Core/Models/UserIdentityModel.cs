namespace DevNexus.Core.Models;

/// <summary>
/// Application-layer user identity model.
/// This decouples Core services from the ASP.NET Identity entity shape.
/// </summary>
public class UserIdentityModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginDeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
