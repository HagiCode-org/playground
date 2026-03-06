namespace CodexSdk;

public sealed class Codex
{
    private readonly CodexExec _exec;
    private readonly CodexOptions _options;

    public Codex(CodexOptions? options = null)
    {
        _options = options ?? new CodexOptions();
        _exec = new CodexExec(_options.CodexPathOverride, _options.Env, _options.Config);
    }

    public CodexThread StartThread(ThreadOptions? options = null)
    {
        return new CodexThread(_exec, _options, options ?? new ThreadOptions());
    }

    public CodexThread ResumeThread(string id, ThreadOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Thread id must not be empty.", nameof(id));
        }

        return new CodexThread(_exec, _options, options ?? new ThreadOptions(), id);
    }
}
