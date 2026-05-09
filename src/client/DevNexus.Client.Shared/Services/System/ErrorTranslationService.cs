using DevNexus.Client.Shared.Abstractions;
namespace DevNexus.Client.Shared.Services.System;
/// <summary>
/// 错误消息翻译服务实现
/// 将英文错误消息翻译为中文
/// </summary>
public class ErrorTranslationService : IErrorTranslationService
{
    /// <inheritdoc />
    public string TranslatePasswordError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "操作失败";

        // ASP.NET Core Identity 密码相关错误消息翻译
        return error switch
        {
            // 当前密码验证错误
            var e when e.Contains("Incorrect password", StringComparison.OrdinalIgnoreCase)
                => "当前密码错误",
            var e when e.Contains("Password incorrect", StringComparison.OrdinalIgnoreCase)
                => "当前密码错误",
            var e when e.Contains("PasswordMismatch", StringComparison.OrdinalIgnoreCase)
                => "当前密码错误",

            // 密码复杂度要求
            var e when e.Contains("Passwords must be at least", StringComparison.OrdinalIgnoreCase)
                => ExtractPasswordLengthError(e),
            var e when e.Contains("PasswordTooShort", StringComparison.OrdinalIgnoreCase)
                => "密码长度不足",
            var e when e.Contains("Passwords must have at least one non alphanumeric", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个特殊字符（如 !@#$%）",
            var e when e.Contains("PasswordRequiresNonAlphanumeric", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个特殊字符（如 !@#$%）",
            var e when e.Contains("Passwords must have at least one digit", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个数字",
            var e when e.Contains("PasswordRequiresDigit", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个数字",
            var e when e.Contains("Passwords must have at least one uppercase", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个大写字母",
            var e when e.Contains("PasswordRequiresUpper", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个大写字母",
            var e when e.Contains("Passwords must have at least one lowercase", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个小写字母",
            var e when e.Contains("PasswordRequiresLower", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含至少一个小写字母",
            var e when e.Contains("must have at least", StringComparison.OrdinalIgnoreCase) && e.Contains("unique chars", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含更多不同的字符",
            var e when e.Contains("PasswordRequiresUniqueChars", StringComparison.OrdinalIgnoreCase)
                => "密码必须包含更多不同的字符",

            // 已有中文消息直接返回
            var e when ContainsChinese(e) => e,

            // 其他情况返回通用提示
            _ => "密码修改失败，请检查输入是否正确"
        };
    }

    /// <inheritdoc />
    public string TranslateUserError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "操作失败";

        return error switch
        {
            // 用户名相关
            var e when e.Contains("UserName", StringComparison.OrdinalIgnoreCase) && e.Contains("taken", StringComparison.OrdinalIgnoreCase)
                => "用户名已被占用",
            var e when e.Contains("DuplicateUserName", StringComparison.OrdinalIgnoreCase)
                => "用户名已被占用",
            var e when e.Contains("InvalidUserName", StringComparison.OrdinalIgnoreCase)
                => "用户名格式无效",

            // 邮箱相关
            var e when e.Contains("Email", StringComparison.OrdinalIgnoreCase) && e.Contains("taken", StringComparison.OrdinalIgnoreCase)
                => "邮箱已被占用",
            var e when e.Contains("DuplicateEmail", StringComparison.OrdinalIgnoreCase)
                => "邮箱已被占用",
            var e when e.Contains("InvalidEmail", StringComparison.OrdinalIgnoreCase)
                => "邮箱格式无效",

            // 用户状态相关
            var e when e.Contains("not found", StringComparison.OrdinalIgnoreCase) || e.Contains("用户不存在")
                => "用户不存在",
            var e when e.Contains("disabled", StringComparison.OrdinalIgnoreCase) || e.Contains("locked", StringComparison.OrdinalIgnoreCase)
                => "用户已被禁用",

            // 已有中文消息直接返回
            var e when ContainsChinese(e) => e,

            // 其他情况
            _ => "用户操作失败，请稍后重试"
        };
    }

    /// <inheritdoc />
    public string TranslateApiError(string error)
    {
        if (string.IsNullOrEmpty(error)) return "请求失败";

        return error switch
        {
            // 网络相关
            var e when e.Contains("network", StringComparison.OrdinalIgnoreCase) || e.Contains("connection", StringComparison.OrdinalIgnoreCase)
                => "网络连接失败，请检查网络设置",
            var e when e.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                => "请求超时，请稍后重试",

            // 权限相关
            var e when e.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) || e.Contains("401")
                => "登录已过期，请重新登录",
            var e when e.Contains("forbidden", StringComparison.OrdinalIgnoreCase) || e.Contains("403")
                => "没有权限执行此操作",
            var e when e.Contains("not found", StringComparison.OrdinalIgnoreCase) || e.Contains("404")
                => "请求的资源不存在",

            // 服务器错误
            var e when e.Contains("500") || e.Contains("internal server", StringComparison.OrdinalIgnoreCase)
                => "服务器内部错误，请稍后重试",
            var e when e.Contains("502") || e.Contains("503") || e.Contains("504")
                => "服务暂时不可用，请稍后重试",

            // 已有中文消息直接返回
            var e when ContainsChinese(e) => e,

            // 其他情况
            _ => "操作失败，请稍后重试"
        };
    }

    /// <inheritdoc />
    public string Translate(Exception ex)
    {
        if (ex == null) return "未知错误";

        // 如果是 API 错误
        if (ex.Message.Contains("API", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("status code", StringComparison.OrdinalIgnoreCase))
        {
            return TranslateApiError(ex.Message);
        }

        // 尝试从 InnerException 获取更多信息
        if (ex.InnerException != null)
        {
            return Translate(ex.InnerException);
        }

        return TranslateApiError(ex.Message);
    }

    /// <summary>
    /// 从密码长度错误消息中提取具体要求
    /// </summary>
    private static string ExtractPasswordLengthError(string error)
    {
        // 尝试提取数字
        var digits = new string(error.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var length))
        {
            return $"密码长度至少需要 {length} 个字符";
        }
        return "密码长度不足";
    }

    /// <summary>
    /// 检查字符串是否包含中文字符
    /// </summary>
    private static bool ContainsChinese(string text)
    {
        return text.Any(c => c >= 0x4E00 && c <= 0x9FFF);
    }
}
