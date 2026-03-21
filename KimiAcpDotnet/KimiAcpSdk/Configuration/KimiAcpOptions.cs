namespace KimiAcpSdk.Configuration;

public sealed class KimiAcpOptions
{
    public string ActiveProfile { get; set; } = "kimi-local";

    public Dictionary<string, KimiLaunchProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize(string basePath)
    {
        Profiles ??= new Dictionary<string, KimiLaunchProfile>(StringComparer.OrdinalIgnoreCase);
        if (Profiles.Count == 0)
        {
            Profiles[ActiveProfile] = new KimiLaunchProfile
            {
                Arguments = ["acp"],
            };
        }

        foreach (var profile in Profiles.Values)
        {
            profile.Arguments ??= [];
            profile.EnvironmentVariables ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            profile.Client ??= new KimiClientOptions();
            profile.Authentication ??= new KimiAuthenticationOptions();
            profile.SessionDefaults ??= new KimiSessionDefaults();
            profile.Artifacts ??= new KimiArtifactOptions();
            profile.ResolvePaths(basePath);
        }
    }

    public KimiLaunchProfile ResolveProfile(Runtime.KimiRunRequest request)
    {
        var profileName = string.IsNullOrWhiteSpace(request.ProfileName) ? ActiveProfile : request.ProfileName.Trim();
        if (!Profiles.TryGetValue(profileName, out var existing))
        {
            throw new InvalidOperationException($"Unknown Kimi profile '{profileName}'. Available profiles: {string.Join(", ", Profiles.Keys.OrderBy(static key => key))}");
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
