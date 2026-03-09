namespace CopilotSdk.Auth;

public interface ICopilotCredentialProvider
{
    Task<CopilotCredential> AcquireAsync(CancellationToken cancellationToken);

    Task<CopilotCredential> RefreshAsync(CopilotCredential current, CancellationToken cancellationToken);
}
