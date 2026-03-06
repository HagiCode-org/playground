using System.Text.Json;

namespace CodexSdk;

public abstract record ThreadEvent(string Type);

public sealed record ThreadStartedEvent(string ThreadId)
    : ThreadEvent("thread.started");

public sealed record TurnStartedEvent()
    : ThreadEvent("turn.started");

public sealed record TurnCompletedEvent(Usage Usage)
    : ThreadEvent("turn.completed");

public sealed record ThreadError(string Message);

public sealed record TurnFailedEvent(ThreadError Error)
    : ThreadEvent("turn.failed");

public sealed record ItemStartedEvent(ThreadItem Item)
    : ThreadEvent("item.started");

public sealed record ItemUpdatedEvent(ThreadItem Item)
    : ThreadEvent("item.updated");

public sealed record ItemCompletedEvent(ThreadItem Item)
    : ThreadEvent("item.completed");

public sealed record ThreadErrorEvent(string Message)
    : ThreadEvent("error");

public sealed record UnknownThreadEvent(string EventType, JsonElement Raw)
    : ThreadEvent(EventType);
