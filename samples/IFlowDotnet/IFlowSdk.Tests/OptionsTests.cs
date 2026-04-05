using IFlowSdk.Client;
using IFlowSdk.Models;
using IFlowSdk.Protocol;
using IFlowSdk.Runtime;

namespace IFlowSdk.Tests;

public sealed class OptionsTests
{
    [Fact]
    public void DefaultOptions_ValidateSuccessfully()
    {
        var options = new IFlowOptions();

        options.Validate();

        Assert.Equal(IFlowOptions.DefaultUrl, options.ResolvedUrl);
        Assert.Equal(ApprovalMode.Yolo, options.ApprovalMode);
    }

    [Fact]
    public void InvalidUrl_ThrowsArgumentException()
    {
        var options = new IFlowOptions { Url = "http://not-a-websocket" };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void ApprovalOutcome_MapsToExpectedOptionId()
    {
        Assert.Equal("proceed_once", ToolCallConfirmationOutcome.ProceedOnce.ToOptionId());
        Assert.Equal("proceed_always", ToolCallConfirmationOutcome.ProceedAlways.ToOptionId());
        Assert.Equal("proceed_always_server", ToolCallConfirmationOutcome.ProceedAlwaysServer.ToOptionId());
        Assert.Equal("proceed_always_tool", ToolCallConfirmationOutcome.ProceedAlwaysTool.ToOptionId());
        Assert.Equal("cancel", ToolCallConfirmationOutcome.Cancel.ToOptionId());
    }

    [Fact]
    public void BuildPrompt_UsesTextAndResourceLinks()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "hello");
        try
        {
            var prompt = AcpMessageSerializer.BuildPrompt("hi", new[] { tempFile });

            Assert.Equal(2, prompt.Count);
            var textBlock = Assert.IsType<Dictionary<string, object?>>(prompt[0]);
            Assert.Equal("text", textBlock["type"]);
            var fileBlock = Assert.IsType<Dictionary<string, object?>>(prompt[1]);
            Assert.Equal("resource_link", fileBlock["type"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
