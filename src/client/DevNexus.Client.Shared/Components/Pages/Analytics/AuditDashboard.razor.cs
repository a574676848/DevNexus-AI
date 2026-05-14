using DevNexus.Client.Shared.DTOs;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Pages.Analytics;

/// <summary>
/// AI 使用与审计看板页面。
/// </summary>
public partial class AuditDashboard : IDisposable
{
    private const string AuditViewMine = "mine";
    private const string AuditViewSystem = "system";
    private const string AuditViewAiOptimization = "ai";
    private const string AuditViewDetail = "detail";
    private const string AuditViewTabClass = "audit-view-tab";
    private const string ActiveAuditViewTabClass = "audit-view-tab active";
    private const string CustomPeriod = "custom";
    private const string DefaultPeriodDays = "30";
    private const int DefaultPeriodDayCount = 30;

    [Parameter] public Guid? TargetUserId { get; set; }

    private bool IsAdmin => TargetUserId == null && UserStateService.CurrentUser?.Roles?.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)) == true;
    private bool IsExternalView => TargetUserId != null;

    private bool isLoading = true;
    private bool hasError;
    private string errorMessage = string.Empty;
    private string selectedPeriod = DefaultPeriodDays;
    private AuditDictionaryDto auditDictionary = new();
    private string selectedOwnerType = string.Empty;
    private string selectedSceneCode = string.Empty;
    private string selectedStatus = string.Empty;
    private string selectedAuditView = AuditViewMine;
    private DateTime? startDate;
    private DateTime? endDate;
    private bool shouldRenderCharts;

    private bool isDetailPanelVisible;
    private Guid? selectedUserId;
    private string selectedUserDisplayName = "用户";

    private TokenUsageStatsDto userStats = new();
    private PagedResultDto<TokenUsageDto> userRecords = new();
    private int userCurrentPage = 1;
    private const int UserPageSize = 5;

    private TokenUsageStatsDto adminStats = new();
    private AuditDashboardDto adminDashboard = new();
    private AiOptimizationDashboardDto aiOptimizationDashboard = new();
    private List<ProviderUsageStatsDto> providerStats = new();
    private List<UserRankingDto> userRanking = new();
    private PagedResult<TokenUsageDetailedDto> allRecords = new();
    private int adminCurrentPage = 1;
    private const int AdminPageSize = 5;

    private bool ShowAiOptimizationView => IsAdmin && selectedAuditView == AuditViewAiOptimization;
    private bool ShowAdminView => IsAdmin && selectedAuditView is AuditViewSystem or AuditViewDetail;

    protected override async Task OnInitializedAsync()
    {
        UserStateService.OnUserChanged += HandleUserChanged;
        SetDefaultDates();
        selectedAuditView = IsAdmin ? AuditViewSystem : AuditViewMine;
        await LoadDataAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!shouldRenderCharts)
        {
            return;
        }

        shouldRenderCharts = false;

        try
        {
            if (ShowAdminView)
            {
                await RenderAdminChartsAsync();
            }
            else
            {
                await RenderUserChartsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[AuditDashboard] Error rendering charts in OnAfterRenderAsync");
        }
    }

    private async Task HandleUserChanged(UserInfo? user)
    {
        await LoadDataAsync();
    }

    public void Dispose()
    {
        UserStateService.OnUserChanged -= HandleUserChanged;
    }

    private void SetDefaultDates()
    {
        endDate = DateTime.Today;
        startDate = int.TryParse(selectedPeriod, out var days)
            ? DateTime.Today.AddDays(-days)
            : DateTime.Today.AddDays(-DefaultPeriodDayCount);
    }

    private async Task OnPeriodChanged()
    {
        if (selectedPeriod != CustomPeriod)
        {
            SetDefaultDates();
            await LoadDataAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        isLoading = true;
        hasError = false;
        errorMessage = string.Empty;
        StateHasChanged();

        try
        {
            if (auditDictionary.Scenes.Count == 0)
            {
                auditDictionary = await ApiService.GetAuditDictionaryAsync();
            }

            if (ShowAiOptimizationView)
            {
                await LoadAiOptimizationDataAsync();
            }
            else if (ShowAdminView)
            {
                await LoadAdminDataAsync();
            }
            else
            {
                await LoadUserDataAsync(TargetUserId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载审计数据失败");
            hasError = true;
            errorMessage = "无法获取统计数据，请检查网络连接后重试";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadUserDataAsync(Guid? userId = null)
    {
        try
        {
            Logger.LogDebug(
                "[AuditDashboard] Loading user data | TargetUserId={TargetUserId} StartDate={StartDate} EndDate={EndDate} Page={Page}",
                userId,
                startDate,
                endDate,
                userCurrentPage);

            var statsTask = userId.HasValue
                ? ApiService.GetUserTokenStatsAsync(userId.Value, startDate, endDate)
                : ApiService.GetMyTokenStatsAsync(startDate, endDate);

            var recordsResult = userId.HasValue
                ? (object)(await ApiService.GetDetailedUsageRecordsAsync(
                    userId: userId.Value,
                    startDate: startDate,
                    endDate: endDate,
                    ownerType: selectedOwnerType,
                    sceneCode: selectedSceneCode,
                    status: selectedStatus,
                    pageNumber: userCurrentPage,
                    pageSize: UserPageSize))
                : (object)(await ApiService.GetMyTokenRecordsAsync(
                    startDate: startDate,
                    endDate: endDate,
                    ownerType: selectedOwnerType,
                    sceneCode: selectedSceneCode,
                    status: selectedStatus,
                    pageNumber: userCurrentPage,
                    pageSize: UserPageSize));

            userStats = await statsTask;

            if (recordsResult is PagedResult<TokenUsageDetailedDto> detailedResult)
            {
                userRecords = new PagedResultDto<TokenUsageDto>
                {
                    Items = detailedResult.Items.Select(r => new TokenUsageDto
                    {
                        Id = r.Id,
                        OwnerType = r.OwnerType,
                        OwnerUserId = r.OwnerUserId,
                        SessionId = r.SessionId,
                        MessageId = r.MessageId,
                        UserId = r.UserId,
                        InvocationKind = r.InvocationKind,
                        SceneCode = r.SceneCode,
                        SceneCategory = r.SceneCategory,
                        ResourceType = r.ResourceType,
                        ResourceId = r.ResourceId,
                        ModelId = r.ModelId,
                        ProviderId = r.ProviderId,
                        ProviderName = r.ProviderName,
                        InputTokens = r.InputTokens,
                        OutputTokens = r.OutputTokens,
                        TotalTokens = r.TotalTokens ?? ((r.InputTokens ?? 0) + (r.OutputTokens ?? 0)),
                        MeteringType = r.MeteringType,
                        MeteringValue = r.MeteringValue,
                        Cost = r.Cost,
                        RequestType = r.RequestType,
                        UsageSource = r.UsageSource,
                        Status = r.Status,
                        IsSuccess = r.IsSuccess,
                        ErrorCode = r.ErrorCode,
                        ErrorMessage = r.ErrorMessage,
                        StartedAt = r.StartedAt,
                        CompletedAt = r.CompletedAt,
                        CreatedAt = r.CreatedAt
                    }).ToList(),
                    TotalCount = detailedResult.TotalCount,
                    PageNumber = detailedResult.PageNumber,
                    PageSize = detailedResult.PageSize
                };
            }
            else if (recordsResult is PagedResultDto<TokenUsageDto> myResult)
            {
                userRecords = myResult;
            }

            Logger.LogDebug(
                "[AuditDashboard] User data loaded | TotalRequests={TotalRequests} TotalTokens={TotalTokens} RecordsCount={RecordsCount}",
                userStats.TotalRequests,
                userStats.TotalTokens,
                userRecords.Items.Count);

            if (userStats.DailyStats != null && userStats.DailyStats.Any())
            {
                shouldRenderCharts = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载用户数据失败");
            userStats = new();
            userRecords = new();
            throw;
        }
    }

    private async Task LoadAdminDataAsync()
    {
        try
        {
            var statsTask = ApiService.GetSystemTokenStatsFilteredAsync(
                startDate,
                endDate,
                selectedOwnerType,
                selectedSceneCode,
                null,
                selectedStatus);
            var dashboardTask = ApiService.GetAuditDashboardAsync(
                startDate,
                endDate,
                selectedOwnerType,
                selectedSceneCode,
                null,
                selectedStatus);
            var providerTask = ApiService.GetProviderStatsAsync(startDate, endDate);
            var rankingTask = selectedAuditView == AuditViewDetail
                ? Task.FromResult(new List<UserRankingDto>())
                : ApiService.GetUserRankingAsync(startDate, endDate, 10);
            var recordsTask = ApiService.GetDetailedUsageRecordsAsync(
                userId: null,
                startDate: startDate,
                endDate: endDate,
                ownerType: selectedOwnerType,
                sceneCode: selectedSceneCode,
                status: selectedStatus,
                pageNumber: adminCurrentPage,
                pageSize: AdminPageSize);

            await Task.WhenAll(statsTask, dashboardTask, providerTask, rankingTask, recordsTask);

            adminStats = await statsTask;
            adminDashboard = await dashboardTask;
            providerStats = await providerTask;
            userRanking = await rankingTask;
            allRecords = await recordsTask;

            if ((adminStats.DailyStats != null && adminStats.DailyStats.Any()) ||
                adminStats.OwnerStats.Any() ||
                adminStats.SceneStats.Any())
            {
                shouldRenderCharts = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载管理员数据失败");
            adminStats = new();
            adminDashboard = new();
            providerStats = new();
            userRanking = new();
            allRecords = new();
            throw;
        }
    }

    private async Task LoadAiOptimizationDataAsync()
    {
        try
        {
            aiOptimizationDashboard = await ApiService.GetAiOptimizationDashboardAsync(startDate, endDate);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载 AI 优化看板失败");
            aiOptimizationDashboard = new();
            throw;
        }
    }

    private async Task SwitchAuditView(string view)
    {
        selectedAuditView = view;
        if (view == AuditViewMine)
        {
            selectedOwnerType = string.Empty;
        }

        await LoadDataAsync();
    }

    private string GetViewTabClass(string view)
    {
        return selectedAuditView == view ? ActiveAuditViewTabClass : AuditViewTabClass;
    }

    private string GetOwnerLabel(string ownerType)
    {
        return auditDictionary.Owners.FirstOrDefault(item => item.Code == ownerType)?.DisplayName ?? ownerType;
    }

    private string GetStatusLabel(string status)
    {
        return auditDictionary.Statuses.FirstOrDefault(item => item.Code == status)?.DisplayName ?? status;
    }

    private string GetSceneLabel(string sceneCode)
    {
        return auditDictionary.Scenes.FirstOrDefault(item => item.Code == sceneCode)?.DisplayName ?? sceneCode;
    }

    private async Task RenderUserChartsAsync()
    {
        try
        {
            var dailyLabels = userStats.DailyStats.OrderBy(d => d.Date).Select(d => d.Date.ToString("MM-dd")).ToArray();
            var dailyData = userStats.DailyStats.OrderBy(d => d.Date).Select(d => d.TotalCost).ToArray();

            var lineChartData = new
            {
                labels = dailyLabels,
                datasets = new[]
                {
                    new
                    {
                        label = "每日成本 ($)",
                        data = dailyData,
                        borderColor = "#07c160",
                        backgroundColor = "rgba(7, 193, 96, 0.1)",
                        fill = true,
                        tension = 0.4
                    }
                }
            };

            await JS.InvokeVoidAsync("devnexus.charts.renderChart", "userTrendChart", lineChartData, "line");

            if (userStats.ModelStats != null && userStats.ModelStats.Any())
            {
                var modelLabels = userStats.ModelStats.Select(m => m.ModelId).ToArray();
                var modelData = userStats.ModelStats.Select(m => m.TotalTokens).ToArray();

                Logger.LogDebug(
                    "[AuditDashboard] Model chart data | Labels={Labels} Data={Data}",
                    string.Join(", ", modelLabels),
                    string.Join(", ", modelData));

                var pieChartData = new
                {
                    labels = modelLabels,
                    datasets = new[]
                    {
                        new
                        {
                            label = "Token 消耗",
                            data = modelData,
                            backgroundColor = new[] { "#07c160", "#576b95", "#fa5151", "#ffc300", "#10aeff", "#c77eb5" },
                            borderWidth = 0
                        }
                    }
                };

                await JS.InvokeVoidAsync("devnexus.charts.renderChart", "userModelChart", pieChartData, "doughnut");
                Logger.LogDebug("[AuditDashboard] User model chart rendered successfully");
            }
            else
            {
                Logger.LogWarning("[AuditDashboard] No ModelStats data available for chart rendering");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[AuditDashboard] Failed to render user charts");
        }
    }

    private async Task RenderAdminChartsAsync()
    {
        try
        {
            Logger.LogDebug(
                "[AuditDashboard] Rendering admin charts | DailyStatsCount={DailyCount} ProviderStatsCount={ProviderCount}",
                adminStats.DailyStats?.Count ?? 0,
                providerStats?.Count ?? 0);

            if (adminStats.DailyStats != null && adminStats.DailyStats.Any())
            {
                var dailyLabels = adminStats.DailyStats.OrderBy(d => d.Date).Select(d => d.Date.ToString("MM-dd")).ToArray();
                var dailyData = adminStats.DailyStats.OrderBy(d => d.Date).Select(d => d.TotalCost).ToArray();

                var lineChartData = new
                {
                    labels = dailyLabels,
                    datasets = new[]
                    {
                        new
                        {
                            label = "每日成本 ($)",
                            data = dailyData,
                            borderColor = "#07c160",
                            backgroundColor = "rgba(7, 193, 96, 0.1)",
                            fill = true,
                            tension = 0.4
                        }
                    }
                };

                await JS.InvokeVoidAsync("devnexus.charts.renderChart", "adminTrendChart", lineChartData, "line");
                Logger.LogDebug("[AuditDashboard] Admin trend chart rendered successfully");
            }
            else
            {
                Logger.LogWarning("[AuditDashboard] No DailyStats data available for admin trend chart");
            }

            if (adminStats.OwnerStats != null && adminStats.OwnerStats.Any())
            {
                var ownerLabels = adminStats.OwnerStats.Select(p => p.DisplayName).ToArray();
                var ownerData = adminStats.OwnerStats.Select(p => p.TotalCost).ToArray();
                var ownerChartData = new
                {
                    labels = ownerLabels,
                    datasets = new[]
                    {
                        new
                        {
                            label = "成本占比",
                            data = ownerData,
                            backgroundColor = new[] { "#07c160", "#576b95", "#fa5151", "#ffc300", "#10aeff", "#c77eb5" },
                            borderWidth = 0
                        }
                    }
                };

                await JS.InvokeVoidAsync("devnexus.charts.renderChart", "ownerBreakdownChart", ownerChartData, "doughnut");
                Logger.LogDebug("[AuditDashboard] Owner breakdown chart rendered successfully");
            }
            else
            {
                Logger.LogWarning("[AuditDashboard] No OwnerStats data available for owner chart");
            }

            if (adminStats.SceneStats != null && adminStats.SceneStats.Any())
            {
                var sceneTop = adminStats.SceneStats.Take(8).ToList();
                var sceneLabels = sceneTop.Select(p => p.DisplayName).ToArray();
                var sceneData = sceneTop.Select(p => p.TotalCost).ToArray();
                var sceneChartData = new
                {
                    labels = sceneLabels,
                    datasets = new[]
                    {
                        new
                        {
                            label = "场景成本",
                            data = sceneData,
                            backgroundColor = new[] { "#07c160", "#2f855a", "#2376d8", "#576b95", "#c98a11", "#d64545", "#7d8892", "#10aeff" },
                            borderWidth = 0
                        }
                    }
                };

                await JS.InvokeVoidAsync("devnexus.charts.renderChart", "sceneBreakdownChart", sceneChartData, "doughnut");
                Logger.LogDebug("[AuditDashboard] Scene breakdown chart rendered successfully");
            }
            else
            {
                Logger.LogWarning("[AuditDashboard] No SceneStats data available for scene chart");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[AuditDashboard] Failed to render admin charts");
        }
    }

    private async Task UserPreviousPage()
    {
        if (userCurrentPage > 1)
        {
            userCurrentPage--;
            await LoadUserDataAsync();
        }
    }

    private async Task UserNextPage()
    {
        if (userCurrentPage < userRecords.TotalPages)
        {
            userCurrentPage++;
            await LoadUserDataAsync();
        }
    }

    private async Task AdminPreviousPage()
    {
        if (adminCurrentPage > 1)
        {
            adminCurrentPage--;
            await LoadAdminDataAsync();
        }
    }

    private async Task AdminNextPage()
    {
        if (adminCurrentPage < allRecords.TotalPages)
        {
            adminCurrentPage++;
            await LoadAdminDataAsync();
        }
    }

    private void ShowUserDetail(Guid userId, string displayName)
    {
        selectedUserId = userId;
        selectedUserDisplayName = displayName;
        isDetailPanelVisible = true;
        StateHasChanged();
    }

    private void HandleUserClick((Guid userId, string displayName) args)
    {
        ShowUserDetail(args.userId, args.displayName);
    }

    private void CloseDetailPanel()
    {
        isDetailPanelVisible = false;
        StateHasChanged();
    }

    private static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString("N0")
        };
    }

    private static string FormatNumber(int number) => FormatNumber((long)number);

    private static string FormatPercent(double value)
    {
        return $"{value:P1}";
    }

    private string FormatFailureReasonRatio(int requestCount)
    {
        if (aiOptimizationDashboard.ToolFailureCount <= 0)
        {
            return "0.0%";
        }

        return FormatPercent((double)requestCount / aiOptimizationDashboard.ToolFailureCount);
    }

    private static string GetTrendIcon(string trend)
    {
        return trend switch
        {
            "up" => "↑",
            "down" => "↓",
            _ => "→"
        };
    }

    private static string GetTrendClass(string trend)
    {
        return trend switch
        {
            "up" => "trend-up",
            "down" => "trend-down",
            _ => "trend-flat"
        };
    }
}
