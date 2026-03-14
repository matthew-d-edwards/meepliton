using System.Text.Json;

namespace Meepliton.Api.Models;

public class Room
{
    public string  Id          { get; set; } = Guid.NewGuid().ToString();
    public string  GameId      { get; set; } = string.Empty;
    public string  HostId      { get; set; } = string.Empty;
    public string  JoinCode    { get; set; } = string.Empty;
    public RoomStatus Status   { get; set; } = RoomStatus.Waiting;
    public int     StateVersion { get; set; } = 0;
    public JsonDocument? GameState   { get; set; }
    public JsonDocument? GameOptions { get; set; }
    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
}

public enum RoomStatus { Waiting, InProgress, Finished }

public class RoomPlayer
{
    public string Id        { get; set; } = Guid.NewGuid().ToString();
    public string RoomId    { get; set; } = string.Empty;
    public string UserId    { get; set; } = string.Empty;
    public int    SeatIndex { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ActionLog
{
    public long   Id          { get; set; }
    public string RoomId      { get; set; } = string.Empty;
    public string PlayerId    { get; set; } = string.Empty;
    public JsonDocument Action { get; set; } = null!;
    public int    StateVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Game
{
    public string  Id          { get; set; } = string.Empty; // gameId
    public string  Name        { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public int     MinPlayers  { get; set; }
    public int     MaxPlayers  { get; set; }
    public string? ThumbnailUrl { get; set; }
}
