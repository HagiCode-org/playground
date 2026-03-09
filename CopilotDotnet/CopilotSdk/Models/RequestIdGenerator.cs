namespace CopilotSdk.Models;

public static class RequestIdGenerator
{
    public static string NewId(DateTimeOffset nowUtc)
    {
        return $"req-{nowUtc:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
    }
}
