using IFlowSdk.Exceptions;
using IFlowSdk.Runtime;
using System.Net;
using System.Net.Sockets;

namespace IFlowSdk.Tests;

public sealed class IFlowProcessManagerTests
{
    [Fact]
    public void ResolveExecutablePath_UsesProvidedOverride()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var executable = Path.Combine(tempDirectory.FullName, OperatingSystem.IsWindows() ? "iflow.cmd" : "iflow");
        File.WriteAllText(executable, "echo test");

        var resolved = IFlowProcessManager.ResolveExecutablePath(executable);

        Assert.Equal(executable, resolved);
    }

    [Fact]
    public void ResolveExecutablePath_ThrowsForMissingOverride()
    {
        Assert.Throws<IFlowProcessException>(() => IFlowProcessManager.ResolveExecutablePath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void FindAvailablePort_ReturnsBindablePort()
    {
        var port = IFlowProcessManager.FindAvailablePort(12000);
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Assert.True(port >= 12000);
    }

    [Fact]
    public void IsEndpointListening_DetectsActiveListener()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Assert.True(IFlowProcessManager.IsEndpointListening($"ws://127.0.0.1:{port}/acp"));
    }
}
