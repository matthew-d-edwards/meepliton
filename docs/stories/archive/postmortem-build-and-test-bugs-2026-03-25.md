# Post-mortem — DeadMansSwitch / FThat build and test bugs — 2026-03-25

Scope: story-030 (DeadMansSwitch + FThat game module). Triggered after PR #38 merged.

---

## What went well

- Backend and frontend implemented the full game logic without structural rework — the module architecture held.
- The tester agent identified all seven bugs before merge rather than after; no follow-up fix story was required.
- The `GenericTypeArguments` filter pattern for EF Core pool descriptors was discovered and documented in the same session it was needed.

---

## What to improve

- **Agent: backend** — added DbContextFactory NuGet checklist to the Database patterns section. The three `Microsoft.Extensions.Configuration.*` packages are silently required by any `*DbContextFactory.cs` that calls `ConfigurationBuilder`. The backend agent scaffolded the factory without the packages, causing a build failure. The fix is now a proactive checklist item: add the packages when writing the factory file, not after the build fails.

- **Agent: backend** — added JsonDocument value converter requirement to the Database patterns section. A `DbContext` property typed as `JsonDocument` without an explicit `HasConversion` in `OnModelCreating` builds and runs fine against Npgsql (which handles it natively) but breaks the InMemory provider used in tests. The agent defined the model property without the converter; the tester's InMemory swap then failed. The converter is now mandatory at property-definition time.

- **Agent: backend** — added two verify checklist items under step 4 to catch both of the above before push: confirm NuGet packages when a factory file exists, confirm `HasConversion` when any model property is `JsonDocument`.

- **Agent: tester** — added "Integration test factories — required patterns" section (three rules). The tester's `WebApplicationFactory` subclass had three independent bugs that each caused test failures:
  1. Only `DbContextOptions<T>` was removed from DI, leaving orphaned pool descriptors from Aspire's `AddDbContextPool`; DI validation failed at startup.
  2. `Guid.NewGuid()` was inside the `AddDbContext` lambda rather than captured before it; each HTTP request scope got a different InMemory database, so seeded users were invisible to authenticated requests and endpoints returned 401.
  3. `AddScheme` was called for `IdentityConstants.ExternalScheme`, which `AddIdentity()` already registers; this threw at startup.

  All three patterns are now documented with copy-paste-ready code.

- **Agent: tester** — added "Deserializing game module output — camelCase options required" section. Game modules serialize state as camelCase JSON; a `JsonSerializer.Deserialize<T>()` call without `PropertyNameCaseInsensitive = true` silently left all PascalCase C# properties as `null`, making scoring assertions fail with misleading "expected 32, got 0" errors. The fix (a static `CamelCaseOptions` field) is now required in all test classes that deserialize module output. Also added guidance to write derivation comments alongside scoring assertions.

---

## Collaboration issues observed

The backend agent and tester agent operated independently on the same session branch. The backend agent's two model-layer omissions (missing NuGet packages, missing value converter) were discovered by the tester agent rather than caught at build time. This is the correct failure mode — tester caught them before merge — but it required an extra round of backend fixes after the tester had already started writing tests.

Root cause: the backend agent's verify step (step 4) checked for warnings and API surface changes, but had no explicit database-layer checklist. The tester's factory setup had no reference pattern for EF Core pool removal or InMemory scope isolation.

Fix: both agents now have the relevant checklists. No structural ownership change is required.

---

## Deferred (needs human decision)

None. All findings were resolved within the session.

---

## Bugs documented (7 total)

| # | Bug | Agent responsible | Root cause |
|---|---|---|---|
| 1 | Build failure: missing `Microsoft.Extensions.Configuration` | backend | Factory scaffolded without required NuGet packages |
| 2 | Build failure: missing `Microsoft.Extensions.Configuration.Json` | backend | Same — all three packages absent |
| 3 | Build failure: missing `Microsoft.Extensions.Configuration.EnvironmentVariables` | backend | Same — all three packages absent |
| 4 | Test DI validation failure: orphaned EF Core pool descriptors | tester | `WebApplicationFactory` only removed `DbContextOptions<T>`, not pool singletons |
| 5 | Integration test 401 on authenticated endpoints | tester | `Guid.NewGuid()` inside `AddDbContext` lambda; each scope got a fresh InMemory database |
| 6 | Startup throw: duplicate scheme registration | tester | `AddScheme` called for a name `AddIdentity()` already registered |
| 7 | Scoring assertions silently wrong (expected N, got 0) | tester | `JsonSerializer.Deserialize<T>()` without `PropertyNameCaseInsensitive`; all properties null |
