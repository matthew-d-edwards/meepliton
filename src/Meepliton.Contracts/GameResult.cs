using System.Text.Json;

namespace Meepliton.Contracts;

public record GameResult(
    JsonDocument NewState,
    string? RejectionReason = null,
    GameEffect[] Effects = default!
)
{
    public GameEffect[] Effects { get; init; } = Effects ?? [];
}

// Side effects the platform can act on after a successful action
public abstract record GameEffect;
public record GameOverEffect(string? WinnerId) : GameEffect;
public record NotifyEffect(string PlayerId, string Message) : GameEffect;
