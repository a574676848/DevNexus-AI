using System.ComponentModel.DataAnnotations;

namespace DevNexus.ApiService.Domain;

public class ChatHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "User"; // User, Assistant, System

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ArtifactId { get; set; }
}
