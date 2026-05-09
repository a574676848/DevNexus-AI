using System.Text.Json;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Storage;

public partial class FileUploadService
{
    private async Task<SmartDocument?> ParseTextBytesAsync(byte[] bytes, string fileName, string contentType, FileAssetDto? uploadedAsset)
    {
        using var stream = new MemoryStream(bytes);
        var document = await ParseTextStreamAsync(stream, fileName);
        return AttachFileAssetMetadata(document, uploadedAsset, fileName, contentType, bytes.Length);
    }

    private async Task<SmartDocument?> ParseTableBytesAsync(
        byte[] bytes,
        string fileName,
        string contentType,
        FileAssetDto? uploadedAsset)
    {
        try
        {
            var extension = Path.GetExtension(fileName).ToLower();
            JsonElement parseResult;

            if (extension == ".csv")
            {
                var csvContent = global::System.Text.Encoding.UTF8.GetString(bytes);
                parseResult = await _js.InvokeAsync<JsonElement>(
                    "DevNexusFileParser.parseCSV",
                    new object?[] { csvContent, fileName });
            }
            else
            {
                parseResult = await _js.InvokeAsync<JsonElement>(
                    "DevNexusFileParser.parseExcel",
                    new object?[] { bytes, fileName });
            }

            if (parseResult.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
            {
                var smartDocJson = parseResult.GetProperty("smartDocument").GetRawText();
                var document = JsonSerializer.Deserialize<SmartDocument>(smartDocJson);
                return AttachFileAssetMetadata(document, uploadedAsset, fileName, contentType, bytes.Length);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 处理文本流（前端解析 - TXT/MD）
    /// </summary>
    public async Task<SmartDocument?> ParseTextStreamAsync(Stream stream, string fileName)
    {
        try
        {
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var parseResult = await _js.InvokeAsync<JsonElement>(
                "DevNexusFileParser.parseText",
                new object?[] { content, fileName });

            if (parseResult.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
            {
                var smartDocJson = parseResult.GetProperty("smartDocument").GetRawText();
                return JsonSerializer.Deserialize<SmartDocument>(smartDocJson);
            }

            var errorMsg = parseResult.TryGetProperty("errorMessage", out var errProp) ? errProp.GetString() : "未知错误";
            await _remoteLog.LogErrorAsync(new Exception(errorMsg), "FileUpload.ParseText.Failure", new Dictionary<string, object?>
            {
                ["FileName"] = fileName,
                ["ErrorMessage"] = errorMsg
            });
            return null;
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "FileUpload.ParseText.Exception", new Dictionary<string, object?>
            {
                ["FileName"] = fileName
            });
            return null;
        }
    }

    /// <summary>
    /// 处理表格流（前端解析 - CSV/Excel）
    /// 若前端解析失败，则尝试后端解析
    /// </summary>
    public async Task<SmartDocument?> ParseTableStreamAsync(Stream stream, string fileName)
    {
        try
        {
            var extension = Path.GetExtension(fileName).ToLower();
            JsonElement parseResult;

            if (extension == ".csv")
            {
                using var reader = new StreamReader(stream);
                var csvContent = await reader.ReadToEndAsync();
                parseResult = await _js.InvokeAsync<JsonElement>(
                    "DevNexusFileParser.parseCSV",
                    new object?[] { csvContent, fileName });
            }
            else
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                parseResult = await _js.InvokeAsync<JsonElement>(
                    "DevNexusFileParser.parseExcel",
                    new object?[] { bytes, fileName });
            }

            if (parseResult.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
            {
                var smartDocJson = parseResult.GetProperty("smartDocument").GetRawText();
                return JsonSerializer.Deserialize<SmartDocument>(smartDocJson);
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            return await ParseDocumentStreamAsync(stream, fileName);
        }
        catch (Exception ex)
        {
            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                return await ParseDocumentStreamAsync(stream, fileName);
            }
            catch (Exception fallbackEx)
            {
                await _remoteLog.LogErrorAsync(fallbackEx, "FileUpload.ParseTable.FallbackFailure", new Dictionary<string, object?>
                {
                    ["FileName"] = fileName,
                    ["OriginalError"] = ex.Message
                });
                return null;
            }
        }
    }
}
