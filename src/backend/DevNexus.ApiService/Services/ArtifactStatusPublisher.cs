using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.ApiService.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevNexus.ApiService.Services;

public class ArtifactStatusPublisher : IArtifactStatusPublisher
{
    public const string StatusCacheKeyPrefix = "artifact-parse-status";

    private readonly IHubContext<ArtifactHub> _hubContext;
    private readonly ILogger<ArtifactStatusPublisher> _logger;
    private readonly IDistributedCache _cache;

    public ArtifactStatusPublisher(
        IHubContext<ArtifactHub> hubContext,
        IDistributedCache cache,
        ILogger<ArtifactStatusPublisher> _logger)
    {
        _hubContext = hubContext;
        _cache = cache;
        this._logger = _logger;
    }

    public async Task PublishStatusAsync(string userId, string traceId, string status, SmartDocument? doc)
    {
        try
        {
            if (string.IsNullOrEmpty(userId)) return;

            var payload = new ArtifactStatusDto
            {
                TraceId = traceId,
                Status = ArtifactStatusConstants.Normalize(status),
                Success = ArtifactStatusConstants.IsCompleted(status),
                ErrorMessage = ArtifactStatusConstants.ExtractFailureMessage(status),
                SmartDocument = doc
            };

            var cacheKey = BuildCacheKey(userId, traceId);
            var json = JsonSerializer.Serialize(payload);
            await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
            });

            await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveArtifactStatus", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish artifact status via SignalR. UserId: {UserId}, TraceId: {TraceId}", userId, traceId);
        }
    }

    public static string BuildCacheKey(string userId, string traceId) =>
        $"{StatusCacheKeyPrefix}:{traceId}";
}
