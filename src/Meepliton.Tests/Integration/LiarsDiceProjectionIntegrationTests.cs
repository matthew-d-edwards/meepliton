// AC-7: Integration test skeleton — two connected clients assert payload isolation.
//
// Full wiring requires:
//   - Microsoft.AspNetCore.Mvc.Testing package reference in Meepliton.Tests.csproj
//   - A project reference to Meepliton.Api
//   - A test-specific in-memory database (SQLite or EF InMemory)
//   - Two authenticated SignalR HubConnection instances
//
// The SignalR client package (Microsoft.AspNetCore.SignalR.Client) and
// WebApplicationFactory are NOT yet referenced by this project.
// When those prerequisites are added, replace each TODO section with real code.

using FluentAssertions;
using Xunit;

// TODO: add using Microsoft.AspNetCore.Mvc.Testing;
// TODO: add using Microsoft.AspNetCore.SignalR.Client;
// TODO: add using Meepliton.Api;           (requires project reference)
// TODO: add using Meepliton.Games.LiarsDice.Models;

namespace Meepliton.Tests.Integration;

/// <summary>
/// AC-7: Stand up the API in-process, authenticate two users, both join the same room,
/// start Liar's Dice, then assert that each player's StateUpdated payload contains only
/// their own dice — the other player's dice array must be empty.
/// </summary>
public class LiarsDiceProjectionIntegrationTests
    // TODO: , IClassFixture<WebApplicationFactory<Program>>
{
    // TODO: private readonly WebApplicationFactory<Program> _factory;
    //
    // public LiarsDiceProjectionIntegrationTests(WebApplicationFactory<Program> factory)
    // {
    //     _factory = factory.WithWebHostBuilder(builder =>
    //     {
    //         builder.ConfigureServices(services =>
    //         {
    //             // Replace Postgres with in-memory store for tests
    //             // services.RemoveAll<DbContextOptions<PlatformDbContext>>();
    //             // services.AddDbContext<PlatformDbContext>(opts =>
    //             //     opts.UseInMemoryDatabase("integration-test"));
    //         });
    //     });
    // }

    [Fact(Skip = "TODO: requires WebApplicationFactory + SignalR client wiring — see comments above")]
    public async Task StateUpdated_DuringBidding_PlayerBDoesNotReceivePlayerADice()
    {
        // ── Arrange ───────────────────────────────────────────────────────────

        // TODO: var client = _factory.CreateClient();

        // TODO: Authenticate as Player A (e.g. POST /auth/test-token?userId=pA)
        // TODO: Authenticate as Player B (e.g. POST /auth/test-token?userId=pB)

        // TODO: Build HubConnection for Player A
        // var connA = new HubConnectionBuilder()
        //     .WithUrl(_factory.Server.BaseAddress + "gamehub",
        //         opts => opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
        //     .Build();

        // TODO: Build HubConnection for Player B
        // var connB = new HubConnectionBuilder()
        //     .WithUrl(_factory.Server.BaseAddress + "gamehub",
        //         opts => opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
        //     .Build();

        // TODO: Create a room (POST /api/rooms), obtain roomId
        // TODO: Both connections call JoinRoom(roomId)
        // TODO: Start a Liar's Dice game (POST /api/rooms/{roomId}/start)

        // ── Capture StateUpdated messages ─────────────────────────────────────

        // TODO: Wire up listeners BEFORE sending the first action:
        // LiarsDiceState? stateForA = null;
        // LiarsDiceState? stateForB = null;
        //
        // connA.On<JsonDocument>("StateUpdated", doc =>
        //     stateForA = JsonSerializer.Deserialize<LiarsDiceState>(doc.RootElement.GetRawText()));
        //
        // connB.On<JsonDocument>("StateUpdated", doc =>
        //     stateForB = JsonSerializer.Deserialize<LiarsDiceState>(doc.RootElement.GetRawText()));

        // TODO: Player A places a bid (SendAction via connA) to trigger a StateUpdated fan-out
        // await connA.InvokeAsync("SendAction", roomId, JsonDocument.Parse(
        //     """{"type":"PlaceBid","bid":{"quantity":1,"face":3}}"""));

        // TODO: await Task.Delay(500); // let SignalR fan-out propagate in-process

        // ── Assert ────────────────────────────────────────────────────────────

        // Player B's received state must NOT contain Player A's dice values.
        // stateForB.Should().NotBeNull();
        // var playerAInB = stateForB!.Players.First(p => p.Id == "pA");
        // playerAInB.Dice.Should().BeEmpty(
        //     because: "Player B must not receive Player A's dice during Bidding (AC-7)");

        // Player A's received state MUST contain their own dice.
        // stateForA.Should().NotBeNull();
        // var playerAInA = stateForA!.Players.First(p => p.Id == "pA");
        // playerAInA.Dice.Should().NotBeEmpty(
        //     because: "Player A must receive their own dice in their projected state (AC-7)");

        // TODO: clean up connections
        // await connA.DisposeAsync();
        // await connB.DisposeAsync();

        await Task.CompletedTask; // placeholder so the async method compiles
    }

    [Fact(Skip = "TODO: requires WebApplicationFactory + SignalR client wiring — see comments above")]
    public async Task JoinRoom_Reconnect_SendsProjectedStateNotFullState()
    {
        // AC-3 / AC-11: When a player reconnects (JoinRoom called on an active game),
        // GameHub.JoinRoom sends a projected state (via dispatcher.ProjectStateForPlayerOrFull),
        // not the raw full state. This ensures reconnect uses the same path as fan-out.

        // TODO: same setup as above: two players, game in Bidding phase

        // TODO: connB disconnects, then reconnects
        // await connB.StopAsync();
        // stateForB = null;
        // await connB.StartAsync();
        // await connB.InvokeAsync("JoinRoom", roomId);
        // await Task.Delay(200);

        // stateForB.Should().NotBeNull();
        // var playerAInB = stateForB!.Players.First(p => p.Id == "pA");
        // playerAInB.Dice.Should().BeEmpty(
        //     because: "reconnect must use the same projection path as fan-out (AC-3, AC-11)");

        await Task.CompletedTask; // placeholder so the async method compiles
    }
}
