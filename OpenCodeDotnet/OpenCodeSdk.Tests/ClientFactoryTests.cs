using System.Net;
using System.Net.Http.Json;
using OpenCodeSdk.Generated;

namespace OpenCodeSdk.Tests;

public sealed class ClientFactoryTests
{
    [Fact]
    public void EncodeDirectoryHeaderValue_LeavesAsciiUntouched()
    {
        var value = OpenCodeClientFactory.EncodeDirectoryHeaderValue("/workspace/app");
        Assert.Equal("/workspace/app", value);
    }

    [Fact]
    public void EncodeDirectoryHeaderValue_EncodesNonAscii()
    {
        var value = OpenCodeClientFactory.EncodeDirectoryHeaderValue("/工作区/应用");
        Assert.Equal(Uri.EscapeDataString("/工作区/应用"), value);
    }

    [Fact]
    public async Task CreateClient_SendsDirectoryHeaderOnRequests()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new OpenCodeHealthResponse { Healthy = true, Version = "test" }),
        });

        var client = OpenCodeClientFactory.CreateClient(new OpenCodeClientOptions
        {
            BaseUri = new Uri("http://127.0.0.1:4123"),
            Directory = "/工作区/应用",
            HttpMessageHandler = handler,
        });

        var health = await client.Global.HealthAsync();

        Assert.True(health.Healthy);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.TryGetValues("x-opencode-directory", out var values));
        Assert.Equal(Uri.EscapeDataString("/工作区/应用"), values!.Single());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
