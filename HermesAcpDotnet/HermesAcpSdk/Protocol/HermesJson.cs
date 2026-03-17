using System.Text.Json;
using System.Text.Json.Serialization;

namespace HermesAcpSdk.Protocol;

public static class HermesJson
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };
}
