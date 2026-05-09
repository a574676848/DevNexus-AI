using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Services.Storage;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class InputBox
{
    #region File Upload

    private async Task HandleFileUploadAsync(InputFileChangeEventArgs e)
    {
        if (_isUploading) return;
        _isUploading = true;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            const int maxFiles = 5;
            var files = e.GetMultipleFiles(maxFiles);

            foreach (var file in files)
            {
                if (file.Size > MaxFileSize)
                {
                    await NotificationService.ShowAsync("文件过大", $"文件 {file.Name} 超过大小限制 (20MB)");
                    continue;
                }

                if (!FileUploadService.IsFileSupported(file.Name))
                {
                    await NotificationService.ShowAsync("不支持的格式", $"不支持的文件类型: {file.Name}");
                    continue;
                }

                var smartDoc = await FileUploadService.ParseFileAsync(file, _selectedProviderId, SessionId);
                if (smartDoc != null)
                {
                    AddDocument(smartDoc, file.Name, DocumentSourceType.Uploaded);
                }
                else
                {
                    await NotificationService.ShowAsync("解析失败", $"文件 {file.Name} 发起解析失败");
                }
            }
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.HandleFileUploadAsync");
            await NotificationService.ShowAsync("操作异常", $"文件上传解析时发生错误: {ex.Message}");
        }
        finally
        {
            _isUploading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void RemoveAttachment(string file)
    {
        _attachments.Remove(file);
    }

    #endregion

}

