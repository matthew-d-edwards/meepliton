namespace Meepliton.Contracts;

/// <summary>
/// A pre-game setup choice the host configures when creating a room — e.g. level,
/// difficulty, or variant. The platform renders these generically as labelled
/// dropdowns in the lobby and transports the chosen values back to the game via
/// the room options blob. The platform never interprets what a choice means.
/// </summary>
public record GameSetupOption(string Key, string Label, IReadOnlyList<GameSetupChoice> Choices);

/// <summary>A single selectable value within a <see cref="GameSetupOption"/>.</summary>
public record GameSetupChoice(string Value, string Label);
