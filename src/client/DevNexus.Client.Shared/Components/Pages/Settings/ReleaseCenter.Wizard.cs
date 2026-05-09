using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

/// <summary>
/// 版本发布中心的发布向导流程。
/// </summary>
public partial class ReleaseCenter
{
    internal void OpenPublishWizard()
    {
        showPublishWizard = true;
        wizardStep = 1;
        wizardReleaseMode = string.IsNullOrWhiteSpace(importMetadataJson) ? "manual" : "import";
        wizardPublishRelease = importPublishRelease;
        wizardCreateRollout = importCreateRollout;
        wizardAcknowledgeRisks = false;
        wizardCompleted = false;
        wizardExecutionResults.Clear();
        wizardLastRelease = null;
        wizardLastRollout = null;

        if (wizardReleaseMode == "import")
        {
            SyncWizardImportDefaults();
        }
    }

    internal void ClosePublishWizard()
    {
        showPublishWizard = false;
        wizardCompleted = false;
    }

    internal bool HasImportMetadataJsonError => !string.IsNullOrWhiteSpace(GetImportMetadataJsonError());
    internal string ImportMetadataJsonStatusText => GetImportMetadataJsonError() ?? "JSON 格式有效";

    internal string? GetImportMetadataJsonError()
    {
        if (string.IsNullOrWhiteSpace(importMetadataJson))
        {
            return "请填写构建元数据 JSON";
        }

        try
        {
            JsonDocument.Parse(importMetadataJson);
            return null;
        }
        catch (JsonException ex)
        {
            return $"JSON 格式错误：第 {ex.LineNumber + 1} 行，第 {ex.BytePositionInLine + 1} 列";
        }
    }

    internal void FormatImportMetadataJson()
    {
        if (string.IsNullOrWhiteSpace(importMetadataJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(importMetadataJson);
            importMetadataJson = JsonSerializer.Serialize(document.RootElement, TemplateJsonOptions);
            ShowSuccessToast("JSON 已格式化");
        }
        catch (JsonException)
        {
            ShowErrorToast("JSON 格式不正确，无法格式化");
        }
    }

    internal Task HandleImportMetadataChangedAsync(string content)
    {
        importMetadataJson = content;
        return Task.CompletedTask;
    }

    internal string GetPlannedReleaseDisplayText()
    {
        if (releaseForm.ReleaseId.HasValue)
        {
            var currentRelease = releases.FirstOrDefault(item => item.ReleaseId == releaseForm.ReleaseId.Value);
            if (currentRelease != null)
            {
                return currentRelease.Version;
            }
        }

        if (!string.IsNullOrWhiteSpace(releaseForm.Version))
        {
            return releaseForm.Version;
        }

        return "请先完成版本信息";
    }

    internal string WizardPendingReleaseOptionValue
    {
        get
        {
            if (releaseForm.ReleaseId.HasValue)
            {
                return releaseForm.ReleaseId.Value.ToString();
            }

            if (wizardReleaseMode == "import" && TryGetImportMetadataPreview(out _))
            {
                return "__pending_release__";
            }

            return !string.IsNullOrWhiteSpace(releaseForm.Version) ? "__pending_release__" : string.Empty;
        }
    }

    internal string WizardPendingReleaseOptionLabel
    {
        get
        {
            if (wizardReleaseMode == "import" && TryGetImportMetadataPreview(out var importPreview))
            {
                return $"{importPreview.Version} / {importPreview.Channel}";
            }

            if (!string.IsNullOrWhiteSpace(releaseForm.Version))
            {
                var channel = string.IsNullOrWhiteSpace(releaseForm.Channel) ? "未指定渠道" : releaseForm.Channel.Trim();
                return $"{releaseForm.Version} / {channel}";
            }

            return "当前版本";
        }
    }

    internal bool TryGetImportMetadataPreview(out (string Version, string Channel) preview)
    {
        preview = default;
        if (!TryDeserializeImportMetadataRequest(out var request) || string.IsNullOrWhiteSpace(request.Version))
        {
            return false;
        }

        preview = (
            request.Version.Trim(),
            string.IsNullOrWhiteSpace(request.Channel) ? "stable" : request.Channel.Trim());
        return true;
    }

    internal bool TryDeserializeImportMetadataRequest(out ImportReleaseMetadataRequest request)
    {
        request = default!;
        if (string.IsNullOrWhiteSpace(importMetadataJson))
        {
            return false;
        }

        try
        {
            request = JsonSerializer.Deserialize<ImportReleaseMetadataRequest>(
                importMetadataJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            return request != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal void SyncWizardImportDefaults()
    {
        if (!TryDeserializeImportMetadataRequest(out var request))
        {
            return;
        }

        releaseForm.Version = string.IsNullOrWhiteSpace(request.Version) ? releaseForm.Version : request.Version.Trim();
        releaseForm.Channel = string.IsNullOrWhiteSpace(request.Channel) ? releaseForm.Channel : request.Channel.Trim();
        releaseForm.Title = string.IsNullOrWhiteSpace(request.Title) ? releaseForm.Title : request.Title.Trim();
        rolloutForm.ReleaseIdString = WizardPendingReleaseOptionValue;

        if (request.RolloutTemplate != null)
        {
            rolloutForm.Platform = string.IsNullOrWhiteSpace(request.RolloutTemplate.Platform) ? rolloutForm.Platform : request.RolloutTemplate.Platform.Trim();
            rolloutForm.Architecture = string.IsNullOrWhiteSpace(request.RolloutTemplate.Architecture) ? rolloutForm.Architecture : request.RolloutTemplate.Architecture.Trim();
            rolloutForm.Channel = string.IsNullOrWhiteSpace(request.RolloutTemplate.Channel) ? rolloutForm.Channel : request.RolloutTemplate.Channel.Trim();
            rolloutForm.MinimumSupportedVersion = request.RolloutTemplate.MinimumSupportedVersion?.Trim() ?? string.Empty;
            rolloutForm.RolloutPercent = request.RolloutTemplate.RolloutPercent;
            rolloutForm.AudienceRule = string.IsNullOrWhiteSpace(request.RolloutTemplate.AudienceRule) ? rolloutForm.AudienceRule : request.RolloutTemplate.AudienceRule.Trim();
            rolloutForm.ForceUpdate = request.RolloutTemplate.ForceUpdate;
            rolloutForm.Enabled = request.RolloutTemplate.Enabled;
        }
        else
        {
            rolloutForm.Channel = string.IsNullOrWhiteSpace(request.Channel) ? rolloutForm.Channel : request.Channel.Trim();
            var artifact = request.Artifacts.FirstOrDefault();
            if (artifact != null)
            {
                rolloutForm.Platform = string.IsNullOrWhiteSpace(artifact.Platform) ? rolloutForm.Platform : artifact.Platform.Trim();
                rolloutForm.Architecture = string.IsNullOrWhiteSpace(artifact.Architecture) ? rolloutForm.Architecture : artifact.Architecture.Trim();
            }
        }
    }

    internal void OpenImportMetadataDialog() => showImportMetadataExpanded = true;
    internal void CloseImportMetadataDialog() => showImportMetadataExpanded = false;

    internal void GoToWizardStep(int step)
    {
        var targetStep = Math.Clamp(step, 1, wizardCompleted ? 5 : 4);
        if (targetStep <= wizardStep)
        {
            wizardStep = targetStep;
            return;
        }

        for (var current = 1; current < targetStep; current++)
        {
            if (!CanProceedFromWizardStep(current))
            {
                return;
            }
        }

        wizardStep = targetStep;
    }

    internal void GoToNextWizardStep()
    {
        if (wizardStep < 4)
        {
            wizardStep++;
        }
    }

    internal void GoToPreviousWizardStep()
    {
        if (wizardStep > 1)
        {
            wizardStep--;
        }
    }

    internal IReadOnlyList<ArtifactFormModel> GetWizardArtifacts()
    {
        if (wizardReleaseMode == "import" && TryDeserializeImportMetadataRequest(out var request))
        {
            return request.Artifacts.Select(item => new ArtifactFormModel
            {
                Platform = item.Platform,
                Architecture = item.Architecture,
                PackageType = item.PackageType,
                FileName = item.FileName,
                FileSize = item.FileSize,
                Checksum = item.Checksum,
                Signature = item.Signature,
                DownloadUrl = item.DownloadUrl,
                StorageKey = item.StorageKey
            }).ToList();
        }

        return releaseForm.Artifacts;
    }

    internal RolloutDto? GetPreviewRolloutSource()
    {
        if (rolloutForm.RolloutId.HasValue)
        {
            return rollouts.FirstOrDefault(item => item.RolloutId == rolloutForm.RolloutId.Value);
        }

        if (!string.IsNullOrWhiteSpace(rolloutForm.ReleaseIdString) && Guid.TryParse(rolloutForm.ReleaseIdString, out var releaseId))
        {
            return rollouts.FirstOrDefault(item => item.ReleaseId == releaseId);
        }

        return null;
    }

    internal bool CanProceedFromWizardStep(int step) => step switch
    {
        1 => !GetWizardValidationErrors(1).Any(),
        2 => !GetWizardValidationErrors(2).Any(),
        3 => !GetWizardValidationErrors(3).Any(),
        4 => CanCompleteWizard(),
        _ => true
    };

    internal IReadOnlyList<string> GetWizardValidationErrors(int step)
    {
        var errors = new List<string>();
        switch (step)
        {
            case 1:
                if (wizardReleaseMode == "import")
                {
                    if (string.IsNullOrWhiteSpace(importMetadataJson))
                    {
                        errors.Add("请填写构建元数据 JSON。");
                    }
                    else if (HasImportMetadataJsonError)
                    {
                        errors.Add("构建元数据 JSON 格式不正确。");
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(releaseForm.Version)) errors.Add("请填写版本号。");
                    if (string.IsNullOrWhiteSpace(releaseForm.Channel)) errors.Add("请填写发布渠道。");
                    if (string.IsNullOrWhiteSpace(releaseForm.Title)) errors.Add("请填写版本标题。");
                }
                break;
            case 2:
                var artifacts = GetWizardArtifacts();
                if (!artifacts.Any())
                {
                    errors.Add("至少需要一个发布物。");
                    break;
                }

                if (artifacts.Any(item => string.IsNullOrWhiteSpace(item.Platform))) errors.Add("发布物缺少平台信息。");
                if (artifacts.Any(item => string.IsNullOrWhiteSpace(item.PackageType))) errors.Add("发布物缺少包类型。");
                if (artifacts.Any(item => string.IsNullOrWhiteSpace(item.DownloadUrl))) errors.Add("发布物缺少下载地址。");
                if (artifacts.Any(item => !string.IsNullOrWhiteSpace(item.DownloadUrl) && !Uri.TryCreate(item.DownloadUrl.Trim(), UriKind.Absolute, out _)))
                {
                    errors.Add("发布物下载地址格式不正确。");
                }
                break;
            case 3:
                if (!wizardCreateRollout)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(rolloutForm.ReleaseIdString) && !releaseForm.ReleaseId.HasValue) errors.Add("请选择目标版本。");
                if (string.IsNullOrWhiteSpace(rolloutForm.Platform)) errors.Add("请填写投放平台。");
                if (string.IsNullOrWhiteSpace(rolloutForm.Channel)) errors.Add("请填写投放渠道。");
                if (rolloutForm.RolloutPercent is < 0 or > 100) errors.Add("灰度比例必须在 0 到 100 之间。");
                if (!string.IsNullOrWhiteSpace(rolloutForm.StartsAtText) && !DateTime.TryParse(rolloutForm.StartsAtText, out _)) errors.Add("开始时间格式不正确。");
                if (!string.IsNullOrWhiteSpace(rolloutForm.EndsAtText) && !DateTime.TryParse(rolloutForm.EndsAtText, out _)) errors.Add("结束时间格式不正确。");
                break;
        }

        return errors;
    }

    internal IReadOnlyList<string> GetWizardExecutionChecklist()
    {
        var items = new List<string>();
        items.Add(wizardReleaseMode == "import" ? "导入 metadata，生成版本" : "保存手动填写的版本");
        items.Add(wizardPublishRelease ? "版本直接发布" : "版本保存为草稿");
        items.Add(wizardCreateRollout ? $"创建 {rolloutForm.Channel} 渠道投放，灰度 {rolloutForm.RolloutPercent}%" : "本次不创建投放");
        return items;
    }

    internal IReadOnlyList<string> GetWizardRiskWarnings()
    {
        var warnings = new List<string>();
        if (wizardReleaseMode == "manual")
        {
            if (string.IsNullOrWhiteSpace(releaseForm.ReleaseNotes)) warnings.Add("未填写发行说明，回看版本时缺少变更记录。");
            if (releaseForm.Artifacts.All(item => string.IsNullOrWhiteSpace(item.DownloadUrl))) warnings.Add("发布物缺少下载地址，客户端无法下载安装。");
        }

        if (wizardCreateRollout)
        {
            if (rolloutForm.RolloutPercent == 0) warnings.Add("灰度比例为 0%，不会有客户端命中。");
            if (rolloutForm.ForceUpdate && string.IsNullOrWhiteSpace(rolloutForm.MinimumSupportedVersion)) warnings.Add("已开启强制更新，但最低支持版本为空。");
            if (!rolloutForm.Enabled) warnings.Add("投放未启用，保存后不会立即生效。");
            if (rolloutForm.KillSwitchEnabled) warnings.Add("已开启熔断，规则保存后也不会生效。");
        }

        return warnings;
    }

    internal bool CanCompleteWizard()
    {
        var warnings = GetWizardRiskWarnings();
        return warnings.Count == 0 || wizardAcknowledgeRisks;
    }

    internal string BuildWizardSummary()
    {
        var releaseSummary = wizardLastRelease == null
            ? "版本结果：未识别。"
            : $"版本结果：{wizardLastRelease.Version}，渠道 {wizardLastRelease.Channel}，状态 {GetReleaseStatusText(wizardLastRelease.Status)}。";
        var rolloutSummary = wizardLastRollout == null
            ? "投放结果：本次未创建投放。"
            : $"投放结果：{wizardLastRollout.Platform}/{wizardLastRollout.Architecture}/{wizardLastRollout.Channel}，灰度 {wizardLastRollout.RolloutPercent}%，状态 {GetRolloutStatusText(wizardLastRollout)}。";

        return $"版本发布中心执行完成。{releaseSummary}{rolloutSummary}";
    }

    internal async Task CopyWizardSummaryAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", BuildWizardSummary());
            ShowSuccessToast("发版摘要已复制");
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.CopyWizardSummaryAsync");
            ShowErrorToast(ex.Message);
        }
    }

    internal async Task CopyVersionAsync()
    {
        if (wizardLastRelease == null)
        {
            return;
        }

        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", wizardLastRelease.Version);
            ShowSuccessToast("版本号已复制");
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.CopyVersionAsync");
            ShowErrorToast(ex.Message);
        }
    }

    internal async Task CopyRolloutSummaryAsync()
    {
        if (wizardLastRollout == null)
        {
            return;
        }

        var text = $"{wizardLastRollout.Platform}/{wizardLastRollout.Architecture}/{wizardLastRollout.Channel} | 灰度 {wizardLastRollout.RolloutPercent}% | {GetRolloutStatusText(wizardLastRollout)}";
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
            ShowSuccessToast("投放信息已复制");
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "ReleaseCenter.CopyRolloutSummaryAsync");
            ShowErrorToast(ex.Message);
        }
    }

    internal async Task CompleteWizardAsync()
    {
        wizardExecutionResults.Clear();
        wizardLastRelease = null;
        wizardLastRollout = null;
        if (wizardReleaseMode == "import")
        {
            importPublishRelease = wizardPublishRelease;
            importCreateRollout = wizardCreateRollout;
            await ImportMetadataAsync();
            wizardLastRelease = releaseForm.ReleaseId.HasValue ? releases.FirstOrDefault(item => item.ReleaseId == releaseForm.ReleaseId.Value) : null;
            wizardLastRollout = !string.IsNullOrWhiteSpace(rolloutForm.ReleaseIdString) ? rollouts.FirstOrDefault(item => item.ReleaseId.ToString() == rolloutForm.ReleaseIdString) : rollouts.FirstOrDefault();
            wizardExecutionResults.Add("已导入构建元数据。");
            wizardExecutionResults.Add(wizardPublishRelease ? "已按向导设置直接发布版本。" : "版本已导入为草稿状态。");
            if (wizardCreateRollout) wizardExecutionResults.Add("已按向导设置自动创建投放规则。");
        }
        else
        {
            await SaveReleaseAsync();
            wizardLastRelease = releaseForm.ReleaseId.HasValue ? releases.FirstOrDefault(item => item.ReleaseId == releaseForm.ReleaseId.Value) : null;
            wizardExecutionResults.Add("已保存手动填写的版本信息。");

            if (wizardPublishRelease && releaseForm.ReleaseId.HasValue)
            {
                await PublishReleaseAsync(releaseForm.ReleaseId.Value);
                wizardLastRelease = releases.FirstOrDefault(item => item.ReleaseId == releaseForm.ReleaseId.Value) ?? wizardLastRelease;
                wizardExecutionResults.Add("已将版本直接发布。");
            }

            if (wizardCreateRollout)
            {
                if ((string.IsNullOrWhiteSpace(rolloutForm.ReleaseIdString) || rolloutForm.ReleaseIdString == "__pending_release__") && releaseForm.ReleaseId.HasValue)
                {
                    rolloutForm.ReleaseIdString = releaseForm.ReleaseId.Value.ToString();
                }

                await SaveRolloutAsync();
                wizardLastRollout = !string.IsNullOrWhiteSpace(rolloutForm.ReleaseIdString) ? rollouts.FirstOrDefault(item => item.ReleaseId.ToString() == rolloutForm.ReleaseIdString) : null;
                wizardExecutionResults.Add("已创建投放规则。");
            }
        }

        activeTab = wizardCreateRollout ? RolloutsTab : ReleasesTab;
        wizardCompleted = true;
        wizardStep = 5;
    }

    internal void AutoFillRolloutFromArtifacts(ReleaseDto release)
    {
        rolloutForm.ReleaseIdString = release.ReleaseId.ToString();
        rolloutForm.Channel = string.IsNullOrWhiteSpace(release.Channel) ? rolloutForm.Channel : release.Channel.Trim();

        var artifact = release.Artifacts.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Platform) && !string.IsNullOrWhiteSpace(item.Architecture));
        if (artifact != null)
        {
            rolloutForm.Platform = artifact.Platform.Trim();
            rolloutForm.Architecture = artifact.Architecture.Trim();
        }
    }
}
