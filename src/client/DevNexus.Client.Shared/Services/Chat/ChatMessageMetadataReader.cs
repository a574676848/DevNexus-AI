using System.Text.Json;

namespace DevNexus.Client.Shared.Services.Chat;

internal static class ChatMessageMetadataReader
{
    public static Guid? GetGuid(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
        {
            if (Guid.TryParse(element.GetString(), out var guid))
            {
                return guid;
            }
        }
        else if (Guid.TryParse(value.ToString(), out var guid))
        {
            return guid;
        }

        return null;
    }

    public static string? GetString(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.ToString();
        }

        return value.ToString();
    }
}