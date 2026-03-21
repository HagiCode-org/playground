using KimiAcpSdk.Configuration;
using KimiAcpSdk.Protocol;

namespace KimiAcpSdk.Runtime;

public sealed class KimiValidationRunner
{
    private readonly KimiAcpOptions _options;

    public KimiValidationRunner(KimiAcpOptions options)
    {
        _options = options;
    }

    public async Task<KimiRunResult> RunAsync(KimiRunRequest request, CancellationToken cancellationToken = default)
    {
        var profileName = string.IsNullOrWhiteSpace(request.ProfileName) ? _options.ActiveProfile : request.ProfileName.Trim();
        var profile = _options.ResolveProfile(request);
        await using var session = new KimiSessionRunner(profileName, profile, request);

        try
        {
            await session.ConnectAsync(cancellationToken);
            var prompt = string.IsNullOrWhiteSpace(request.Prompt) ? profile.DefaultPrompt : request.Prompt;
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                await session.PromptAsync(prompt, cancellationToken);
            }
        }
        catch (KimiProtocolException)
        {
        }
        catch (Exception)
        {
        }

        return await session.PersistSnapshotAsync(cancellationToken);
    }
}
