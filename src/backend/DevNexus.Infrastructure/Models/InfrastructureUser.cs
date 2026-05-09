using DevNexus.Domain.Entities.Base;
using Microsoft.AspNetCore.Identity;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// ASP.NET Identity persistence model for users.
/// This stays in Infrastructure so Domain no longer depends on Identity.
/// </summary>
public class InfrastructureUser : IdentityUser<Guid>, IAuditableEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginDeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
