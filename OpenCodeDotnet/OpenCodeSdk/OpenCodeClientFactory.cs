using OpenCodeSdk.Generated;

namespace OpenCodeSdk;

public static class OpenCodeClientFactory
{
    private static readonly Uri DefaultBaseUri = new("http://127.0.0.1:4096");

    public static OpenCodeClient CreateClient(OpenCodeClientOptions? options = null)
    {
        options ??= new OpenCodeClientOptions();

        var baseUri = NormalizeBaseUri(options.BaseUri ?? DefaultBaseUri);
        var handler = options.HttpMessageHandler ?? new HttpClientHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = baseUri,
            Timeout = options.Timeout ?? TimeSpan.FromSeconds(100),
        };

        if (!string.IsNullOrWhiteSpace(options.Directory))
        {
            httpClient.DefaultRequestHeaders.Remove("x-opencode-directory");
            httpClient.DefaultRequestHeaders.Add(
                "x-opencode-directory",
                EncodeDirectoryHeaderValue(options.Directory));
        }

        var generated = new OpenCodeGeneratedClient(httpClient, options.SerializerOptions);
        return new OpenCodeClient(generated, baseUri, options.Directory, options.Workspace);
    }

    public static string EncodeDirectoryHeaderValue(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return directory;
        }

        return directory.All(ch => ch <= 0x7f)
            ? directory
            : Uri.EscapeDataString(directory);
    }

    private static Uri NormalizeBaseUri(Uri input)
    {
        var text = input.ToString();
        return text.EndsWith("/", StringComparison.Ordinal) ? input : new Uri(text + "/");
    }
}
