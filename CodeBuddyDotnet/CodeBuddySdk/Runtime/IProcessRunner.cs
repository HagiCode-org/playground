namespace CodeBuddySdk.Runtime;

public interface IProcessRunner
{
    Task<RawProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}
