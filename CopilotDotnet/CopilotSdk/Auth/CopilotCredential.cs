namespace CopilotSdk.Auth;

public sealed record CopilotCredential(
    string? AccessToken,
    bool UseLoggedInUser,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc;
}
