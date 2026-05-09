using System.Text.Json;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Components.Icons;
using DevNexus.Client.Shared.Components.Shared;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

public partial class LLMProviderPanel
{
    private static readonly ProviderType[] SupportedProviderTypes =
    {
        ProviderType.OpenAICompatible,
        ProviderType.Gemini,
        ProviderType.Kimi,
        ProviderType.MiniMax,
        ProviderType.DeepSeek,
        ProviderType.GLM
    };

    [Parameter]
    public List<LLMProviderResponse> Providers { get; set; } = new();

    [Parameter]
    public EventCallback OnRefresh { get; set; }

    private bool showDialog = false;
    private bool showEditDialog = false;
    private bool showDeleteConfirm = false;
    private bool isSubmitting = false;
    private bool testingProvider = false;
    private Guid? testingProviderId;
    private string? dialogMessage;
    private bool dialogSuccess;
    private string? editDialogMessage;
    private bool editDialogSuccess;
    private string deleteConfirmMessage = "";

    private CreateLLMProviderRequest newProvider = new();
    private UpdateLLMProviderRequest editProvider = new();
    private LLMProviderResponse? providerToEdit;
    private LLMProviderResponse? providerToDelete;
    private Dictionary<Guid, ValidateProviderResponse> testResults = new();
    private HashSet<Guid> logoErrorProviders = new();
    private ProviderType? _selectedProviderType;

    // Logo selector component references
    private LogoSelector? addLogoSelector;
    private LogoSelector? editLogoSelector;
    private string? newProviderLogoPreview;
    private string? editProviderLogoPreview;

    // LLM 参数配置（存储在 Configuration JSONB 中）
    private int newMaxTokens = 8096;
    private double newTemperature = 0.7;
    private double newTopP = 0.9;
    private string? newGroupId;
    private int editMaxTokens = 8096;
    private double editTemperature = 0.7;
    private double editTopP = 0.9;
    private string? editGroupId;

    private int editPriority = 100;
    
    // Vision 能力配置
    private bool newSupportsVision = false;
    private bool editSupportsVision = false;

    // Text-to-Image 能力配置
    private bool newSupportsTextToImage = false;
    private bool editSupportsTextToImage = false;

    // 定价信息
    private HashSet<Guid> providerIdsWithPricing = new();

    // 更多操作菜单状态
    private Guid? moreMenuOpenProviderId;

    protected override async Task OnInitializedAsync()
    {
        await LoadPricingDataAsync();
    }

    private async Task LoadPricingDataAsync()
    {
        try
        {
            var pricings = await ApiService.GetModelPricingsAsync();
            providerIdsWithPricing = pricings.Select(p => p.ProviderId).ToHashSet();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            // 非阻断性错误，仅记录日志
            await RemoteLog.LogErrorAsync(ex, "LLMProviderPanel.LoadPricingDataAsync");
        }
    }

    /// <summary>
    /// 处理 Logo 选择消息（Add 对话框）
    /// </summary>
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

    /// <summary>
    /// 处理 Logo 选择消息（Edit 对话框）
    /// </summary>
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
        newProvider = new CreateLLMProviderRequest { IsEnabled = true };
        _selectedProviderType = null;
        newProviderLogoPreview = null;
        dialogMessage = null;
        addLogoSelector?.Reset();
        // 重置 LLM 参数为默认值
        newMaxTokens = 4096;
        newTemperature = 0.7;
        newTopP = 0.9;
        newGroupId = null;
        newGroupId = null;
        newSupportsVision = false;
        newSupportsTextToImage = false;
        showDialog = true;
    }

    private void CloseDialog()
    {
        showDialog = false;
        newProvider = new();
        _selectedProviderType = null;
        newProviderLogoPreview = null;
    }

    private void OnProviderTypeChanged()
    {
        // 绑定 ProviderType 并同步 ProviderId
        if (_selectedProviderType.HasValue)
        {
            newProvider.Type = _selectedProviderType.Value;
            newProvider.ProviderId = GetProviderIdFromType(_selectedProviderType.Value);
            newProviderLogoPreview = LogoService.GetDefaultLogo(newProvider.ProviderId);
        }
    }

    /// <summary>
    /// 根据 ProviderType 生成 ProviderId
    /// </summary>
    private static string GetProviderIdFromType(ProviderType type)
    {
        return type switch
        {
            ProviderType.OpenAICompatible => "openai-compatible",
            ProviderType.Gemini => "gemini",
            ProviderType.Kimi => "kimi",
            ProviderType.MiniMax => "minimax",
            ProviderType.DeepSeek => "deepseek",
            ProviderType.GLM => "glm",
            _ => "openai-compatible"
        };
    }

    /// <summary>
    /// 根据 ProviderType 获取显示名称
    /// </summary>
    private static string GetProviderTypeDisplayName(ProviderType type)
    {
        return type switch
        {
            ProviderType.OpenAICompatible => "OpenAI Compatible（自定义）",
            ProviderType.Gemini => "Gemini",
            ProviderType.Kimi => "Kimi",
            ProviderType.MiniMax => "MiniMax",
            ProviderType.DeepSeek => "DeepSeek",
            ProviderType.GLM => "GLM（智谱）",
            _ => "OpenAI Compatible（自定义）"
        };
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
            // 将 LLM 参数存入 Configuration
            newProvider.Configuration ??= new Dictionary<string, object>();
            newProvider.Configuration["maxTokens"] = newMaxTokens;
            newProvider.Configuration["temperature"] = newTemperature;
            newProvider.Configuration["topP"] = newTopP;
            if (!string.IsNullOrEmpty(newGroupId))
            {
                newProvider.Configuration["groupId"] = newGroupId;
            }
            newProvider.Configuration["SupportsVision"] = newSupportsVision;

            // 处理 Capabilities
            var capabilities = new List<string>();
            if (newSupportsTextToImage) capabilities.Add("TextToImage");
            newProvider.Configuration["Capabilities"] = capabilities;

            await ApiService.CreateLLMProviderAsync(newProvider);
            dialogSuccess = true;
            dialogMessage = "供应商创建成功！";
            await Task.Delay(500);
            CloseDialog();
            CloseDialog();
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "LLMProviderPanel.CreateProvider");
            var message = ex.Message;
            if (ex is DevNexus.Client.Shared.Services.Exceptions.ApiException apiEx && !string.IsNullOrEmpty(apiEx.ResponseContent))
            {
                try 
                {
                    var errorObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(apiEx.ResponseContent);
                    if (errorObj != null && errorObj.TryGetPropertyValue("error", out var errorNode))
                    {
                        message = errorNode?.ToString() ?? message;
                    }
                }
                catch { /* 忽略解析错误 */ }
            }
            dialogMessage = $"创建失败: {message}";
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
            dialogMessage = "请填写供应商类型和API Key";
            dialogSuccess = false;
            return;
        }

        isSubmitting = true;
        dialogMessage = "测试连接中..";
        StateHasChanged();

        try
        {
            var result = await ApiService.TestLLMProviderConnectionAsync(newProvider);
            dialogSuccess = result.IsValid;
            dialogMessage = result.IsValid ? "连接测试成功" : result.ErrorMessage;
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "LLMProviderPanel.TestNewProvider");
            var message = ex.Message;
            if (ex is DevNexus.Client.Shared.Services.Exceptions.ApiException apiEx && !string.IsNullOrEmpty(apiEx.ResponseContent))
            {
                try 
                {
                    var errorObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(apiEx.ResponseContent);
                    if (errorObj != null && errorObj.TryGetPropertyValue("error", out var errorNode))
                    {
                        message = errorNode?.ToString() ?? message;
                    }
                }
                catch { /* 忽略解析错误 */ }
            }
            dialogMessage = $"测试失败: {message}";
            dialogSuccess = false;
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private async Task TestConnection(LLMProviderResponse provider)
    {
        testingProvider = true;
        testingProviderId = provider.Id;
        testResults.Remove(provider.Id);
        StateHasChanged();

        try
        {
            var result = await ApiService.ValidateLLMProviderAsync(provider.Id);
            testResults[provider.Id] = result;
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "LLMProviderPanel.TestConnection", new() { ["ProviderId"] = provider.Id });
            testResults[provider.Id] = new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
        finally
        {
            testingProvider = false;
            testingProviderId = null;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 切换更多操作菜单
    /// </summary>
    private void ToggleMoreMenu(Guid providerId)
    {
        moreMenuOpenProviderId = moreMenuOpenProviderId == providerId ? null : providerId;
    }

    /// <summary>
    /// 点击面板空白处关闭更多菜单
    /// </summary>
    private void CloseMoreMenu()
    {
        if (moreMenuOpenProviderId != null)
        {
            moreMenuOpenProviderId = null;
        }
    }

    /// <summary>
    /// 从更多菜单发起测试连接
    /// </summary>
    private async Task TestConnectionFromMenu(LLMProviderResponse provider)
    {
        moreMenuOpenProviderId = null;
        await TestConnection(provider);
    }

    /// <summary>
    /// 从更多菜单发起删除确认
    /// </summary>
    private void ShowDeleteConfirmFromMenu(LLMProviderResponse provider)
    {
        moreMenuOpenProviderId = null;
        ShowDeleteConfirm(provider);
    }

    /// <summary>
    /// 关闭测试结果显示
    /// </summary>
    private void DismissTestResult(Guid providerId)
    {
        testResults.Remove(providerId);
        StateHasChanged();
    }

    private void NavigateToPricing(Guid providerId)
    {
        Navigation.NavigateTo(
            $"/settings/model-pricing?providerId={providerId}&providerType={ModelInvocationProviderTypes.Llm}");
    }

    private async Task SetAsDefault(LLMProviderResponse provider)
    {
        try
        {
            await ApiService.SetDefaultLLMProviderAsync(provider.Id);
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "LLMProviderPanel.SetAsDefault", new() { ["ProviderId"] = provider.Id });
        }
    }

    private void ShowDeleteConfirm(LLMProviderResponse provider)
    {
        providerToDelete = provider;
        deleteConfirmMessage = $"确定要删除供应商 \"{provider.DisplayName}\" 吗？此操作无法撤销";
        showDeleteConfirm = true;
    }

    private async Task ConfirmDelete()
    {
        if (providerToDelete == null) return;

        try
        {
            await ApiService.DeleteLLMProviderAsync(providerToDelete.Id);
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "LLMProviderPanel.ConfirmDelete", new() { ["ProviderId"] = providerToDelete.Id });
        }
        finally
        {
            providerToDelete = null;
            showDeleteConfirm = false;
        }
    }

    private void HandleLogoError(Guid providerId)
    {
        logoErrorProviders.Add(providerId);
        // StateHasChanged(); // 移除此处调用，避免触发由 ProviderCard 引起的循环渲染
    }

    private void ShowEditDialog(LLMProviderResponse provider)
    {
        providerToEdit = provider;
        editProvider = new UpdateLLMProviderRequest
            {
                DisplayName = provider.DisplayName,
                Endpoint = provider.Endpoint,
                ModelName = provider.ModelName,
                IsEnabled = provider.IsEnabled
            };
        editProviderLogoPreview = provider.LogoUrl ?? LogoService.GetDefaultLogo(provider.ProviderId);
        editDialogMessage = null;
        editLogoSelector?.Reset();
        // 从 Configuration 读取 LLM 参数
        editMaxTokens = GetConfigValue<int>(provider.Configuration, "maxTokens", 4096);
        editTemperature = GetConfigValue<double>(provider.Configuration, "temperature", 0.7);
        editTopP = GetConfigValue<double>(provider.Configuration, "topP", 0.9);
        editGroupId = GetConfigValue<string?>(provider.Configuration, "groupId", null);
        editSupportsVision = GetConfigValue<bool>(provider.Configuration, "SupportsVision", false);
        
        // 读取 Capabilities - 直接从 Configuration 字典中获取避免类型转换问题
        editSupportsTextToImage = false;
        if (provider.Configuration?.TryGetValue("Capabilities", out var capsObj) == true)
        {
            if (capsObj is System.Text.Json.JsonElement capsJson && capsJson.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in capsJson.EnumerateArray())
                {
                    if (item.GetString()?.Equals("TextToImage", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        editSupportsTextToImage = true;
                        break;
                    }
                }
            }
            else if (capsObj is IEnumerable<string> capsList)
            {
                editSupportsTextToImage = capsList.Any(c => c.Equals("TextToImage", StringComparison.OrdinalIgnoreCase));
            }
            else if (capsObj is string capsStr)
            {
                // 处理逗号分隔的字符串
                editSupportsTextToImage = capsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Any(c => c.Trim().Equals("TextToImage", StringComparison.OrdinalIgnoreCase));
            }
        }

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
        if (providerToEdit == null) return;

        isSubmitting = true;
        editDialogMessage = null;
        StateHasChanged();

        try
        {
            if (!string.IsNullOrEmpty(editProviderLogoPreview))
            {
                editProvider.LogoUrl = editProviderLogoPreview;
            }
            // 将 LLM 参数存入 Configuration
            editProvider.Configuration ??= new Dictionary<string, object>();
            editProvider.Configuration["maxTokens"] = editMaxTokens;
            editProvider.Configuration["temperature"] = editTemperature;
            editProvider.Configuration["topP"] = editTopP;
            if (!string.IsNullOrEmpty(editGroupId))
            {
                editProvider.Configuration["groupId"] = editGroupId;
            }
            editProvider.Configuration["SupportsVision"] = editSupportsVision;

            // 处理 Capabilities
            var capabilities = new List<string>();
            if (editSupportsTextToImage) capabilities.Add("TextToImage");
            editProvider.Configuration["Capabilities"] = capabilities;

            editProvider.Priority = editPriority;
            await ApiService.UpdateLLMProviderAsync(providerToEdit.Id, editProvider);
            editDialogSuccess = true;
            editDialogMessage = "供应商更新成功！";
            await Task.Delay(500);
            CloseEditDialog();
            await OnRefresh.InvokeAsync();
            await LoadPricingDataAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "LLMProviderPanel.UpdateProvider");
            var message = ex.Message;
            if (ex is DevNexus.Client.Shared.Services.Exceptions.ApiException apiEx && !string.IsNullOrEmpty(apiEx.ResponseContent))
            {
                try 
                {
                    var errorObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(apiEx.ResponseContent);
                    if (errorObj != null && errorObj.TryGetPropertyValue("error", out var errorNode))
                    {
                        message = errorNode?.ToString() ?? message;
                    }
                }
                catch { /* 忽略解析错误 */ }
            }
            editDialogMessage = $"更新失败: {message}";
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
        // 如果预览图片加载失败，回退到默认图标
        newProviderLogoPreview = LogoService.GetDefaultLogo(newProvider.ProviderId);
        StateHasChanged();
    }

    private void HandleEditLogoPreviewError()
    {
        // 如果编辑时预览图片加载失败，回退到默认图标
        if (providerToEdit != null)
        {
            editProviderLogoPreview = LogoService.GetDefaultLogo(providerToEdit.ProviderId);
        }
        StateHasChanged();
    }

    /// <summary>
    /// 从配置字典获取值
    /// </summary>
    private static T? GetConfigValue<T>(Dictionary<string, object>? config, string key, T? defaultValue)
    {
        if (config == null || !config.TryGetValue(key, out var value))
        {
            return defaultValue;
        }
        
        try
        {
            // 处理 JSON 反序列化后的类型转换
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)jsonElement.GetInt32();
                }
                if (typeof(T) == typeof(double))
                {
                    return (T)(object)jsonElement.GetDouble();
                }
                if (typeof(T) == typeof(string) || typeof(T) == typeof(string))
                {
                    return (T)(object)jsonElement.GetString()!;
                }
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)jsonElement.GetBoolean();
                }
            }
            
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}
