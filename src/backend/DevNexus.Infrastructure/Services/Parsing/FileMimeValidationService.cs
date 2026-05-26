using System.Text;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// 服务端文件类型校验：扩展名 + 声明 MIME + 文件魔数联合校验。
/// </summary>
public sealed class FileMimeValidationService
{
    private static readonly HashSet<string> SignificantMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "application/msword",
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "image/bmp"
    };

    private static readonly HashSet<string> GenericMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "application/octet-stream",
        "binary/octet-stream"
    };

    private static readonly Dictionary<string, string[]> ExtensionMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new[] { "application/pdf" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/zip" },
        [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip" },
        [".xls"] = new[] { "application/vnd.ms-excel", "application/octet-stream" },
        [".doc"] = new[] { "application/msword", "application/octet-stream" },
        [".csv"] = new[] { "text/csv", "text/plain", "application/vnd.ms-excel" },
        [".txt"] = new[] { "text/plain" },
        [".md"] = new[] { "text/markdown", "text/plain" },
        [".png"] = new[] { "image/png" },
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".gif"] = new[] { "image/gif" },
        [".webp"] = new[] { "image/webp" },
        [".bmp"] = new[] { "image/bmp" },
        [".cs"] = new[] { "text/x-csharp", "text/plain" },
        [".java"] = new[] { "text/x-java", "text/plain" },
        [".ts"] = new[] { "text/typescript", "text/plain" },
        [".tsx"] = new[] { "text/typescript", "text/plain" },
        [".js"] = new[] { "application/javascript", "text/javascript", "text/plain" },
        [".jsx"] = new[] { "text/javascript", "text/plain" },
        [".py"] = new[] { "text/x-python", "text/plain" }
    };

    public FileMimeValidationResult Validate(
        string fileName,
        string? declaredMimeType,
        ReadOnlySpan<byte> fileHeadBytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var normalizedDeclared = NormalizeMimeType(declaredMimeType);
        var detectedMime = DetectMimeByMagic(fileHeadBytes, extension);
        var expectedByExtension = ResolveExpectedMimeByExtension(extension);

        if (!string.IsNullOrWhiteSpace(expectedByExtension) &&
            IsSignificant(normalizedDeclared) &&
            !MimeMatches(expectedByExtension, normalizedDeclared))
        {
            return FileMimeValidationResult.Invalid(
                $"声明 MIME ({normalizedDeclared}) 与文件扩展名 ({extension}) 不匹配。");
        }

        if (RequiresBinaryMagic(extension))
        {
            if (string.IsNullOrWhiteSpace(detectedMime))
            {
                return FileMimeValidationResult.Invalid(
                    $"无法识别文件魔数，扩展名 {extension} 要求二进制签名校验。");
            }

            if (!string.IsNullOrWhiteSpace(expectedByExtension) &&
                !MimeMatches(expectedByExtension, detectedMime))
            {
                return FileMimeValidationResult.Invalid(
                    $"文件魔数识别为 {detectedMime}，与扩展名 {extension} 不匹配。");
            }
        }

        if (IsSignificant(normalizedDeclared) &&
            IsSignificant(detectedMime) &&
            !MimeMatches(normalizedDeclared, detectedMime))
        {
            return FileMimeValidationResult.Invalid(
                $"声明 MIME ({normalizedDeclared}) 与文件魔数 ({detectedMime}) 不一致。");
        }

        var effectiveMime = !string.IsNullOrWhiteSpace(expectedByExtension)
            ? expectedByExtension
            : !string.IsNullOrWhiteSpace(normalizedDeclared)
                ? normalizedDeclared
                : !string.IsNullOrWhiteSpace(detectedMime)
                    ? detectedMime
                    : "application/octet-stream";

        return FileMimeValidationResult.Valid(effectiveMime);
    }

    private static string NormalizeMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return string.Empty;
        }

        var index = mimeType.IndexOf(';');
        return (index >= 0 ? mimeType[..index] : mimeType).Trim().ToLowerInvariant();
    }

    private static bool RequiresBinaryMagic(string extension)
    {
        return extension is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    private static string ResolveExpectedMimeByExtension(string extension)
    {
        if (ExtensionMimeMap.TryGetValue(extension, out var accepted) && accepted.Length > 0)
        {
            return accepted[0];
        }

        return string.Empty;
    }

    private static bool IsSignificant(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        return SignificantMimeTypes.Contains(mimeType) || !GenericMimeTypes.Contains(mimeType);
    }

    private static bool MimeMatches(string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var sameZipFamily = (expected.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase) ||
                             expected.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase))
                            && actual.Equals("application/zip", StringComparison.OrdinalIgnoreCase);

        if (sameZipFamily)
        {
            return true;
        }

        if (expected.Equals("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase) &&
            actual.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // text/markdown 文件魔数无法与 text/plain 区分，视为兼容
        var isTextMarkdownFamily =
            (expected.Equals("text/markdown", StringComparison.OrdinalIgnoreCase) ||
             actual.Equals("text/markdown", StringComparison.OrdinalIgnoreCase)) &&
            (expected.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
             actual.Equals("text/plain", StringComparison.OrdinalIgnoreCase));
        if (isTextMarkdownFamily)
        {
            return true;
        }

        return false;
    }

    private static string DetectMimeByMagic(ReadOnlySpan<byte> bytes, string extension)
    {
        if (bytes.Length < 4)
        {
            return string.Empty;
        }

        if (bytes.Length >= 5 &&
            bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D)
        {
            return "application/pdf";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 6 &&
            bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
            bytes[3] == 0x38 && (bytes[4] == 0x39 || bytes[4] == 0x37) && bytes[5] == 0x61)
        {
            return "image/gif";
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return "image/bmp";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0 &&
            bytes[4] == 0xA1 && bytes[5] == 0xB1 && bytes[6] == 0x1A && bytes[7] == 0xE1)
        {
            return extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                ? "application/vnd.ms-excel"
                : "application/msword";
        }

        if (bytes.Length >= 4 &&
            bytes[0] == 0x50 && bytes[1] == 0x4B && (bytes[2] == 0x03 || bytes[2] == 0x05 || bytes[2] == 0x07) &&
            (bytes[3] == 0x04 || bytes[3] == 0x06 || bytes[3] == 0x08))
        {
            return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : "application/zip";
        }

        if (LooksLikeText(bytes))
        {
            return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "text/plain";
        }

        return string.Empty;
    }

    private static bool LooksLikeText(ReadOnlySpan<byte> bytes)
    {
        var sample = bytes[..Math.Min(bytes.Length, 1024)];
        int controlCount = 0;
        foreach (var b in sample)
        {
            if (b == 0)
            {
                return false;
            }

            if (b < 0x09 || (b > 0x0D && b < 0x20))
            {
                controlCount++;
            }
        }

        return controlCount < sample.Length / 10;
    }
}

public sealed class FileMimeValidationResult
{
    private FileMimeValidationResult(bool isValid, string effectiveMimeType, string? errorMessage)
    {
        IsValid = isValid;
        EffectiveMimeType = effectiveMimeType;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }

    public string EffectiveMimeType { get; }

    public string? ErrorMessage { get; }

    public static FileMimeValidationResult Valid(string effectiveMimeType) =>
        new(true, effectiveMimeType, null);

    public static FileMimeValidationResult Invalid(string message) =>
        new(false, string.Empty, message);
}
