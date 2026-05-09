using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Client.Shared.Components.Editor;
using DevNexus.Client.Shared.Services.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

/// <summary>
/// 版本发布中心页面。
/// </summary>
public partial class ReleaseCenter : ComponentBase
{
    private static readonly JsonSerializerOptions TemplateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Inject]
    private ToastService ToastService { get; set; } = default!;

    private const string ReleasesTab = "releases";
    internal const string RolloutsTab = "rollouts";
    internal const string ObservabilityTab = "observability";
    private const string OverviewTab = "overview";
    private const string WorkflowView = "workflow";
    private const string DashboardView = "dashboard";
    internal static readonly IReadOnlyList<string> ReleaseChannelOptions = ["stable", "beta", "alpha"];
    internal static readonly IReadOnlyList<string> ArtifactPlatformOptions = ["desktop-windows", "desktop-macos", "desktop-linux", "web"];
    internal static readonly IReadOnlyList<string> ArtifactArchitectureOptions = ["x64", "arm64", "any", "browser"];
    internal static readonly IReadOnlyList<string> ArtifactPackageTypeOptions = ["installer", "portable", "pkg", "dmg", "deb", "rpm", "refresh"];

    private string activeTab = ReleasesTab;
    private string activeView = WorkflowView;
    private string lastDashboardTab = OverviewTab;
    private bool isLoading = true;
    internal bool isSaving;
    private string? errorMessage;
    internal List<ReleaseDto> releases = new();
    internal List<RolloutDto> rollouts = new();
    private UpdateObservabilitySummaryDto observability = new();
    private UpdateObservabilityDetailDto observabilityDetails = new();
    private UpdateObservabilityFilterRequest observabilityFilter = new();
    private UpdateManifestResponse? previewResult;
    internal string importMetadataJson = string.Empty;
    private bool importPublishRelease = true;
    private bool importCreateRollout = true;
    internal MonacoEditor? _importMetadataEditor;
    private bool showImportMetadataExpanded;
    internal bool showPublishWizard;
    internal int wizardStep = 1;
    internal string wizardReleaseMode = "import";
    internal bool wizardPublishRelease = true;
    internal bool wizardCreateRollout = true;
    internal bool wizardAcknowledgeRisks;
    internal bool wizardCompleted;
    internal List<string> wizardExecutionResults = new();
    internal ReleaseDto? wizardLastRelease;
    internal RolloutDto? wizardLastRollout;
    private string checkTrendPolyline = string.Empty;
    private string updateAvailableTrendPolyline = string.Empty;
    private string installCompletedTrendPolyline = string.Empty;
    private string failedTrendPolyline = string.Empty;
    internal ReleaseFormModel releaseForm = ReleaseFormModel.CreateEmpty();
    internal RolloutFormModel rolloutForm = RolloutFormModel.CreateEmpty();

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            var releaseTask = ApiService.GetReleasesAsync();
            var rolloutTask = ApiService.GetRolloutsAsync();
            var observabilityTask = ApiService.GetUpdateObservabilitySummaryAsync();
            var observabilityDetailsTask = ApiService.GetUpdateObservabilityDetailsAsync(observabilityFilter);

            await Task.WhenAll(releaseTask, rolloutTask, observabilityTask, observabilityDetailsTask);

            releases = (await releaseTask).OrderByDescending(item => item.CreatedAt).ToList();
            rollouts = (await rolloutTask).OrderByDescending(item => item.UpdatedAt).ToList();
            observability = await observabilityTask ?? new UpdateObservabilitySummaryDto();
            observabilityDetails = await observabilityDetailsTask ?? new UpdateObservabilityDetailDto();
            RefreshTrendPolylines();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.RefreshAsync");
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    internal void SwitchTab(string tab)
    {
        activeTab = tab;
        if (tab is OverviewTab or ReleasesTab or RolloutsTab or ObservabilityTab)
        {
            lastDashboardTab = tab;
        }

        previewResult = null;
    }

    private void SwitchView(string view)
    {
        activeView = view;
        if (view == DashboardView)
        {
            activeTab = lastDashboardTab;
        }
    }


    private async Task ApplyObservabilityFiltersAsync()
    {
        isLoading = true;
        try
        {
            observabilityDetails = await ApiService.GetUpdateObservabilityDetailsAsync(observabilityFilter) ?? new UpdateObservabilityDetailDto();
            RefreshTrendPolylines();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.ApplyObservabilityFiltersAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isLoading = false;
        }
    }


}
