using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Components.Shared;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

public partial class StorageProviderPanel
{
[Parameter]
    public List<StorageProviderResponse> Providers { get; set; } = new();

    [Parameter]
    public EventCallback OnRefresh { get; set; }

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
    private string deleteConfirmMessage = "";
    private bool editProviderIsEnabled = true;
    private bool editProviderForcePathStyle = false;
    private bool editProviderUseHttps = true;
    private HashSet<Guid> logoErrorProviders = new();

    private CreateStorageProviderRequest newProvider = new();
    private UpdateStorageProviderRequest editProvider = new();
    private StorageProviderResponse? providerToEdit;
    private StorageProviderResponse? providerToDelete;
    private Dictionary<Guid, ValidateProviderResponse> testResults = new();

    // Logo selector component references
    private LogoSelector? addLogoSelector;
    private LogoSelector? editLogoSelector;
    private string? newProviderLogoPreview;
    private string? editProviderLogoPreview;
    private StorageProviderType? _selectedProviderType;
    private int editPriority = 100;

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
        newProvider = new CreateStorageProviderRequest { IsEnabled = true, UseHttps = true, PresignedUrlExpirationSeconds = 3600 };
        _selectedProviderType = null;
        newProviderLogoPreview = null;
        dialogMessage = null;
        addLogoSelector?.Reset();
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
            newProviderLogoPreview = GetDefaultProviderLogo(_selectedProviderType.Value);
        }
    }

    /// <summary>
    /// 根据 StorageProviderType 生成 ProviderId
    /// </summary>
    private static string GetProviderIdFromType(StorageProviderType type)
    {
        return type switch
        {
            StorageProviderType.AwsS3 => "aws-s3",
            StorageProviderType.AliyunOss => "aliyun-oss",
            StorageProviderType.QiniuKodo => "qiniu-kodo",
            StorageProviderType.TencentCos => "tencent-cos",
            StorageProviderType.MinIO => "minio",
            StorageProviderType.CloudflareR2 => "cloudflare-r2",
            StorageProviderType.Local => "local",
            StorageProviderType.S3Compatible => "s3-compatible",
            _ => "aws-s3"
        };
    }

    private async Task CreateProvider()
    {
        if (string.IsNullOrEmpty(newProvider.ProviderId))
        {
            dialogMessage = "请选择存储类型";
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
            
            await ApiService.CreateStorageProviderAsync(newProvider);
            dialogSuccess = true;
            dialogMessage = "存储服务创建成功！";
            await Task.Delay(500);
            CloseDialog();
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "StorageProviderPanel.CreateProvider");
            dialogMessage = $"创建失败: {ex.Message}";
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
        if (string.IsNullOrEmpty(newProvider.ProviderId))
        {
            dialogMessage = "请选择存储类型";
            dialogSuccess = false;
            return;
        }

        isSubmitting = true;
        dialogMessage = "测试连接中...";
        StateHasChanged();

        try
        {
            var result = await ApiService.TestStorageProviderConnectionAsync(newProvider);
            dialogSuccess = result.IsValid;
            dialogMessage = result.IsValid ? "连接测试成功！" : result.ErrorMessage;
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "StorageProviderPanel.TestNewProvider");
            dialogMessage = $"测试失败: {ex.Message}";
            dialogSuccess = false;
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private async Task TestConnection(StorageProviderResponse provider)
    {
        testingProvider = true;
        testingProviderId = provider.Id;
        testResults.Remove(provider.Id);
        StateHasChanged();

        try
        {
            var result = await ApiService.ValidateStorageProviderAsync(provider.Id);
            testResults[provider.Id] = result;
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "StorageProviderPanel.TestConnection", new() { ["ProviderId"] = provider.Id });
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
    /// 关闭测试结果显示
    /// </summary>
    private void DismissTestResult(Guid providerId)
    {
        testResults.Remove(providerId);
        StateHasChanged();
    }

    private string GetDefaultProviderLogo(StorageProviderType providerType)
    {
        return providerType switch
        {
            StorageProviderType.AwsS3 => "/images/providers/aws.svg",
            StorageProviderType.AliyunOss => "/images/providers/aliyun.svg",
            StorageProviderType.QiniuKodo => "/images/providers/qiniu.svg",
            StorageProviderType.TencentCos => "/images/providers/tencent.svg",
            StorageProviderType.MinIO => "/images/providers/default.svg",
            StorageProviderType.CloudflareR2 => "/images/providers/cloudflare.svg",
            StorageProviderType.Local => "/images/providers/default.svg",
            StorageProviderType.S3Compatible => "/images/providers/aws.svg",
            _ => "/images/providers/default.svg"
        };
    }

    private string GetProviderTypeName(StorageProviderType providerType)
    {
        return providerType switch
        {
            StorageProviderType.AwsS3 => "AWS S3",
            StorageProviderType.AliyunOss => "阿里云 OSS",
            StorageProviderType.QiniuKodo => "七牛云 Kodo",
            StorageProviderType.TencentCos => "腾讯云 COS",
            StorageProviderType.MinIO => "MinIO",
            StorageProviderType.CloudflareR2 => "Cloudflare R2",
            StorageProviderType.Local => "本地存储",
            StorageProviderType.S3Compatible => "S3 兼容服务",
            _ => "未知"
        };
    }


    private void HandleLogoError(Guid providerId)
    {
        logoErrorProviders.Add(providerId);
        // StateHasChanged(); // 移除此处调用，避免触发由 ProviderCard 引起的循环渲染
    }

    private async Task SetAsDefault(StorageProviderResponse provider)
    {
        try
        {
            await ApiService.SetDefaultStorageProviderAsync(provider.Id);
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "StorageProviderPanel.SetAsDefault");
        }
    }

    private void ShowDeleteConfirm(StorageProviderResponse provider)
    {
        providerToDelete = provider;
        deleteConfirmMessage = $"确定要删除存储服务 \"{provider.DisplayName}\" 吗？此操作无法撤销。";
        showDeleteConfirm = true;
    }

    private async Task ConfirmDelete()
    {
        if (providerToDelete == null) return;

        try
        {
            await ApiService.DeleteStorageProviderAsync(providerToDelete.Id);
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "StorageProviderPanel.ConfirmDelete", new() { ["ProviderId"] = providerToDelete.Id });
        }
        finally
        {
            providerToDelete = null;
        }
    }

    private void ShowEditDialog(StorageProviderResponse provider)
    {
        providerToEdit = provider;
        editProvider = new UpdateStorageProviderRequest
            {
                DisplayName = provider.DisplayName,
                BucketName = provider.BucketName,
                Region = provider.Region,
                ServiceUrl = provider.ServiceUrl,
                CdnDomain = provider.CdnDomain,
                PresignedUrlExpirationSeconds = provider.PresignedUrlExpirationSeconds
            };
        editProviderIsEnabled = provider.IsEnabled;
        editProviderForcePathStyle = provider.ForcePathStyle;
        editProviderUseHttps = provider.UseHttps;
        editProviderLogoPreview = provider.LogoUrl ?? GetDefaultProviderLogo(provider.Type);
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
        if (providerToEdit == null) return;

        isSubmitting = true;
        editDialogMessage = null;
        editProvider.IsEnabled = editProviderIsEnabled;
        editProvider.ForcePathStyle = editProviderForcePathStyle;
        editProvider.UseHttps = editProviderUseHttps;
        StateHasChanged();

        try
        {
            if (!string.IsNullOrEmpty(editProviderLogoPreview))
            {
                editProvider.LogoUrl = editProviderLogoPreview;
            }
            editProvider.Priority = editPriority;
            
            await ApiService.UpdateStorageProviderAsync(providerToEdit.Id, editProvider);
            editDialogSuccess = true;
            editDialogMessage = "存储服务更新成功！";
            await Task.Delay(500);
            CloseEditDialog();
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "StorageProviderPanel.UpdateProvider");
            editDialogMessage = $"更新失败: {ex.Message}";
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
        if (_selectedProviderType.HasValue)
        {
            newProviderLogoPreview = GetDefaultProviderLogo(_selectedProviderType.Value);
        }
        StateHasChanged();
    }

    private void HandleEditLogoPreviewError()
    {
        // 如果编辑时预览图片加载失败，回退到默认图标
        if (providerToEdit != null)
        {
            editProviderLogoPreview = GetDefaultProviderLogo(providerToEdit.Type);
        }
        StateHasChanged();
    }
}
