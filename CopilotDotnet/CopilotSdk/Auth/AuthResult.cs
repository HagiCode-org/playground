namespace CopilotSdk.Auth;

public sealed class AuthResult
{
    private AuthResult(CopilotCredential? credential, AuthDiagnostic? diagnostic)
    {
        Credential = credential;
        Diagnostic = diagnostic;
    }

    public CopilotCredential? Credential { get; }

    public AuthDiagnostic? Diagnostic { get; }

    public bool Success => Credential is not null;

    public static AuthResult FromCredential(CopilotCredential credential) => new(credential, null);

    public static AuthResult FromDiagnostic(AuthDiagnostic diagnostic) => new(null, diagnostic);
}
