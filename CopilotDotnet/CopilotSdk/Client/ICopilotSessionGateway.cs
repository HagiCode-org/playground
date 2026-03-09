using CopilotSdk.Models;

namespace CopilotSdk.Client;

public interface ICopilotSessionGateway
{
    Task<CopilotGatewayResponse> SendPromptAsync(CopilotGatewayRequest request, CancellationToken cancellationToken);
}
