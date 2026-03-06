namespace CodexSdk;

public abstract record UserInputItem;

public sealed record TextInput(string Text) : UserInputItem;

public sealed record LocalImageInput(string Path) : UserInputItem;

internal sealed record NormalizedInput(string Prompt, IReadOnlyList<string> Images);
