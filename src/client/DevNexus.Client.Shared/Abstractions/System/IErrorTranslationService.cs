namespace DevNexus.Client.Shared.Abstractions;


/// <summary>
/// 错误消息翻译服务接口
/// </summary>
public interface IErrorTranslationService
{
    /// <summary>
    /// 翻译 ASP.NET Core Identity 密码相关错误消息
    /// </summary>
    string TranslatePasswordError(string error);

    /// <summary>
    /// 翻译 ASP.NET Core Identity 用户相关错误消息
    /// </summary>
    string TranslateUserError(string error);

    /// <summary>
    /// 翻译通用 API 错误消息
    /// </summary>
    string TranslateApiError(string error);

    /// <summary>
    /// 翻译异常消息
    /// </summary>
    string Translate(Exception ex);
}

