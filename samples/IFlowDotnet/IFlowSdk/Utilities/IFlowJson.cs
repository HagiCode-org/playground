namespace IFlowSdk.Utilities;

internal static class IFlowJson
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static JsonElement ToElement<T>(T value)
    {
        return JsonSerializer.SerializeToElement(value, Default);
    }
}
