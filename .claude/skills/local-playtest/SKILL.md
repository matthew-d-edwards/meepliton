---
name: local-playtest
description: Run the Meepliton Aspire stack locally and play/QA a game end-to-end in a browser as a dev account. Load when asked to play, manually test, reproduce a bug in, or verify a fix against the real running app (not just unit tests).
user-invocable: true
---

# Local playtest — run the stack and drive a real game in the browser

Use this to reproduce a bug or verify a fix in the **actual running app** (full stack + SignalR + DB), not just xUnit. Drive the browser as a seeded dev account.

Golden rule: if you get **stuck** driving the UI, that is itself a bug to investigate — do **not** silently fall back to hitting the API to "make progress". Read-only `fetch('/api/...')` is fine for *verifying* state; taking game actions must go through the UI.

## 1. Start the stack and read its state via the `aspire` CLI

```powershell
# from repo root. Development env so the DevSeeder creates dev accounts.
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT='true'; $env:DOTNET_ENVIRONMENT='Development'; $env:ASPNETCORE_ENVIRONMENT='Development'
aspire run --non-interactive --nologo        # run in BACKGROUND (long-running)
```

Inspect services with the CLI — **do not poll raw TCP ports**:

| Need | Command |
|---|---|
| Is an AppHost running? | `aspire ps` |
| All resources + endpoints + health (JSON) | `aspire describe --format Json` |
| Wait for readiness | `aspire wait api --status healthy --timeout 240` |
| Server logs (e.g. confirm seeding) | `aspire logs api` |
| Stop everything | `aspire stop` |

Poll `aspire describe --format Json` until the `api` resource is `state=Running, healthStatus=Healthy`. The **API is fixed at `http://localhost:5000`** (AppHost disables the DCP proxy for it). Postgres uses a persisted data volume, so dev accounts and prior rooms survive restarts.

## 2. Attach the preview browser  ⚠️ the frontend port is DYNAMIC

`AddViteApp` makes Aspire start Vite on a **random port each run** (e.g. `:50535`, `:63491`) — that's why a saved room URL's port changes. `aspire describe` shows the real one under the `frontend` resource's `urls`.

But the **preview tool insists on managing its own dev server** and refuses to attach to Aspire's Vite (owned by `dcp.exe`). So run a **separate** Vite that the preview tool owns, on a fixed port — it proxies `/api` + `/hubs` to the Aspire API on `:5000` (`vite.config.ts` falls back to `localhost:5000` when not under Aspire):

`.claude/launch.json` → `frontend` config: `"port": 5173, "autoPort": false`, then `preview_start { name: "frontend" }`. Browser is now at `http://localhost:5173`, talking to the Aspire API. (Aspire's own Vite keeps running, unused — harmless.)

## 3. Dev accounts (Development only — `DevSeeder.cs`)

`bob@dev.local` / `BobPass1` (admin) · `jan@dev.local` / `JanPass1` · `rick@dev.local` / `RickPass1` · `matt@dev.local` / `MattPass1`. Drive as **Bob**.

## 4. Browser-driving recipes (preview_eval)

`preview_click`/`preview_fill` **do not trigger React** here — use `preview_eval`.

- **Login / text inputs** (React-controlled): set value via the native setter, then dispatch `input`:
  ```js
  const set=(el,v)=>{Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,v);el.dispatchEvent(new Event('input',{bubbles:true}));};
  ```
- **HTML `<button>`** → `btn.click()` works.
- **SVG board cells are `<g>` elements — `.click()` does NOT exist on SVGElement.** Dispatch a bubbling event:
  ```js
  cell.dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true,view:window}));
  ```
  Query cells by `[role=button][aria-label^="Cell -4,-2"]` (dev coord labels are on in DEV builds).
- **Create/start a solo game via the UI:** lobby has one "Create room" button per game (find by climbing to the card's game name); the room page has "Start game" (works solo — `MinPlayers` can be 1). No opponent seeding needed for solo.
- **Two-step pickers:** clicking a cell opens a modal (`[role=dialog]`) — Place/Rotate/Move/Spawn — then click the confirm button. Allow ~600ms after a cell click before querying the dialog (React render).
- **Modal overlays block the board.** After a round boundary, Hex Escape shows a **"Zombie movement" overlay** (`Continue` button) and an exit-reveal banner. These are modal — dismiss (click `Continue`) before clicking cells, or your clicks hit the overlay and "nothing happens".
- **Verify an action landed** (read-only): `await (await fetch('/api/rooms/<id>',{credentials:'include'})).json()` → parse `gameState` (full authoritative state), check `stateVersion` grew / positions changed. The benign `SignalR: connection stopped during negotiation` console line per room entry (StrictMode double-mount) is noise.

## 5. Rebuilding after a BACKEND (C#) change

The running API locks the game DLLs, so a build fails with a file-in-use error. Sequence:

```
aspire stop                                              # release DLL locks
dotnet test src/Meepliton.Tests --filter HexEscape       # build + test the change
aspire run --non-interactive --nologo                    # restart; API loads new code
```

Frontend (TS/React) changes need **no restart** — Vite HMR picks them up; just reload the page.

## 6. Cleanup

`aspire run` writes `aspire.config.json` at the repo root (CLI artifact). Stop the stack with `aspire stop` when done.
