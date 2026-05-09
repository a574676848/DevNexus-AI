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
    #region Paste Handling

    private async Task HandlePasteAsync(ClipboardEventArgs e)
    {
        try
        {
            var pastedText = await JS.InvokeAsync<string>("navigator.clipboard.readText");
            if (string.IsNullOrEmpty(pastedText)) return;

            var lineCount = pastedText.Split('\n').Length;
            var charCount = pastedText.Length;

            // 检测是否为纯 URL - 如果是则作为纯文本保留，让 LLM 处理
            if (lineCount == 1 && Uri.TryCreate(pastedText.Trim(), UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // URL 会被当作普通文本，让LLM主动调用 ReadWebpage 函数
                _content = pastedText.Trim();
                RequestTextareaSync(moveCaretToEnd: true);
                return;
            }

            if (lineCount > MaxLinesThreshold || charCount > MaxCharsThreshold)
            {
                await CreatePastedDocumentAsync(pastedText);
            }
        }
        catch { /* 忽略剪贴板读取失败 */ }
    }

    private async Task CreatePastedDocumentAsync(string text)
    {
        _pastedDocumentCounter++;
        var fileName = $"pasted-content-{_pastedDocumentCounter}.txt";
        var smartDoc = FileUploadService.CreateFromPastedText(text, fileName);

        _pastedDocuments.Add(new PastedDocument
        {
            SmartDocument = smartDoc,
            SourceType = DocumentSourceType.Pasted
        });

        AdjustHeightForDocuments();
        _content = "";
        RequestTextareaSync();
        StateHasChanged();
    }

    private void RemovePastedDocument(Guid documentId)
    {
        _pastedDocuments.RemoveAll(d => d.Id == documentId);
        AdjustHeightForDocuments();
        EnsureArtifactStatusPolling();
    }

    #endregion

}

