namespace KimiAcpSdk.Configuration;

public sealed class KimiSessionDefaults
{
    public string? Model { get; set; }

    public string? ModeId { get; set; }

    public Dictionary<string, string?> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public KimiSessionDefaults Clone()
    {
        return new KimiSessionDefaults
        {
            Model = Model,
            ModeId = ModeId,
            Metadata = new Dictionary<string, string?>(Metadata, StringComparer.OrdinalIgnoreCase),
        };
    }

    public IReadOnlyDictionary<string, object?> ToDictionary()
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(Model))
        {
            dictionary["model"] = Model;
        }

        if (!string.IsNullOrWhiteSpace(ModeId))
        {
            dictionary["modeId"] = ModeId;
        }

        foreach (var pair in Metadata)
        {
            dictionary[pair.Key] = pair.Value;
        }

        return dictionary;
    }
}
