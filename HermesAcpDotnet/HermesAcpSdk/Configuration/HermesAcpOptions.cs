namespace HermesAcpSdk.Configuration;

public sealed class HermesAcpOptions
{
    public string ActiveProfile { get; set; } = "hermes-local";

    public Dictionary<string, HermesLaunchProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize(string basePath)
    {
        Profiles ??= new Dictionary<string, HermesLaunchProfile>(StringComparer.OrdinalIgnoreCase);
        if (Profiles.Count == 0)
        {
            Profiles[ActiveProfile] = new HermesLaunchProfile
            {
                Arguments = ["acp"],
            };
        }

        foreach (var profile in Profiles.Values)
        {
            profile.Arguments ??= [];
            profile.EnvironmentVariables ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            profile.Client ??= new HermesClientOptions();
            profile.Authentication ??= new HermesAuthenticationOptions();
            profile.SessionDefaults ??= new HermesSessionDefaults();
            profile.Artifacts ??= new HermesArtifactOptions();
            profile.ResolvePaths(basePath);
        }
    }

    public HermesLaunchProfile ResolveProfile(Runtime.HermesRunRequest request)
    {
        var profileName = string.IsNullOrWhiteSpace(request.ProfileName) ? ActiveProfile : request.ProfileName.Trim();
        if (!Profiles.TryGetValue(profileName, out var existing))
        {
            throw new InvalidOperationException($"Unknown Hermes profile '{profileName}'. Available profiles: {string.Join(", ", Profiles.Keys.OrderBy(static key => key))}");
        }

        var resolved = existing.Clone();
        if (request.ArgumentOverrides.Count > 0)
        {
            resolved.Arguments = [.. request.ArgumentOverrides];
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectoryOverride))
        {
            resolved.WorkingDirectory = Path.GetFullPath(request.WorkingDirectoryOverride);
        }

        if (!string.IsNullOrWhiteSpace(request.ArtifactOutputOverride))
        {
            resolved.Artifacts.RunStorePath = Path.GetFullPath(request.ArtifactOutputOverride);
        }

        if (!string.IsNullOrWhiteSpace(request.AuthMethodOverride))
        {
            resolved.Authentication.PreferredMethodId = request.AuthMethodOverride;
        }

        foreach (var pair in request.EnvironmentOverrides)
        {
            resolved.EnvironmentVariables[pair.Key] = pair.Value;
        }

        return resolved;
    }
}
