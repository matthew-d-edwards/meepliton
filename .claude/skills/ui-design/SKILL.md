---
name: ui-design
description: Collaborative UI design workflow. Run before building any new screen, game board, or platform component. Orchestrates the UX designer and frontend developer as a team that debates design and implementation approach before writing a single line of code.
user-invocable: true
argument-hint: <screen or component to design>
---

## Purpose

Produce a design-aligned implementation plan that the UX designer and frontend developer have both signed off on — before any code is written. Prevents the cycle of "build it, then redesign it".

## When to use

- Any new screen or page (lobby, room, game board, settings)
- Any new platform component being extracted to `packages/ui/`
- Any game's UI module (board, controls, score display)
- Significant layout or interaction changes to existing screens

Do **not** use for small text changes, colour tweaks, or single-token corrections.

## Team

| Role | Agent | Perspective |
|---|---|---|
| UX designer | `ux` | Visual design, design token compliance, mobile layouts, accessibility, component boundaries |
| Frontend developer | `frontend` | TypeScript feasibility, React patterns, type alignment with C# models, CSS Modules vs global |

## Workflow

### Round 1 — UX defines the design intent

The `ux` agent produces:
1. **Design brief** — what is this screen for? Who uses it and when?
2. **Layout sketch** — ASCII or prose description at 375px, 768px, 1280px
3. **Component list** — which components are needed; which already exist in `packages/ui/`?
4. **Design tokens** — which `--surface-*`, `--neon-*`, `--font-*`, `--space-*` values apply?
5. **Interaction notes** — hover states, transitions, loading states, empty states
6. **Accessibility requirements** — focus order, `aria-label`s, contrast requirements
7. **Boundary call** — is any proposed component reusable (→ `packages/ui/`) or game-specific (→ `apps/frontend/src/games/{gameId}/`)?

### Round 2 — Frontend developer responds

The `frontend` agent reads the UX brief and flags:
- **Type alignment** — does the required state exist in `types.ts`? Does it mirror the C# model?
- **Pattern fit** — does the proposed component structure match existing React patterns in this codebase?
- **CSS approach** — CSS Modules (game-scoped) or global class names (platform chrome)? Confirm the right call.
- **Complexity concerns** — anything that would require a new dependency, a SignalR change, or a new API endpoint should be flagged and escalated
- **Counter-proposal** — if the design intent can't be met as specified, propose an alternative that preserves the design goal

### Round 3 — UX responds to concerns

The `ux` agent either:
- **Accepts** the frontend's constraints and adjusts the design brief, or
- **Holds the line** with a design rationale (e.g., "the 44px tap target is non-negotiable on mobile")

Rounds continue until both agents are aligned. Maximum 3 rounds.

### Final output — Implementation plan

When aligned, produce a single **UI implementation plan** at `docs/ui-plans/{component-slug}.md`:

```markdown
# UI Plan: {Screen or Component Name}

**Status:** Agreed
**Date:** {today}
**Authors:** ux + frontend

## Design intent
{one paragraph from UX perspective}

## Breakpoint behaviour
| Breakpoint | Layout |
|---|---|
| < 700px | {description} |
| 700–1100px | {description} |
| > 1100px | {description} |

## Components
| Component | Location | New or existing |
|---|---|---|
| `{Name}` | `packages/ui/src/` or `apps/frontend/src/games/{id}/` | New / Existing |

## CSS approach
- Platform chrome: global class names
- Game-scoped: CSS Modules
- Tokens: {list key tokens used}

## Type requirements
- `{TypeName}` must mirror `{C#Model}` — verify before building

## Accessibility
- {requirement}

## Out of scope
- {explicitly excluded}
```

## Ground rules

- The `ux` agent owns the design decisions; the `frontend` agent owns feasibility
- The `frontend` agent must never override a design decision with "it's easier to do X" unless X provably achieves the same design intent
- The `ux` agent must never require functionality (new API, new SignalR event) without consulting the `architect` first
- No `any` in TypeScript — if the type doesn't exist, add it before building
- All CSS values via tokens — if a token doesn't exist, propose adding it to `tokens.css` via the `ux` agent
