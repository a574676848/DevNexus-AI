using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

/// <summary>
/// 模型定价管理页面。
/// </summary>
public partial class ModelPricing
{
    private List<ModelPricingResponse> pricings = new();
    private List<LLMProviderResponse> availableProviders = new();
    private List<EmbeddingProviderResponse> availableEmbeddingProviders = new();
    private Dictionary<Guid, string> providerLogos = new();
    private HashSet<Guid> failedLogos = new();
    private bool isLoading = true;
    private bool isSubmitting = false;
    private string? _processedProviderPrefillKey;
    private string? errorMessage;

    private bool showAddDialog = false;
    private CreateModelPricingRequest newPricing = new();
    private string? dialogMessage;
    private bool dialogSuccess;

    private bool showProviderDropdown = false;
    private string providerSearchQuery = "";

    private IEnumerable<ProviderOption> FilteredProviderOptions
    {
        get
        {
            var providers = GetProviderOptions(newPricing.ProviderType);

            if (!string.IsNullOrWhiteSpace(providerSearchQuery))
            {
                providers = providers.Where(p =>
                    p.DisplayName.Contains(providerSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.ProviderCode.Contains(providerSearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            return providers;
        }
    }

    private bool showEditDialog = false;
    private ModelPricingResponse? pricingToEdit;
    private UpdateModelPricingRequest editPricing = new();
    private string? editDialogMessage;
    private bool editDialogSuccess;

    private bool showDeleteConfirmDialog = false;
    private ModelPricingResponse? pricingToDelete;
    private string deleteConfirmMessage = "";

    [SupplyParameterFromQuery]
    public string? ProviderId { get; set; }

    [SupplyParameterFromQuery]
    public string? ProviderType { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    protected override void OnParametersSet()
    {
        var currentPrefillKey = BuildProviderPrefillKey();
        if (!string.Equals(_processedProviderPrefillKey, currentPrefillKey, StringComparison.OrdinalIgnoreCase))
        {
            _processedProviderPrefillKey = null;
        }

        if (!string.IsNullOrEmpty(ProviderId) && Guid.TryParse(ProviderId, out var targetProviderId) && pricings.Count > 0)
        {
            CheckAndOpenDialog(targetProviderId, NormalizeProviderType(ProviderType));
        }
    }

    private void CheckAndOpenDialog(Guid targetProviderId, string? providerType)
    {
        var prefillKey = BuildProviderPrefillKey();
        if (string.IsNullOrEmpty(prefillKey) || _processedProviderPrefillKey == prefillKey)
        {
            return;
        }

        var resolvedProviderType = ResolveProviderType(providerType, targetProviderId);
        var existingPricing = pricings.FirstOrDefault(p =>
            p.ProviderId == targetProviderId &&
            (string.IsNullOrWhiteSpace(resolvedProviderType) ||
             string.Equals(p.ProviderType, resolvedProviderType, StringComparison.OrdinalIgnoreCase)));

        if (existingPricing != null)
        {
            ShowEditDialog(existingPricing);
        }
        else
        {
            ShowAddDialogWithProvider(targetProviderId, resolvedProviderType);
        }

        _processedProviderPrefillKey = prefillKey;
    }

    private void ShowAddDialogWithProvider(Guid providerId, string? providerType)
    {
        var resolvedProviderType = ResolveProviderType(providerType, providerId) ?? ModelInvocationProviderTypes.Llm;
        newPricing = new CreateModelPricingRequest
        {
            IsEnabled = true,
            Currency = "CNY",
            ProviderType = resolvedProviderType,
            ProviderId = providerId
        };
        dialogMessage = null;
        providerSearchQuery = string.Empty;
        showAddDialog = true;
        StateHasChanged();
    }

    private async Task LoadDataAsync()
    {
        isLoading = true;
        errorMessage = null;
        StateHasChanged();

        try
        {
            var pricingResponses = await ApiService.GetModelPricingsAsync();
            availableProviders = await ApiService.GetLLMProvidersAsync(includeDisabled: true);
            availableEmbeddingProviders = await ApiService.GetEmbeddingProvidersAsync(includeDisabled: true);

            var stalePricings = pricingResponses
                .Where(item => !IsProviderActive(item.ProviderType, item.ProviderId))
                .ToList();

            pricings = pricingResponses
                .Where(item => IsProviderActive(item.ProviderType, item.ProviderId))
                .ToList();

            providerLogos.Clear();
            foreach (var provider in availableProviders)
            {
                if (!string.IsNullOrEmpty(provider.LogoUrl))
                {
                    providerLogos[provider.Id] = provider.LogoUrl;
                }
            }

            foreach (var provider in availableEmbeddingProviders)
            {
                if (!string.IsNullOrEmpty(provider.LogoUrl))
                {
                    providerLogos[provider.Id] = provider.LogoUrl;
                }
            }

            if (stalePricings.Count > 0)
            {
                _ = InvokeAsync(() => CleanupDeletedProviderPricingsAsync(stalePricings));
            }

            if (!string.IsNullOrEmpty(ProviderId) && Guid.TryParse(ProviderId, out var targetProviderId))
            {
                CheckAndOpenDialog(targetProviderId, NormalizeProviderType(ProviderType));
            }
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ModelPricing.LoadData");
            errorMessage = $"加载信息失败: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private string GetProviderLogoUrl(ModelPricingResponse pricing)
    {
        if (failedLogos.Contains(pricing.ProviderId))
        {
            return LogoService.GetDefaultLogo(pricing.ProviderProviderId);
        }

        if (providerLogos.TryGetValue(pricing.ProviderId, out var logoUrl))
        {
            return LogoService.GetLogoUrl(logoUrl, LogoService.GetDefaultLogo(pricing.ProviderProviderId));
        }

        return LogoService.GetDefaultLogo(pricing.ProviderProviderId);
    }

    private void HandleLogoError(Guid providerId)
    {
        failedLogos.Add(providerId);
        StateHasChanged();
    }

    private bool IsProviderActive(string? providerType, Guid providerId)
    {
        if (string.Equals(providerType, ModelInvocationProviderTypes.Embedding, StringComparison.OrdinalIgnoreCase))
        {
            return availableEmbeddingProviders.Any(provider => provider.Id == providerId);
        }

        return availableProviders.Any(provider => provider.Id == providerId);
    }

    private async Task CleanupDeletedProviderPricingsAsync(IReadOnlyCollection<ModelPricingResponse> stalePricings)
    {
        foreach (var pricing in stalePricings)
        {
            try
            {
                await ApiService.DeleteModelPricingAsync(pricing.Id);
            }
            catch (Exception ex)
            {
                await RemoteLog.LogErrorAsync(
                    ex,
                    "ModelPricing.CleanupDeletedProviderPricingsAsync",
                    new()
                    {
                        ["PricingId"] = pricing.Id,
                        ["ProviderId"] = pricing.ProviderId,
                        ["ProviderType"] = pricing.ProviderType
                    });
            }
        }
    }

    private void ShowAddDialog()
    {
        newPricing = new CreateModelPricingRequest
        {
            IsEnabled = true,
            Currency = "CNY",
            ProviderType = ModelInvocationProviderTypes.Llm
        };
        dialogMessage = null;
        showAddDialog = true;
    }

    private void CloseAddDialog()
    {
        showAddDialog = false;
        newPricing = new();
        CloseProviderDropdown();
    }

    private void ToggleProviderDropdown()
    {
        showProviderDropdown = !showProviderDropdown;
        if (showProviderDropdown)
        {
            providerSearchQuery = string.Empty;
        }
    }

    private void CloseProviderDropdown()
    {
        showProviderDropdown = false;
        providerSearchQuery = string.Empty;
    }

    private void SelectProvider(Guid providerId)
    {
        newPricing.ProviderId = providerId;
        CloseProviderDropdown();
    }

    private Task OnProviderTypeChanged()
    {
        newPricing.ProviderId = Guid.Empty;
        providerSearchQuery = string.Empty;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task CreatePricing()
    {
        if (newPricing.ProviderId == Guid.Empty)
        {
            dialogMessage = "请选择模型供应商";
            dialogSuccess = false;
            return;
        }

        isSubmitting = true;
        dialogMessage = null;
        StateHasChanged();

        try
        {
            await ApiService.CreateModelPricingAsync(newPricing);
            dialogSuccess = true;
            dialogMessage = "定价配置创建成功！";
            await Task.Delay(500);
            CloseAddDialog();
            ClearProviderIdParameter();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ModelPricing.CreatePricing");
            dialogMessage = $"创建失败: {ex.Message}";
            dialogSuccess = false;
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private void ShowEditDialog(ModelPricingResponse pricing)
    {
        pricingToEdit = pricing;
        editPricing = new UpdateModelPricingRequest
        {
            ProviderType = pricing.ProviderType,
            InputCostPerMillion = pricing.InputCostPerMillion,
            OutputCostPerMillion = pricing.OutputCostPerMillion,
            Currency = pricing.Currency,
            IsEnabled = pricing.IsEnabled
        };
        editDialogMessage = null;
        showEditDialog = true;
    }

    private void CloseEditDialog()
    {
        showEditDialog = false;
        pricingToEdit = null;
        editPricing = new();
    }

    private async Task UpdatePricing()
    {
        if (pricingToEdit == null)
        {
            return;
        }

        isSubmitting = true;
        editDialogMessage = null;
        StateHasChanged();

        try
        {
            await ApiService.UpdateModelPricingAsync(pricingToEdit.Id, editPricing);
            editDialogSuccess = true;
            editDialogMessage = "定价配置更新成功！";
            await Task.Delay(500);
            CloseEditDialog();
            ClearProviderIdParameter();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ModelPricing.UpdatePricing");
            editDialogMessage = $"更新失败: {ex.Message}";
            editDialogSuccess = false;
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private void ShowDeleteConfirm(ModelPricingResponse pricing)
    {
        pricingToDelete = pricing;
        deleteConfirmMessage = $"确定要删除供应商 \"{pricing.ProviderDisplayName}\" 的定价配置吗？此操作无法撤销。";
        showDeleteConfirmDialog = true;
    }

    private async Task ConfirmDelete()
    {
        if (pricingToDelete == null)
        {
            return;
        }

        try
        {
            await ApiService.DeleteModelPricingAsync(pricingToDelete.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ModelPricing.ConfirmDelete", new() { ["Id"] = pricingToDelete.Id });
        }
        finally
        {
            pricingToDelete = null;
        }
    }

    private void ClearProviderIdParameter()
    {
        ProviderId = null;
        ProviderType = null;
        _processedProviderPrefillKey = null;

        var uri = Navigation.GetUriWithQueryParameter("providerId", (string?)null);
        uri = RemoveProviderTypeQuery(uri);
        Navigation.NavigateTo(uri, replace: true);
    }

    private string? ResolveProviderType(string? providerType, Guid providerId)
    {
        var normalizedProviderType = NormalizeProviderType(providerType);
        if (!string.IsNullOrWhiteSpace(normalizedProviderType))
        {
            return normalizedProviderType;
        }

        if (availableEmbeddingProviders.Any(provider => provider.Id == providerId))
        {
            return ModelInvocationProviderTypes.Embedding;
        }

        if (availableProviders.Any(provider => provider.Id == providerId))
        {
            return ModelInvocationProviderTypes.Llm;
        }

        return null;
    }

    private string? BuildProviderPrefillKey()
    {
        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            return null;
        }

        return $"{NormalizeProviderType(ProviderType) ?? "unknown"}:{ProviderId}";
    }

    private static string? NormalizeProviderType(string? providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            return null;
        }

        return providerType.Trim().ToLowerInvariant() switch
        {
            "embedding" => ModelInvocationProviderTypes.Embedding,
            "llm" => ModelInvocationProviderTypes.Llm,
            _ => null
        };
    }

    private static string RemoveProviderTypeQuery(string uri)
    {
        return uri
            .Replace("?providerType=llm", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("&providerType=llm", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("?providerType=embedding", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("&providerType=embedding", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<ProviderOption> GetProviderOptions(string providerType)
    {
        if (providerType == ModelInvocationProviderTypes.Embedding)
        {
            return availableEmbeddingProviders.Select(provider => new ProviderOption(
                provider.Id,
                provider.DisplayName,
                provider.ProviderId,
                LogoService.GetLogoUrl(provider.LogoUrl, LogoService.GetDefaultLogo(provider.ProviderId))));
        }

        return availableProviders.Select(provider => new ProviderOption(
            provider.Id,
            provider.DisplayName,
            provider.ProviderId,
            LogoService.GetLogoUrl(provider.LogoUrl, LogoService.GetDefaultLogo(provider.ProviderId))));
    }

    private ProviderOption? GetSelectedProviderOption(string providerType, Guid providerId)
    {
        return GetProviderOptions(providerType).FirstOrDefault(item => item.Id == providerId);
    }

    private static string GetProviderTypeLabel(string? providerType)
    {
        return providerType == ModelInvocationProviderTypes.Embedding ? "向量模型" : "LLM 模型";
    }

    private string GetAddDialogTitle()
    {
        var selectedProvider = GetSelectedProviderOption(newPricing.ProviderType, newPricing.ProviderId);
        return selectedProvider == null
            ? "添加定价配置"
            : $"为 {selectedProvider.DisplayName} 添加定价";
    }

    private string? GetAddDialogHint()
    {
        var selectedProvider = GetSelectedProviderOption(newPricing.ProviderType, newPricing.ProviderId);
        if (selectedProvider == null)
        {
            return null;
        }

        return $"已定位到 {GetProviderTypeLabel(newPricing.ProviderType)}供应商 {selectedProvider.DisplayName}，可直接补充价格后创建。";
    }

    private record ProviderOption(Guid Id, string DisplayName, string ProviderCode, string LogoUrl);
}
