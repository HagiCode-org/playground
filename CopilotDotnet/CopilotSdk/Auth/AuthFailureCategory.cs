namespace CopilotSdk.Auth;

public enum AuthFailureCategory
{
    MissingCredentials,
    ExpiredCredentials,
    RefreshFailed,
    Unauthorized,
    Unknown
}
