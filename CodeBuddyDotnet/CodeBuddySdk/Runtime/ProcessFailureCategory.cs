namespace CodeBuddySdk.Runtime;

public enum ProcessFailureCategory
{
    None,
    MissingExecutable,
    Timeout,
    Authentication,
    ProcessExit,
    Protocol,
    Unknown,
}
