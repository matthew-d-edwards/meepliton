using System.Text.Json;

namespace Meepliton.Contracts;

public record PlayerInfo(
    string Id,
    string DisplayName,
    string? AvatarUrl,
    int SeatIndex
);

public record GameContext(
    JsonDocument CurrentState,
    JsonDocument Action,
    string PlayerId,
    string RoomId,
    int StateVersion
);
