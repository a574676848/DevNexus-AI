using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

/// <summary>
/// 版本发布中心的发布与投放操作。
/// </summary>
public partial class ReleaseCenter
{
    private void ResetReleaseForm()
    {
        releaseForm = ReleaseFormModel.CreateEmpty();
    }

    internal void AddArtifact()
    {
        releaseForm.Artifacts.Add(new ArtifactFormModel());
    }

    private void RemoveArtifact(ArtifactFormModel artifact)
    {
        if (releaseForm.Artifacts.Count <= 1)
        {
            return;
        }

        releaseForm.Artifacts.Remove(artifact);
    }

    private void EditRelease(ReleaseDto release)
    {
        releaseForm = ReleaseFormModel.FromDto(release);
        SwitchTab(ReleasesTab);
    }

    private void OpenReleaseEditor(ReleaseDto release)
    {
        EditRelease(release);
        showPublishWizard = true;
        wizardStep = 1;
        wizardReleaseMode = "manual";
        wizardPublishRelease = UpdateReleaseStatusExtensions.Parse(release.Status) == UpdateReleaseStatus.Published;
        wizardCreateRollout = false;
        wizardAcknowledgeRisks = false;
        wizardCompleted = false;
        wizardExecutionResults.Clear();
        wizardLastRelease = null;
        wizardLastRollout = null;
    }

    private async Task SaveReleaseAsync()
    {
        isSaving = true;
        try
        {
            var saved = await ApiService.SaveReleaseAsync(releaseForm.ToRequest());
            if (saved != null)
            {
                await RefreshAsync();
                EditRelease(saved);
                AutoFillRolloutFromArtifacts(saved);
                ShowSuccessToast("版本已保存");
            }
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.SaveReleaseAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task ImportMetadataAsync()
    {
        isSaving = true;
        try
        {
            if (string.IsNullOrWhiteSpace(importMetadataJson))
            {
                throw new InvalidOperationException("请先填写构建元数据。");
            }

            var request = JsonSerializer.Deserialize<ImportReleaseMetadataRequest>(importMetadataJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException("构建元数据不能为空。");

            request.PublishRelease = importPublishRelease;
            request.CreateRollout = importCreateRollout;

            var result = await ApiService.ImportReleaseMetadataAsync(request);
            if (result != null)
            {
                await RefreshAsync();
                EditRelease(result.Release);
                AutoFillRolloutFromArtifacts(result.Release);
                if (result.Rollout != null)
                {
                    EditRollout(result.Rollout);
                }

                ShowSuccessToast("构建元数据已导入");
            }
        }
        catch (JsonException ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.ImportMetadataAsync");
            ShowErrorToast("构建元数据格式不正确，请检查 JSON。");
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.ImportMetadataAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task PublishReleaseAsync(Guid releaseId)
    {
        await RunReleaseActionAsync(() => ApiService.PublishReleaseAsync(releaseId), "版本已发布");
    }

    private async Task ArchiveReleaseAsync(Guid releaseId)
    {
        await RunReleaseActionAsync(() => ApiService.ArchiveReleaseAsync(releaseId), "版本已归档");
    }

    private async Task DeleteReleaseAsync(Guid releaseId, string title)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", $"删除版本“{title}”后无法恢复。是否继续？");
        if (!confirmed)
        {
            return;
        }

        isSaving = true;
        try
        {
            await ApiService.DeleteReleaseAsync(releaseId);
            if (releaseForm.ReleaseId == releaseId)
            {
                ResetReleaseForm();
            }

            if (rolloutForm.ReleaseIdString == releaseId.ToString())
            {
                ResetRolloutForm();
            }

            await RefreshAsync();
            ShowSuccessToast("版本已删除");
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.DeleteReleaseAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task RunReleaseActionAsync(Func<Task<ReleaseDto?>> action, string successMessage)
    {
        isSaving = true;
        try
        {
            await action();
            await RefreshAsync();
            ShowSuccessToast(successMessage);
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.RunReleaseActionAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    private void ResetRolloutForm()
    {
        rolloutForm = RolloutFormModel.CreateEmpty();
        previewResult = null;
    }

    internal void LoadImportMetadataTemplate(string templateType = "stable")
    {
        var isGray = string.Equals(templateType, "gray", StringComparison.OrdinalIgnoreCase);
        var version = isGray ? "1.2.0-beta.1" : "1.2.0";
        var channel = isGray ? "beta" : "stable";

        var template = new ImportReleaseMetadataRequest
        {
            Version = version,
            Channel = channel,
            Title = isGray ? "1.2 灰度发布" : "1.2 正式发布",
            ReleaseNotes = isGray ? "灰度验证版本。" : "正式发布版本。",
            PublishRelease = !isGray,
            CreateRollout = true,
            Artifacts =
            [
                new ImportReleaseArtifactMetadata
                {
                    Platform = "desktop-windows",
                    Architecture = "x64",
                    PackageType = "installer",
                    FileName = $"DevNexus-{version}-win-x64.exe",
                    DownloadUrl = $"https://downloads.example.com/{version}/DevNexus-win-x64.exe",
                    FileSize = 154_234_112,
                    Checksum = "sha256:xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
                    Signature = "sig:windows-authenticode",
                    StorageKey = $"releases/{version}/DevNexus-win-x64.exe"
                }
            ],
            RolloutTemplate = new ImportReleaseRolloutTemplate
            {
                Platform = "desktop-windows",
                Architecture = "x64",
                Channel = channel,
                MinimumSupportedVersion = isGray ? "1.1.0" : "1.0.0",
                RolloutPercent = isGray ? 10 : 100,
                AudienceRule = isGray ? "tenant in ['pilot-a', 'pilot-b']" : "all",
                ForceUpdate = false,
                Enabled = true
            }
        };

        importMetadataJson = JsonSerializer.Serialize(template, TemplateJsonOptions);
    }

    private void EditRollout(RolloutDto rollout)
    {
        rolloutForm = RolloutFormModel.FromDto(rollout);
        previewResult = null;
        SwitchTab(RolloutsTab);
    }

    private void OpenRolloutEditor(RolloutDto rollout)
    {
        EditRollout(rollout);
        showPublishWizard = true;
        wizardStep = 3;
        wizardReleaseMode = "manual";
        wizardPublishRelease = false;
        wizardCreateRollout = true;
        wizardAcknowledgeRisks = false;
        wizardCompleted = false;
        wizardExecutionResults.Clear();
        wizardLastRelease = releases.FirstOrDefault(item => item.ReleaseId == rollout.ReleaseId);
        wizardLastRollout = rollout;
    }

    private async Task SaveRolloutAsync()
    {
        isSaving = true;
        try
        {
            var saved = await ApiService.SaveRolloutAsync(rolloutForm.ToRequest());
            if (saved != null)
            {
                await RefreshAsync();
                EditRollout(saved);
                ShowSuccessToast("投放已保存");
            }
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.SaveRolloutAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task PauseRolloutAsync(Guid rolloutId)
    {
        await RunRolloutActionAsync(() => ApiService.PauseRolloutAsync(rolloutId), "投放已暂停");
    }

    private async Task ResumeRolloutAsync(Guid rolloutId)
    {
        await RunRolloutActionAsync(() => ApiService.ResumeRolloutAsync(rolloutId), "投放已恢复");
    }

    private async Task RollbackRolloutAsync(Guid rolloutId)
    {
        await RunRolloutActionAsync(() => ApiService.RollbackRolloutAsync(rolloutId), "投放已回滚");
    }

    private async Task DeleteRolloutAsync(Guid rolloutId, string releaseVersion)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", $"删除版本“{releaseVersion}”对应的投放后无法恢复。是否继续？");
        if (!confirmed)
        {
            return;
        }

        isSaving = true;
        try
        {
            await ApiService.DeleteRolloutAsync(rolloutId);
            if (rolloutForm.RolloutId == rolloutId)
            {
                ResetRolloutForm();
            }

            await RefreshAsync();
            ShowSuccessToast("投放已删除");
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.DeleteRolloutAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task PreviewRolloutAsync(RolloutDto rollout)
    {
        EditRollout(rollout);
        await PreviewManifestAsync();
    }

    private async Task RunRolloutActionAsync(Func<Task<RolloutDto?>> action, string successMessage)
    {
        isSaving = true;
        try
        {
            await action();
            await RefreshAsync();
            ShowSuccessToast(successMessage);
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.RunRolloutActionAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task PreviewManifestAsync()
    {
        isSaving = true;
        try
        {
            if (string.IsNullOrWhiteSpace(rolloutForm.ReleaseIdString) && LatestRollout != null)
            {
                rolloutForm = RolloutFormModel.FromDto(LatestRollout);
            }

            previewResult = await ApiService.PreviewRolloutAsync(new UpdateManifestRequest
            {
                Platform = string.IsNullOrWhiteSpace(rolloutForm.Platform) ? "desktop-windows" : rolloutForm.Platform.Trim(),
                Architecture = string.IsNullOrWhiteSpace(rolloutForm.Architecture) ? "any" : rolloutForm.Architecture.Trim(),
                Channel = string.IsNullOrWhiteSpace(rolloutForm.Channel) ? "stable" : rolloutForm.Channel.Trim(),
                CurrentVersion = string.IsNullOrWhiteSpace(rolloutForm.MinimumSupportedVersion)
                    ? "0.0.0"
                    : rolloutForm.MinimumSupportedVersion.Trim(),
                InstallationId = "release-center-preview",
                TenantId = "preview",
                ClientCapabilities = new List<string> { "preview" }
            });
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.PreviewManifestAsync");
            ShowErrorToast(ex.Message);
        }
        finally
        {
            isSaving = false;
        }
    }
}
