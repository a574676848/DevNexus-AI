using DevNexus.Shared.DTOs.Swarm;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Swarm;

/// <summary>
/// SwarmMonitor - SignalR 事件处理
/// </summary>
public partial class SwarmMonitor
{
    private void SetupSignalRHandlers()
    {
        if (_hubConnection == null) return;

        _hubConnection.Reconnected += async (connectionId) =>
        {
            await _hubConnection.InvokeAsync("JoinSession", SessionId);
        };

        _hubConnection.On<ServerEvent>("ServerEventReceived", serverEvent =>
        {
            try
            {
                if (serverEvent.SessionId == Guid.Empty
                    || !Guid.TryParse(SessionId, out var parsedSessionId)
                    || serverEvent.SessionId != parsedSessionId)
                {
                    return;
                }

                InvokeAsync(() => HandleServerEvent(serverEvent));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing server event: {ex.Message}");
            }
        });
    }

    private void HandleServerEvent(ServerEvent serverEvent)
    {
        var data = serverEvent.Data is JsonElement json ? json : default;

        switch (serverEvent.EventType)
        {
            case ServerEventType.SwarmSessionStarted:
            case ServerEventType.SwarmStarted:
                ChatState.SetSwarmActive(serverEvent.SessionId, true);
                AddTimelineEntry("Swarm 已启动", "success", "system");
                _errorMessage = null;
                break;

            case ServerEventType.SwarmCompleted:
                ChatState.SetSwarmActive(serverEvent.SessionId, false);
                AddTimelineEntry("Swarm 已完成", "success", "system");
                _errorMessage = null;
                break;

            case ServerEventType.SwarmCancelled:
                ChatState.SetSwarmActive(serverEvent.SessionId, false);
                AddTimelineEntry("Swarm 已取消", "neutral", "system");
                _errorMessage = null;
                break;

            case ServerEventType.SwarmFailed:
                ChatState.SetSwarmActive(serverEvent.SessionId, false);
                var reason = TryGetElementString(data, "Reason", out var upperReason)
                    ? upperReason
                    : TryGetElementString(data, "reason", out var lowerReason)
                        ? lowerReason
                        : null;
                _errorMessage = IsCancellationReason(reason)
                    ? null
                    : string.IsNullOrWhiteSpace(reason)
                        ? "Swarm 编排失败。"
                        : $"Swarm 编排失败：{reason}";
                AddTimelineEntry("Swarm 已失败", IsCancellationReason(reason) ? "neutral" : "danger", "system");
                break;

            case ServerEventType.SwarmContextPackagesUpdated:
                if (data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty("Packages", out var packagesElement))
                {
                    var updatedPackages = JsonSerializer.Deserialize<List<ContextWorkPackageDto>>(packagesElement.GetRawText())
                        ?? new List<ContextWorkPackageDto>();
                    var changedPackageIds = GetChangedPackageIds(updatedPackages);
                    ContextPackages = updatedPackages;
                    foreach (var changedPackageId in changedPackageIds)
                    {
                        MarkPackageRecentlyChanged(changedPackageId);
                    }

                    EnsureSelectedPackageExists();
                    if (changedPackageIds.Count > 0)
                    {
                        AddTimelineEntry($"上下文工作包已更新，变更 {changedPackageIds.Count} 项", "info", "task");
                    }
                }
                break;

            case ServerEventType.SwarmAgentStatusChanged:
                var agentName = TryGetElementString(data, "AgentName", out var upperAgentName)
                    ? upperAgentName
                    : TryGetElementString(data, "Name", out var lowerAgentName)
                        ? lowerAgentName
                        : null;
                var status = TryGetElementString(data, "Status", out var upperStatus)
                    ? upperStatus
                    : null;
                var currentAction = TryGetElementString(data, "CurrentAction", out var upperCurrentAction)
                    ? upperCurrentAction
                    : null;
                if (!string.IsNullOrWhiteSpace(agentName))
                {
                    var agent = ActiveAgents.FirstOrDefault(a => a.Name == agentName);
                    if (agent != null)
                    {
                        var index = ActiveAgents.IndexOf(agent);
                        ActiveAgents[index] = agent with { Status = status ?? string.Empty, CurrentAction = currentAction ?? string.Empty };
                    }
                    else
                    {
                        ActiveAgents.Add(new AgentStatusDto
                        {
                            Name = agentName,
                            Status = status ?? string.Empty,
                            CurrentAction = currentAction ?? string.Empty
                        });
                    }
                    MarkAgentRecentlyChanged(agentName);
                    AddTimelineEntry($"智能体 {agentName} 状态更新", "info", "agent");
                }
                break;

            case ServerEventType.SwarmControlCommand:
                var command = TryGetElementString(data, "Command", out var upperCommand)
                    ? upperCommand
                    : null;
                if (command == "Paused")
                {
                    IsPaused = true;
                    AddTimelineEntry("Swarm 已暂停", "warning", "system");
                }
                else if (command == "Resumed")
                {
                    IsPaused = false;
                    AddTimelineEntry("Swarm 已继续", "success", "system");
                }
                else if (command == "Aborted")
                {
                    IsPaused = false;
                    AddTimelineEntry("Swarm 已中止", "danger", "system");
                }
                break;

            case ServerEventType.SwarmConfirmationRequested:
                var confirmationId = TryGetElementString(data, "ConfirmationId", out var upperConfirmationId)
                    ? upperConfirmationId
                    : TryGetElementString(data, "confirmationId", out var lowerConfirmationId)
                        ? lowerConfirmationId
                        : null;
                var operation = TryGetElementString(data, "Operation", out var upperOperation)
                    ? upperOperation
                    : TryGetElementString(data, "operation", out var lowerOperation)
                        ? lowerOperation
                        : "Unknown";
                var payload = TryGetElementString(data, "Payload", out var upperPayload)
                    ? upperPayload
                    : TryGetElementString(data, "payload", out var lowerPayload)
                        ? lowerPayload
                        : "{}";
                if (!string.IsNullOrWhiteSpace(confirmationId))
                {
                    _pendingConfirmations.RemoveAll(item => item.Id == confirmationId);
                    _pendingConfirmations.Add(new ConfirmReq(confirmationId, operation ?? "Unknown", payload ?? "{}"));
                    AddTimelineEntry("收到人工确认请求", "warning", "system");
                }
                break;
        }

        StateHasChanged();
    }

    private static bool TryGetElementString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        value = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        return true;
    }
}
