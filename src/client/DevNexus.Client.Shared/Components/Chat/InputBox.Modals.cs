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
    #region Modal Handlers

    private async Task HandleQuickCommandSelect(QuickCommand command)
    {
        _selectedQuickTool = command;
        _selectedSlashSkill = null;
        CloseSlashSkillPicker();
        _content = string.IsNullOrWhiteSpace(_content)
            ? command.Template
            : $"{command.Template.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{_content}";
        RequestTextareaSync(moveCaretToEnd: true);

        _showQuickCommandModal = false;
        StateHasChanged();
        await FocusInputAsync();
    }

    private async Task UpdateContent(string newContent)
    {
        _content = newContent;
        RequestTextareaSync(moveCaretToEnd: true);
        UpdateSlashSkillPicker();
        await Task.CompletedTask;
    }

    private void ClearSelectedQuickTool()
    {
        _selectedQuickTool = null;
    }

    private void ClearSelectedSlashSkill()
    {
        _selectedSlashSkill = null;
    }

    private async Task LoadAvailableSkillsAsync()
    {
        _isLoadingSkills = true;

        try
        {
            _availableSkills = await ApiService.GetAvailableSkillsAsync();
            RefreshFilteredSlashSkills(_slashSkillQuery);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "InputBox.LoadAvailableSkillsAsync");
            _availableSkills = new List<SkillDto>();
            _filteredSlashSkills.Clear();
        }
        finally
        {
            _isLoadingSkills = false;
        }
    }

    private void UpdateSlashSkillPicker()
    {
        if (IsTerminalWaitingForInput || HasActiveTerminalSession)
        {
            CloseSlashSkillPicker();
            return;
        }

        if (!LooksLikeSlashSkillSearch(_content))
        {
            CloseSlashSkillPicker();
            return;
        }

        var trimmed = _content.TrimStart();
        var nextQuery = trimmed.Length > 1 ? trimmed[1..].Trim() : string.Empty;
        _slashSkillQuery = nextQuery;
        _showSlashSkillPicker = true;
        RefreshFilteredSlashSkills(nextQuery);
        EnsureActiveSlashSkill();
        _shouldEnsureActiveSlashSkillVisible = false;
    }

    private void RefreshFilteredSlashSkills(string query)
    {
        if (_availableSkills.Count == 0)
        {
            _filteredSlashSkills.Clear();
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            _filteredSlashSkills = _availableSkills
                .OrderByDescending(skill => skill.Priority)
                .ThenBy(skill => skill.Name)
                .Take(SlashSkillMaxResults)
                .ToList();
            return;
        }

        _filteredSlashSkills = _availableSkills
            .Select(skill => new { Skill = skill, Score = CalculateSlashSkillScore(skill, query) })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Skill.Priority)
            .ThenBy(item => item.Skill.Name)
            .Take(SlashSkillMaxResults)
            .Select(item => item.Skill)
            .ToList();
    }

    private static int CalculateSlashSkillScore(SkillDto skill, string query)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return 1;
        }

        var score = 0;
        if (skill.Name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 120;
        }
        else if (skill.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 90;
        }

        if (skill.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 60;
        }

        if (skill.Tags.Any(tag => tag.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            score += 50;
        }

        if (skill.Plugins.Any(plugin => plugin.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
        {
            score += 40;
        }

        return score;
    }

    private async Task SelectSlashSkillAsync(SkillDto skill)
    {
        _selectedSlashSkill = skill;
        _selectedQuickTool = null;
        CloseSlashSkillPicker(resetActiveSelection: false);
        _activeSlashSkillName = skill.Name;
        _content = string.Empty;
        RequestTextareaSync();
        StateHasChanged();
        await FocusInputAsync();
    }

    private bool IsActiveSlashSkill(SkillDto skill)
    {
        return string.Equals(skill.Name, _activeSlashSkillName, StringComparison.OrdinalIgnoreCase);
    }

    private void MoveActiveSlashSkill(int offset)
    {
        var skills = FilteredSlashSkills;
        if (skills.Count == 0)
        {
            _activeSlashSkillName = null;
            return;
        }

        var currentIndex = skills
            .Select((skill, index) => new { skill.Name, Index = index })
            .FirstOrDefault(item => string.Equals(item.Name, _activeSlashSkillName, StringComparison.OrdinalIgnoreCase))?.Index ?? -1;

        var nextIndex = currentIndex < 0
            ? (offset >= 0 ? 0 : skills.Count - 1)
            : (currentIndex + offset + skills.Count) % skills.Count;

        _activeSlashSkillName = skills[nextIndex].Name;
        _shouldEnsureActiveSlashSkillVisible = true;
    }

    private void EnsureActiveSlashSkill()
    {
        var skills = FilteredSlashSkills;
        if (skills.Count == 0)
        {
            _activeSlashSkillName = null;
            return;
        }

        var hasActive = skills.Any(skill => string.Equals(skill.Name, _activeSlashSkillName, StringComparison.OrdinalIgnoreCase));
        if (!hasActive)
        {
            _activeSlashSkillName = skills[0].Name;
        }
    }

    private async Task TrySelectActiveSlashSkillAsync()
    {
        var activeSkill = FilteredSlashSkills.FirstOrDefault(skill =>
            string.Equals(skill.Name, _activeSlashSkillName, StringComparison.OrdinalIgnoreCase));

        if (activeSkill == null)
        {
            activeSkill = FilteredSlashSkills.FirstOrDefault();
        }

        if (activeSkill != null)
        {
            await SelectSlashSkillAsync(activeSkill);
        }
    }

    private async Task CloseSlashSkillPickerAsync()
    {
        CloseSlashSkillPicker();
        await FocusInputAsync();
        StateHasChanged();
    }

    private void CloseSlashSkillPicker(bool resetActiveSelection = true)
    {
        _showSlashSkillPicker = false;
        _slashSkillQuery = string.Empty;
        _shouldEnsureActiveSlashSkillVisible = false;
        _filteredSlashSkills.Clear();

        if (resetActiveSelection)
        {
            _activeSlashSkillName = null;
        }
    }

    private static bool LooksLikeSlashSkillSearch(string value)
    {
        return value.TrimStart().StartsWith("/", StringComparison.Ordinal);
    }

    private string BuildSkillTagLine(SkillDto skill)
    {
        var tagValues = skill.Tags.Take(2).ToList();
        if (skill.Plugins.Count > 0)
        {
            tagValues.AddRange(
                skill.Plugins
                    .Take(Math.Max(0, 2 - tagValues.Count))
                    .Select(plugin => $"插件：{TranslatePluginName(plugin)}"));
        }

        return tagValues.Count > 0 ? string.Join(" · ", tagValues.Distinct()) : skill.Scope;
    }

    private string BuildDefaultPlaceholder()
    {
        if (_selectedSlashSkill != null)
        {
            return $"技能 /{_selectedSlashSkill.Name} 已就绪，输入内容后会直接调用";
        }

        if (_selectedQuickTool != null)
        {
            return _selectedQuickTool.Placeholder;
        }

        return "输入消息，或键入 / 调用技能";
    }

    private static string TranslatePluginName(string pluginName)
    {
        return pluginName switch
        {
            "WebSearchPlugin" => "网络搜索工具",
            "CodeExecutionPlugin" => "代码执行工具",
            "KnowledgeBasePlugin" => "知识库工具",
            "SessionMemoryPlugin" => "会话记忆工具",
            "IntegrationPlugin" => "集成工具",
            "SwarmControlPlugin" => "Swarm 控制工具",
            _ => pluginName
        };
    }

    private Dictionary<string, object>? BuildComposerMetadata(List<string> attachmentUrls)
    {
        Dictionary<string, object>? metadata = null;

        if (_selectedQuickTool?.Metadata.Count > 0)
        {
            metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _selectedQuickTool.Metadata)
            {
                metadata[pair.Key] = pair.Value;
            }

            metadata["invocationSource"] = "tool-panel";
            metadata["toolCommand"] = _selectedQuickTool.Command;
        }

        if (_selectedSlashSkill != null)
        {
            metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            metadata["skillInvocationSource"] = "slash-picker";
        }

        if (attachmentUrls.Count > 0)
        {
            metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            metadata["attachmentUrls"] = attachmentUrls;
        }

        return metadata;
    }

    private static Dictionary<string, object>? MergeMetadataWithAttachmentUrls(
        Dictionary<string, object>? metadata,
        List<string> attachmentUrls)
    {
        Dictionary<string, object>? merged = null;
        if (metadata != null)
        {
            merged = new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase);
        }

        if (attachmentUrls.Count > 0)
        {
            merged ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            merged["attachmentUrls"] = attachmentUrls;
        }

        return merged;
    }

    #endregion

}

