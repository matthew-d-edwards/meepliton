<#
.SYNOPSIS
    Scaffold a new Meepliton game module.

.EXAMPLE
    ./scripts/new-game.ps1 -GameId sushigo -GameName "Sushi Go" -MinPlayers 2 -MaxPlayers 5 -Description "Draft cards to build the best sushi meal."
#>
param(
    [Parameter(Mandatory)][string]$GameId,
    [Parameter(Mandatory)][string]$GameName,
    [string]$Description = "A Meepliton board game.",
    [int]$MinPlayers = 2,
    [int]$MaxPlayers = 6
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Derive names ──────────────────────────────────────────────────────────────
$Pascal = (Get-Culture).TextInfo.ToTitleCase($GameId.ToLower()) -replace '\s', ''
$Namespace = "Meepliton.Games.$Pascal"
$CsProject = "src/games/Meepliton.Games.$Pascal"
$TsDir     = "apps/frontend/src/games/$GameId"

Write-Host ""
Write-Host "Scaffolding game: $GameName (id=$GameId, pascal=$Pascal)" -ForegroundColor Cyan
Write-Host ""

# ── 1. C# project ─────────────────────────────────────────────────────────────
$csproj = "$CsProject/Meepliton.Games.$Pascal.csproj"
New-Item -ItemType Directory -Path "$CsProject/Migrations" -Force | Out-Null
New-Item -ItemType Directory -Path "$CsProject/Models"     -Force | Out-Null

Set-Content $csproj @"
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Meepliton.Contracts\Meepliton.Contracts.csproj" />
  </ItemGroup>

</Project>
"@

# Module class
Set-Content "$CsProject/${Pascal}Module.cs" @"
using System.Text.Json;
using Meepliton.Contracts;
using $Namespace.Models;

namespace $Namespace;

public class ${Pascal}Module : ReducerGameModule<${Pascal}State, ${Pascal}Action, object>
{
    public override string GameId      => "$GameId";
    public override string Name        => "$GameName";
    public override string Description => "$Description";
    public override int    MinPlayers  => $MinPlayers;
    public override int    MaxPlayers  => $MaxPlayers;

    public override ${Pascal}State CreateInitialState(IReadOnlyList<PlayerInfo> players, object? options)
    {
        // TODO: initialise state from players list
        throw new NotImplementedException();
    }

    public override string? Validate(${Pascal}State state, ${Pascal}Action action, string playerId)
    {
        // TODO: return null if valid, or an error string if invalid
        throw new NotImplementedException();
    }

    public override ${Pascal}State Apply(${Pascal}State state, ${Pascal}Action action)
    {
        // TODO: return new state after applying the action
        throw new NotImplementedException();
    }
}
"@

# DbContext
Set-Content "$CsProject/${Pascal}DbContext.cs" @"
using Meepliton.Contracts;
using $Namespace.Models;
using Microsoft.EntityFrameworkCore;

namespace $Namespace;

/// <summary>
/// Remove this DbContext entirely if your game does not need supplementary tables.
/// If you keep it, add EF migrations and a CI step per docs/requirements.md §15.5.
/// </summary>
public class ${Pascal}DbContext(DbContextOptions<${Pascal}DbContext> options, IConfiguration configuration)
    : DbContext(options), IGameDbContext
{
    public string GameId => "$GameId";

    // Read-only platform views
    public DbSet<RoomView>       Rooms       => Set<RoomView>();
    public DbSet<RoomPlayerView> RoomPlayers => Set<RoomPlayerView>();
    public DbSet<UserView>       Users       => Set<UserView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoomView>().ToTable("rooms").HasNoKey();
        modelBuilder.Entity<RoomPlayerView>().ToTable("room_players").HasNoKey();
        modelBuilder.Entity<UserView>().ToTable("users").HasNoKey();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseNpgsql(
                configuration.GetConnectionString("meepliton"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_$GameId"));
        }
    }

    public async Task MigrateAsync(CancellationToken ct = default)
        => await Database.MigrateAsync(ct);
}

// Platform view records — same as Skyline; copy if you need them
public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);
"@

# Models
Set-Content "$CsProject/Models/${Pascal}Models.cs" @"
namespace $Namespace.Models;

// TODO: define your state shape here — no prescribed structure
public record ${Pascal}State(
    // TODO
);

// TODO: define your action type(s)
public record ${Pascal}Action(string Type /* TODO: add payload properties */);
"@

# C# README
Set-Content "$CsProject/README.md" @"
# $GameName

TODO: describe the game.

## Files

| File | Purpose |
|---|---|
| ``${Pascal}Module.cs`` | Game logic — implement CreateInitialState, Validate, Apply |
| ``${Pascal}DbContext.cs`` | Optional supplementary DB tables — delete if not needed |
| ``Models/${Pascal}Models.cs`` | State and action records |
| ``Migrations/`` | EF Core migration history |

## Adding migrations

``````bash
dotnet ef migrations add <Name> \
  --project src/games/Meepliton.Games.$Pascal \
  --context ${Pascal}DbContext
``````
"@

Write-Host "  Created $CsProject" -ForegroundColor Green

# ── 2. Add to solution ────────────────────────────────────────────────────────
$slnPath = "src/Meepliton.sln"
$guid     = [System.Guid]::NewGuid().ToString().ToUpper()
$gamesFolder = "E5F6A7B8-C9D0-1234-EF01-345678901234"   # Games solution folder GUID

$projectLine  = "Project(`"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`") = `"Meepliton.Games.$Pascal`", `"games\Meepliton.Games.$Pascal\Meepliton.Games.$Pascal.csproj`", `"{$guid}`""
$endLine      = "EndProject"
$configLines  = @(
    "`t`t{$guid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
    "`t`t{$guid}.Debug|Any CPU.Build.0 = Debug|Any CPU",
    "`t`t{$guid}.Release|Any CPU.ActiveCfg = Release|Any CPU",
    "`t`t{$guid}.Release|Any CPU.Build.0 = Release|Any CPU"
)
$nestedLine   = "`t`t{$guid} = {$gamesFolder}"

$sln = Get-Content $slnPath
$out = [System.Collections.Generic.List[string]]::new()
foreach ($line in $sln) {
    if ($line -match "^Global$") {
        $out.Add($projectLine)
        $out.Add($endLine)
    }
    $out.Add($line)
    if ($line -match "ProjectConfigurationPlatforms") {
        # configs are added inline below
    }
    if ($line -match "EndGlobalSection" -and $out[-2] -match "Release.*Build\.0") {
        foreach ($c in $configLines) { $out.Add($c) }
    }
    if ($line -match "NestedProjects.*=.*preSolution") {
        $out.Add($nestedLine)
    }
}
Set-Content $slnPath ($out -join "`n")
Write-Host "  Updated $slnPath" -ForegroundColor Green

# ── 3. Add project reference to Meepliton.Api ─────────────────────────────────
$apiCsproj = "src/Meepliton.Api/Meepliton.Api.csproj"
$refLine   = "    <ProjectReference Include=`"..\games\Meepliton.Games.$Pascal\Meepliton.Games.$Pascal.csproj`" />"
$content   = Get-Content $apiCsproj
$content   = $content -replace '(<!-- Add new games here -->)', "$refLine`n    `$1"
Set-Content $apiCsproj $content
Write-Host "  Updated $apiCsproj" -ForegroundColor Green

# ── 4. Frontend module ────────────────────────────────────────────────────────
New-Item -ItemType Directory -Path "$TsDir/components" -Force | Out-Null

Set-Content "$TsDir/index.tsx" @"
import type { GameModule } from '@meepliton/contracts'
import type { ${Pascal}State } from './types'
import Game from './components/Game'

const $GameId: GameModule<${Pascal}State> = {
  gameId: '$GameId',
  Component: Game,
}

export default $GameId
"@

Set-Content "$TsDir/types.ts" @"
// Mirror of ${Pascal}Models.cs — keep in sync

export interface ${Pascal}State {
  // TODO: mirror your C# state record here
}

export type ${Pascal}Action =
  | { type: string /* TODO */ }
"@

Set-Content "$TsDir/styles.module.css" @"
/* TODO: add game-specific styles */
"@

Set-Content "$TsDir/components/Game.tsx" @"
import type { GameContext } from '@meepliton/contracts'
import type { ${Pascal}State } from '../types'

export default function Game({ state, myPlayerId, dispatch }: GameContext<${Pascal}State>) {
  return (
    <div>
      <p>TODO: implement $GameName UI</p>
      <pre>{JSON.stringify(state, null, 2)}</pre>
    </div>
  )
}
"@

Set-Content "$TsDir/README.md" @"
# $GameName — Frontend

TODO: describe the game UI.

## Files

| File | Purpose |
|---|---|
| ``index.tsx`` | Module entry point |
| ``types.ts`` | Mirror of C# state/action records |
| ``components/Game.tsx`` | Main game component — implement your UI here |
| ``styles.module.css`` | Game-specific styles |
"@

Write-Host "  Created $TsDir" -ForegroundColor Green

# ── 5. Register in registry.ts ────────────────────────────────────────────────
$registry = "apps/frontend/src/games/registry.ts"
$content  = Get-Content $registry -Raw
$newLine  = "  $GameId`: () => import('./$GameId'),"
$content  = $content -replace '(// Add new games here)', "$newLine`n  `$1"
Set-Content $registry $content
Write-Host "  Updated $registry" -ForegroundColor Green

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Done! Next steps:" -ForegroundColor Cyan
Write-Host "  1. Implement TODOs in $CsProject/${Pascal}Module.cs"
Write-Host "  2. Implement TODOs in $TsDir/components/Game.tsx"
Write-Host "  3. Run: dotnet run --project src/Meepliton.AppHost"
Write-Host "  4. Open a Claude conversation and attach .claude/skills/NEW-GAME.md"
Write-Host ""
