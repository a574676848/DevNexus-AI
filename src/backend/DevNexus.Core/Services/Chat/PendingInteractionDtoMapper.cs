using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services;

/// <summary>
/// 挂起交互 DTO 映射器。
/// </summary>
internal static class PendingInteractionDtoMapper
{
    /// <summary>
    /// 将实体映射为 DTO。
    /// </summary>
    public static PendingInteractionDto ToDto(PendingInteraction interaction)
    {
        return new PendingInteractionDto
        {
            Id = interaction.Id,
            SessionId = interaction.SessionId,
            Kind = interaction.Kind.ToWireValue(),
            Status = interaction.Status.ToWireValue(),
            Title = interaction.Title,
            Description = interaction.Description,
            SourceTool = interaction.SourceTool,
            SuggestedAction = interaction.SuggestedAction?.ToWireValue(),
            RequestedFields = ParseRequestedFields(interaction.RequestedData),
            ExpiresAt = interaction.ExpiresAt,
            RetryToken = interaction.RetryToken
        };
    }

    private static List<PendingInteractionFieldDto> ParseRequestedFields(Dictionary<string, object>? requestedData)
    {
        if (requestedData == null || !requestedData.TryGetValue("fields", out var fieldsObj) || fieldsObj == null)
        {
            return new List<PendingInteractionFieldDto>();
        }

        if (fieldsObj is not IEnumerable<object> fields)
        {
            return new List<PendingInteractionFieldDto>();
        }

        var result = new List<PendingInteractionFieldDto>();
        foreach (var field in fields.OfType<Dictionary<string, object>>())
        {
            result.Add(new PendingInteractionFieldDto
            {
                Key = field.TryGetValue("key", out var key) ? key?.ToString() ?? string.Empty : string.Empty,
                Type = field.TryGetValue("type", out var type) ? type?.ToString() ?? string.Empty : string.Empty,
                Label = field.TryGetValue("label", out var label) ? label?.ToString() ?? string.Empty : string.Empty,
                Required = field.TryGetValue("required", out var required)
                    && bool.TryParse(required?.ToString(), out var parsedRequired)
                    && parsedRequired,
                Placeholder = field.TryGetValue("placeholder", out var placeholder) ? placeholder?.ToString() : null
            });
        }

        return result;
    }
}
