namespace IFlowSdk.Models;

public enum QueueOverflowStrategy
{
    DropOldest,
    DropNewest,
    Throw,
}

public enum ToolCallStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
}

public enum ToolCallConfirmationOutcome
{
    ProceedOnce,
    ProceedAlways,
    ProceedAlwaysServer,
    ProceedAlwaysTool,
    Cancel,
}

public enum ApprovalMode
{
    Default,
    Smart,
    Yolo,
    Plan,
}

public enum HookEventType
{
    PreToolUse,
    PostToolUse,
    Stop,
    SubagentStop,
    SetUpEnvironment,
}

public enum StopReason
{
    EndTurn,
    MaxTokens,
    Refusal,
    Cancelled,
}

public static class ToolCallConfirmationOutcomeExtensions
{
    public static string ToOptionId(this ToolCallConfirmationOutcome outcome)
    {
        return outcome switch
        {
            ToolCallConfirmationOutcome.ProceedOnce => "proceed_once",
            ToolCallConfirmationOutcome.ProceedAlways => "proceed_always",
            ToolCallConfirmationOutcome.ProceedAlwaysServer => "proceed_always_server",
            ToolCallConfirmationOutcome.ProceedAlwaysTool => "proceed_always_tool",
            ToolCallConfirmationOutcome.Cancel => "cancel",
            _ => "proceed_once",
        };
    }
}
