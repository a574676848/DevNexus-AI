using DevNexus.Shared.DTOs.Swarm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Swarm;

/// <summary>
/// SwarmMonitor - 主文件（生命周期和用户操作）
/// </summary>
public partial class SwarmMonitor : ComponentBase, IAsyncDisposable
{
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired]
    public string SessionId { get; set; } = string.Empty;

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public bool ShowHeader { get; set; } = true;

    [Inject]
    public IWindowService WindowService { get; set; } = default!;

    [Inject]
    public IAuthService AuthService { get; set; } = default!;

    [Inject]
    public IChatState ChatState { get; set; } = default!;

    [Inject]
    public IApiService ApiService { get; set; } = default!;

    // 状态字段
    private record ConfirmReq(string Id, string Operation, string Payload);
    private record TimelineEntry(string Text, string Tone, string Category, DateTime Timestamp);
    private List<ConfirmReq> _pendingConfirmations = new();
    private List<TimelineEntry> _timelineEntries = new();
    private List<ContextWorkPackageDto> ContextPackages { get; set; } = new();
    private SwarmSessionStatusSummaryDto? StatusSummary { get; set; }
    private List<AgentStatusDto> ActiveAgents { get; set; } = new();
    private HubConnection? _hubConnection;
    private bool IsPaused { get; set; } = false;
    private string _timelineFilter = "all";
    private string _packageFilter = "all";
    private string? _selectedPackageId;
    private readonly HashSet<string> _recentlyChangedPackageIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recentlyChangedAgentNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;
    private string? _errorMessage;

    private ContextWorkPackageDto? SelectedPackage =>
        string.IsNullOrWhiteSpace(_selectedPackageId)
            ? ContextPackages.FirstOrDefault()
            : ContextPackages.FirstOrDefault(package => string.Equals(package.Id, _selectedPackageId, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        var hubUrl = await AuthService.GetApiBaseUrlAsync() + "/swarm-hub";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await AuthService.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        SetupSignalRHandlers();

        try
        {
            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("JoinSession", SessionId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"连接 Swarm 失败：{ex.Message}";
        }
    }

    #region User Actions

    private async Task ApproveAction(string id)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.InvokeAsync("ResolveConfirmation", id, true);
            _pendingConfirmations.RemoveAll(x => x.Id == id);
            StateHasChanged();
        }
    }

    private async Task RejectAction(string id)
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.InvokeAsync("ResolveConfirmation", id, false);
            _pendingConfirmations.RemoveAll(x => x.Id == id);
            StateHasChanged();
        }
    }

    private async Task OnPause()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("PauseSession", SessionId);
    }

    private async Task OnResume()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("ResumeSession", SessionId);
    }

    private Task HandlePackageSelected(ContextWorkPackageDto package)
    {
        _selectedPackageId = package.Id;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task FocusFirstFailedPackageAsync()
    {
        var failedPackage = ContextPackages.FirstOrDefault(package => IsFailureStatus(package.Status));
        if (failedPackage == null)
        {
            return Task.CompletedTask;
        }

        _selectedPackageId = failedPackage.Id;
        _packageFilter = "failed";
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task OpenSwarmTerminalSidekickAsync()
    {
        if (ParsedSessionId != Guid.Empty && HasSwarmTerminalRecords)
        {
            ChatState.OpenChatTerminalSidekick(ParsedSessionId, SelectedTerminalRecord?.RecordId);
        }

        return Task.CompletedTask;
    }

    private async Task OpenSelectedPackageArtifactAsync()
    {
        if (SelectedPackage?.ExecutionReportArtifactId == null || ParsedSessionId == Guid.Empty)
        {
            return;
        }

        var artifact = await ApiService.GetArtifactAsync(SelectedPackage.ExecutionReportArtifactId.Value);
        if (artifact == null)
        {
            _errorMessage = "未找到该工作包的执行报告。";
            StateHasChanged();
            return;
        }

        ChatState.SetArtifact(ParsedSessionId, artifact);
        ChatState.OpenArtifactSidekick(ParsedSessionId);
    }

    private Task FocusSelectedPackageTerminalAsync()
    {
        if (ParsedSessionId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        var record = GetSelectedPackageTerminalRecords().FirstOrDefault();
        if (record == null)
        {
            return Task.CompletedTask;
        }

        ChatState.OpenChatTerminalSidekick(ParsedSessionId, record.RecordId);
        return Task.CompletedTask;
    }

    private async Task RetrySelectedPackageAsync()
    {
        if (SelectedPackage == null || ParsedSessionId == Guid.Empty || !SelectedPackage.CanRetry)
        {
            return;
        }

        _errorMessage = null;

        try
        {
            await ApiService.RetrySwarmPackageAsync(ParsedSessionId, SelectedPackage.Id);
            AddTimelineEntry($"已发起工作包重试：{SelectedPackage.Title}", "warning", "task");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            _errorMessage = $"发起工作包重试失败：{ex.Message}";
            StateHasChanged();
        }
    }

    private void OnOpenInWindow()
    {
        if (Guid.TryParse(SessionId, out var parsedSessionId))
        {
            WindowService.OpenSwarmWindow(parsedSessionId);
        }
    }

    private async Task ClosePanelAsync()
    {
        _errorMessage = null;
        if (OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
