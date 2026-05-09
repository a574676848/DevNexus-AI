using System.Text.Json;
using System.Text.Json.Nodes;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Components.Shared;
using DevNexus.Client.Shared.Services.Exceptions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

public partial class EmbeddingProviderPanel
{
    [Parameter]
    public List<EmbeddingProviderResponse> Providers { get; set; } = new();

    [Parameter]
    public EventCallback OnRefresh { get; set; }

    [Parameter]
    public int GlobalVectorSize { get; set; }

    private bool showDialog = false;
    private bool showEditDialog = false;
    private bool showDeleteConfirm = false;
    private bool isSubmitting = false;
    private bool testingProvider = false;
    private Guid? testingProviderId;
    private string? editDialogMessage;
    private bool editDialogSuccess;
    private string? dialogMessage;
    private bool dialogSuccess;
    private string deleteConfirmMessage = string.Empty;
    private readonly HashSet<Guid> logoErrorProviders = new();
    private EmbeddingProviderType? _selectedProviderType;
    private HashSet<Guid> providerIdsWithPricing = new();

    private CreateEmbeddingProviderRequest newProvider = new();
    private UpdateEmbeddingProviderRequest editProvider = new();
    private EmbeddingProviderResponse? providerToEdit;
    private EmbeddingProviderResponse? providerToDelete;
    private readonly Dictionary<Guid, ValidateProviderResponse> testResults = new();

    private LogoSelector? addLogoSelector;
    private LogoSelector? editLogoSelector;
    private string? newProviderLogoPreview;
    private string? editProviderLogoPreview;
    private int editPriority = 100;

    protected override async Task OnInitializedAsync()
    {
        await LoadPricingDataAsync();
    }

    private async Task LoadPricingDataAsync()
    {
        try
        {
            var pricings = await ApiService.GetModelPricingsAsync();
            providerIdsWithPricing = pricings
                .Where(item => item.ProviderType == ModelInvocationProviderTypes.Embedding)
                .Select(item => item.ProviderId)
                .ToHashSet();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "EmbeddingProviderPanel.LoadPricingDataAsync");
        }
    }

    private void HandleLogoMessage((string Message, bool Success) msg)
    {
        dialogMessage = msg.Message;
        dialogSuccess = msg.Success;
        if (msg.Success && !string.IsNullOrEmpty(newProviderLogoPreview))
        {
            newProvider.LogoUrl = newProviderLogoPreview;
        }

        StateHasChanged();
    }

    private void HandleEditLogoMessage((string Message, bool Success) msg)
    {
        editDialogMessage = msg.Message;
        editDialogSuccess = msg.Success;
        if (msg.Success && !string.IsNullOrEmpty(editProviderLogoPreview))
        {
            editProvider.LogoUrl = editProviderLogoPreview;
        }

        StateHasChanged();
    }

    private void ShowAddDialog()
    {
        newProvider = new CreateEmbeddingProviderRequest
        {
            IsEnabled = true,
            VectorSize = GlobalVectorSize
        };
        _selectedProviderType = null;
        newProviderLogoPreview = null;
        dialogMessage = null;
        addLogoSelector?.Reset();
        showDialog = true;
    }

    private void CloseDialog()
    {
        showDialog = false;
        newProvider = new() { VectorSize = GlobalVectorSize };
        _selectedProviderType = null;
        newProviderLogoPreview = null;
    }

    private void OnProviderTypeChanged()
    {
        if (_selectedProviderType.HasValue)
        {
            newProvider.Type = _selectedProviderType.Value;
            newProvider.ProviderId = GetProviderIdFromType(_selectedProviderType.Value);
            newProviderLogoPreview = GetDefaultProviderLogoFromType(_selectedProviderType.Value);
        }
    }

    private async Task CreateProvider()
    {
        if (string.IsNullOrEmpty(newProvider.ProviderId) || string.IsNullOrEmpty(newProvider.ApiKey))
        {
            dialogMessage = "请填写必要字段";
            dialogSuccess = false;
            return;
        }

        isSubmitting = true;
        dialogMessage = null;
        StateHasChanged();

        try
        {
            if (!string.IsNullOrEmpty(newProviderLogoPreview))
            {
                newProvider.LogoUrl = newProviderLogoPreview;
            }

            await ApiService.CreateEmbeddingProviderAsync(newProvider);
            dialogSuccess = true;
            dialogMessage = "供应商创建成功！";
            await Task.Delay(500);
            CloseDialog();
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "EmbeddingProviderPanel.CreateProvider");
            dialogMessage = $"创建失败: {ExtractApiErrorMessage(ex)}";
            dialogSuccess = false;
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private async Task TestNewProvider()
    {
        if (string.IsNullOrEmpty(newProvider.ProviderId) || string.IsNullOrEmpty(newProvider.ApiKey))
        {
            dialogMessage = "请填写供应商类型和 API Key";
            dialogSuccess = false;
            return;
        }

        isSubmitting = true;
        dialogMessage = "测试连接中...";
        StateHasChanged();

        try
        {
            var result = await ApiService.TestEmbeddingProviderConnectionAsync(newProvider);
            dialogSuccess = result.IsValid;
            dialogMessage = result.IsValid ? "连接测试成功！" : result.ErrorMessage;
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "EmbeddingProviderPanel.TestNewProvider");
            dialogMessage = $"测试失败: {ExtractApiErrorMessage(ex)}";
            dialogSuccess = false;
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private async Task TestConnection(EmbeddingProviderResponse provider)
    {
        testingProvider = true;
        testingProviderId = provider.Id;
        testResults.Remove(provider.Id);
        StateHasChanged();

        try
        {
            var result = await ApiService.ValidateEmbeddingProviderAsync(provider.Id);
            testResults[provider.Id] = result;
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "EmbeddingProviderPanel.TestConnection", new() { ["ProviderId"] = provider.Id });
            testResults[provider.Id] = new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
        finally
        {
            testingProvider = false;
            testingProviderId = null;
            StateHasChanged();
        }
    }

    private void DismissTestResult(Guid providerId)
    {
        testResults.Remove(providerId);
        StateHasChanged();
    }

    private static string GetProviderIdFromType(EmbeddingProviderType type)
    {
        return type switch
        {
            EmbeddingProviderType.OpenAI => "openai",
            EmbeddingProviderType.Doubao => "doubao",
            EmbeddingProviderType.Local => "local",
            _ => "openai"
        };
    }

    private static string GetProviderTypeDisplayName(EmbeddingProviderType type)
    {
        return type switch
        {
            EmbeddingProviderType.OpenAI => "OpenAI",
            EmbeddingProviderType.Doubao => "豆包",
            EmbeddingProviderType.Local => "本地模型",
            _ => type.ToString()
        };
    }

    private static string GetDefaultProviderLogoFromType(EmbeddingProviderType type)
    {
        return type switch
        {
            EmbeddingProviderType.OpenAI => "/images/providers/openai-compatible.svg",
            EmbeddingProviderType.Doubao => "/images/providers/doubao.svg",
            EmbeddingProviderType.Local => "/images/providers/default.svg",
            _ => "/images/providers/default.svg"
        };
    }

    private void HandleLogoError(Guid providerId)
    {
        logoErrorProviders.Add(providerId);
    }

    private async Task SetAsDefault(EmbeddingProviderResponse provider)
    {
        try
        {
            await ApiService.SetDefaultEmbeddingProviderAsync(provider.Id);
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "EmbeddingProviderPanel.SetAsDefault");
        }
    }

    private void ShowDeleteConfirm(EmbeddingProviderResponse provider)
    {
        providerToDelete = provider;
        deleteConfirmMessage = $"确定要删除供应商 \"{provider.DisplayName}\" 吗？此操作无法撤销。";
        showDeleteConfirm = true;
    }

    private async Task ConfirmDelete()
    {
        if (providerToDelete == null)
        {
            return;
        }

        try
        {
            await ApiService.DeleteEmbeddingProviderAsync(providerToDelete.Id);
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "EmbeddingProviderPanel.ConfirmDelete", new() { ["ProviderId"] = providerToDelete.Id });
        }
        finally
        {
            providerToDelete = null;
        }
    }

    private void ShowEditDialog(EmbeddingProviderResponse provider)
    {
        providerToEdit = provider;
        editProvider = new UpdateEmbeddingProviderRequest
        {
            DisplayName = provider.DisplayName,
            Endpoint = provider.Endpoint,
            ModelName = provider.ModelName,
            IsEnabled = provider.IsEnabled
        };
        editProviderLogoPreview = provider.LogoUrl ?? GetDefaultProviderLogoFromType(provider.Type);
        editDialogMessage = null;
        editPriority = provider.Priority;
        showEditDialog = true;
    }

    private void CloseEditDialog()
    {
        showEditDialog = false;
        providerToEdit = null;
        editProvider = new();
        editProviderLogoPreview = null;
    }

    private async Task UpdateProvider()
    {
        if (providerToEdit == null)
        {
            return;
        }

        isSubmitting = true;
        editDialogMessage = null;
        StateHasChanged();

        try
        {
            if (!string.IsNullOrEmpty(editProviderLogoPreview))
            {
                editProvider.LogoUrl = editProviderLogoPreview;
            }

            editProvider.Priority = editPriority;
            await ApiService.UpdateEmbeddingProviderAsync(providerToEdit.Id, editProvider);
            editDialogSuccess = true;
            editDialogMessage = "供应商更新成功！";
            await Task.Delay(500);
            CloseEditDialog();
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "EmbeddingProviderPanel.UpdateProvider");
            editDialogMessage = $"更新失败: {ExtractApiErrorMessage(ex)}";
            editDialogSuccess = false;
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private void HandleNewLogoPreviewError()
    {
        if (_selectedProviderType.HasValue)
        {
            newProviderLogoPreview = GetDefaultProviderLogoFromType(_selectedProviderType.Value);
        }

        StateHasChanged();
    }

    private void HandleEditLogoPreviewError()
    {
        if (providerToEdit != null)
        {
            editProviderLogoPreview = GetDefaultProviderLogoFromType(providerToEdit.Type);
        }

        StateHasChanged();
    }

    private static string ExtractApiErrorMessage(Exception ex)
    {
        var message = ex.Message;
        if (ex is not ApiException apiEx || string.IsNullOrEmpty(apiEx.ResponseContent))
        {
            return message;
        }

        try
        {
            var errorObj = JsonSerializer.Deserialize<JsonObject>(apiEx.ResponseContent);
            if (errorObj != null && errorObj.TryGetPropertyValue("error", out var errorNode))
            {
                return errorNode?.ToString() ?? message;
            }
        }
        catch
        {
        }

        return message;
    }

    private void NavigateToPricing(Guid providerId)
    {
        Navigation.NavigateTo(
            $"/settings/model-pricing?providerId={providerId}&providerType={ModelInvocationProviderTypes.Embedding}");
    }
}
