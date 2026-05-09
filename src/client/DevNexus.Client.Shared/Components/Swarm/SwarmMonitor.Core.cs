using DevNexus.Shared.DTOs.Swarm;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Swarm;

/// <summary>
/// SwarmMonitor - 核心状态管理和辅助方法。
/// </summary>
public partial class SwarmMonitor
{
    private void EnsureSelectedPackageExists()
    {
        if (ContextPackages.Count == 0)
        {
            _selectedPackageId = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedPackageId) &&
            ContextPackages.Any(package => string.Equals(package.Id, _selectedPackageId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var preferredPackage = ContextPackages.FirstOrDefault(package =>
                string.Equals(package.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            ?? ContextPackages.LastOrDefault();

        _selectedPackageId = preferredPackage?.Id;
    }

    private void AddTimelineEntry(string text, string tone, string category)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _timelineEntries.Insert(0, new TimelineEntry(text, tone, category, DateTime.UtcNow));
        if (_timelineEntries.Count > 8)
        {
            _timelineEntries.RemoveRange(8, _timelineEntries.Count - 8);
        }
    }

    private void MarkAgentRecentlyChanged(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return;
        }

        _recentlyChangedAgentNames.Add(agentName);
        _ = ClearAgentRecentFlagAsync(agentName);
    }

    private void MarkPackageRecentlyChanged(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return;
        }

        _recentlyChangedPackageIds.Add(packageId);
        _ = ClearPackageRecentFlagAsync(packageId);
    }

    private async Task ClearAgentRecentFlagAsync(string agentName)
    {
        await Task.Delay(900);
        if (_isDisposed)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _recentlyChangedAgentNames.Remove(agentName);
            StateHasChanged();
        });
    }

    private async Task ClearPackageRecentFlagAsync(string packageId)
    {
        await Task.Delay(900);
        if (_isDisposed)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _recentlyChangedPackageIds.Remove(packageId);
            StateHasChanged();
        });
    }

    private List<string> GetChangedPackageIds(List<ContextWorkPackageDto> updatedPackages)
    {
        var changedPackageIds = new List<string>();

        foreach (var updatedPackage in updatedPackages)
        {
            var currentPackage = ContextPackages.FirstOrDefault(package =>
                string.Equals(package.Id, updatedPackage.Id, StringComparison.OrdinalIgnoreCase));
            if (currentPackage == null || HasPackageChanged(currentPackage, updatedPackage))
            {
                changedPackageIds.Add(updatedPackage.Id);
            }
        }

        return changedPackageIds;
    }

    private static bool HasPackageChanged(ContextWorkPackageDto currentPackage, ContextWorkPackageDto updatedPackage)
    {
        if (!string.Equals(currentPackage.Title, updatedPackage.Title, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(currentPackage.Objective, updatedPackage.Objective, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(currentPackage.ContextType, updatedPackage.ContextType, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(currentPackage.Status, updatedPackage.Status, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(currentPackage.ExecutionStrategy, updatedPackage.ExecutionStrategy, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(currentPackage.Result, updatedPackage.Result, StringComparison.Ordinal))
        {
            return true;
        }

        return !currentPackage.Dependencies.SequenceEqual(updatedPackage.Dependencies);
    }

    private bool IsRecentlyChangedAgent(string agentName)
    {
        return _recentlyChangedAgentNames.Contains(agentName);
    }

    private bool IsRecentlyChangedPackage(string packageId)
    {
        return _recentlyChangedPackageIds.Contains(packageId);
    }

    private IReadOnlyList<TimelineEntry> GetFilteredTimelineEntries()
    {
        return _timelineEntries
            .Where(entry => IsTimelineFilterActive("all") || string.Equals(entry.Category, _timelineFilter, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToList();
    }

    private bool IsTimelineFilterActive(string filter)
    {
        return string.Equals(_timelineFilter, filter, StringComparison.OrdinalIgnoreCase);
    }

    private void SetTimelineFilter(string filter)
    {
        _timelineFilter = filter;
        StateHasChanged();
    }

    private void SetTimelineFilterAll() => SetTimelineFilter("all");
    private void SetTimelineFilterSystem() => SetTimelineFilter("system");
    private void SetTimelineFilterPackage() => SetTimelineFilter("task");
    private void SetTimelineFilterAgent() => SetTimelineFilter("agent");

    private IReadOnlyList<ContextWorkPackageDto> GetFilteredPackages()
    {
        return ContextPackages
            .Where(package => _packageFilter switch
            {
                "failed" => IsFailureStatus(package.Status),
                "active" => IsExecutingStatus(package.Status) || IsEvaluatingStatus(package.Status),
                "completed" => IsTerminalStatus(package.Status),
                _ => true
            })
            .ToList();
    }

    private bool IsPackageFilterActive(string filter)
    {
        return string.Equals(_packageFilter, filter, StringComparison.OrdinalIgnoreCase);
    }

    private void SetPackageFilter(string filter)
    {
        _packageFilter = filter;
        StateHasChanged();
    }

    private void SetPackageFilterAll() => SetPackageFilter("all");
    private void SetPackageFilterFailed() => SetPackageFilter("failed");
    private void SetPackageFilterActive() => SetPackageFilter("active");
    private void SetPackageFilterCompleted() => SetPackageFilter("completed");

    private IReadOnlyList<ContextWorkPackageDto> GetSelectedDependencies()
    {
        if (SelectedPackage == null || SelectedPackage.Dependencies.Count == 0)
        {
            return Array.Empty<ContextWorkPackageDto>();
        }

        return ContextPackages
            .Where(package => SelectedPackage.Dependencies.Contains(package.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private IReadOnlyList<ContextWorkPackageDto> GetSelectedDependents()
    {
        if (SelectedPackage == null)
        {
            return Array.Empty<ContextWorkPackageDto>();
        }

        return ContextPackages
            .Where(package => package.Dependencies.Contains(SelectedPackage.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private int GetFailedPackageCount()
    {
        return ContextPackages.Count(package => IsFailureStatus(package.Status));
    }

    private int GetActivePackageCount()
    {
        return ContextPackages.Count(package => IsExecutingStatus(package.Status) || IsEvaluatingStatus(package.Status));
    }

    private static bool IsFailureStatus(string? status)
    {
        return string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument ParseJson(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonDocument.Parse(json);
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return false;
        }

        var text = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }
}
