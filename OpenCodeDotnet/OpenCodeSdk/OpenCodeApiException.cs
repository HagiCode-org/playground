namespace OpenCodeSdk;

public sealed class OpenCodeApiException : Exception
{
    public OpenCodeApiException(HttpStatusCode statusCode, string? requestUri, string? responseBody)
        : base($"OpenCode API request to '{requestUri ?? "(unknown)"}' failed with status {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        ResponseBody = responseBody ?? string.Empty;
    }

    public HttpStatusCode StatusCode { get; }

    public string? RequestUri { get; }

    public string ResponseBody { get; }
}
