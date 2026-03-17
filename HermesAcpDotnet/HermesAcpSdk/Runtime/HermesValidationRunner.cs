using HermesAcpSdk.Configuration;
using HermesAcpSdk.Protocol;

namespace HermesAcpSdk.Runtime;

public sealed class HermesValidationRunner
{
    private readonly HermesAcpOptions _options;

    public HermesValidationRunner(HermesAcpOptions options)
    {
        _options = options;
    }

    public async Task<HermesRunResult> RunAsync(HermesRunRequest request, CancellationToken cancellationToken = default)
    {
        var profileName = string.IsNullOrWhiteSpace(request.ProfileName) ? _options.ActiveProfile : request.ProfileName.Trim();
        var profile = _options.ResolveProfile(request);
        await using var session = new HermesSessionRunner(profileName, profile, request);

        try
        {
            await session.ConnectAsync(cancellationToken);
            var prompt = string.IsNullOrWhiteSpace(request.Prompt) ? profile.DefaultPrompt : request.Prompt;
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                await session.PromptAsync(prompt, cancellationToken);
            }
        }
        catch (HermesProtocolException)
        {
        }
        catch (Exception)
        {
        }

        return await session.PersistSnapshotAsync(cancellationToken);
    }
}
