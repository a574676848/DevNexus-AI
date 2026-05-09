using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services;

/// <summary>
/// 客户端更新事件服务实现。
/// </summary>
public class UpdateClientEventService : IUpdateClientEventService
{
    private readonly IUpdateClientEventRepository _eventRepository;
    private readonly ILogger<UpdateClientEventService> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateClientEventService(
        IUpdateClientEventRepository eventRepository,
        ILogger<UpdateClientEventService> logger)
    {
        _eventRepository = eventRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReportAsync(ReportUpdateClientEventRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InstallationId))
        {
            throw new InvalidOperationException("InstallationId 不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            throw new InvalidOperationException("EventType 不能为空");
        }

        var eventType = UpdateClientEventTypeExtensions.Parse(request.EventType);
        var result = string.IsNullOrWhiteSpace(request.Result)
            ? UpdateClientEventResult.Success
            : UpdateClientEventResultExtensions.Parse(request.Result);

        await _eventRepository.AddAsync(new UpdateClientEvent
        {
            InstallationId = request.InstallationId.Trim(),
            Platform = request.Platform.Trim().ToLowerInvariant(),
            Architecture = request.Architecture.Trim().ToLowerInvariant(),
            Channel = request.Channel.Trim().ToLowerInvariant(),
            CurrentVersion = request.CurrentVersion.Trim(),
            TargetVersion = request.TargetVersion.Trim(),
            RolloutId = request.RolloutId,
            ReleaseId = request.ReleaseId,
            ArtifactId = request.ArtifactId,
            EventType = eventType,
            Result = result,
            ErrorCode = string.IsNullOrWhiteSpace(request.ErrorCode) ? null : request.ErrorCode.Trim(),
            ErrorMessage = string.IsNullOrWhiteSpace(request.ErrorMessage) ? null : request.ErrorMessage.Trim()
        }, cancellationToken);

        _logger.LogInformation(
            "[UpdateClientEventService] 已处理更新事件上报 | InstallationId={InstallationId} EventType={EventType} Result={Result}",
            request.InstallationId,
            eventType.ToWireValue(),
            result.ToWireValue());
    }
}
