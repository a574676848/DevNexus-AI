namespace DevNexus.ApiService.Auth;

internal sealed record AuthenticatedRequestInfo(
    bool IsAuthenticated,
    Guid? UserId,
    bool IsAdmin,
    string UserKey)
{
    public static readonly AuthenticatedRequestInfo Anonymous = new(
        false,
        null,
        false,
        "anonymous");
}
