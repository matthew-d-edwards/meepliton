---
name: spec-design
description: Collaborative spec and architecture design workflow. Run when designing a new feature, game, or significant change. Orchestrates analyst and architect as a team that debates until they reach consensus, then produces a finalised spec.
user-invocable: true
argument-hint: <feature or change to design>
---

## Purpose

Produce a spec and architecture decision that has been stress-tested by two opposing perspectives — the analyst (what the product needs) and the architect (what the system can sustain) — before any code is written.

## When to use

- Designing a new game module (backend + frontend)
- Adding a significant platform feature (auth change, new SignalR event, new API surface)
- Any change that touches contracts (`IGameModule`, `IGameHandler`, `GameContext`, TS `GameModule`)
- Any change requiring a new EF Core migration

Do **not** use for small bug fixes or single-file changes.

## Team

| Role | Agent | Perspective |
|---|---|---|
| Analyst | `analyst` | User needs, product value, acceptance criteria |
| Architect | `architect` | System constraints, maintainability, contract integrity, blast radius |

## Workflow

### Round 1 — Analyst drafts

The `analyst` agent produces:
1. **Problem statement** — what user need or gap does this solve?
2. **Proposed solution** — describe in plain language, no code
3. **Acceptance criteria** — 3–7 testable "Given / When / Then" statements
4. **Open questions** — anything that needs architect input

### Round 2 — Architect challenges

The `architect` agent reads the analyst's draft and responds to every open question plus raises objections on:
- **Contract stability** — does this require changes to `IGameModule`, `IGameHandler`, `GameContext`, or TS `GameModule`? If yes, flag blast radius.
- **Data model** — new tables? JSONB schema changes? Migration risk?
- **Consistency** — does this fit the existing patterns (Scrutor auto-discovery, snake_case tables, `ReducerGameModule` inheritance)?
- **Complexity** — is the proposed solution the simplest thing that could work?

The architect proposes an **alternative or refined design** if any objection is critical.

### Round 3 — Analyst responds

The `analyst` agent either:
- **Accepts** the architect's refinements and updates the spec, or
- **Contests** with justification — rounds continue until consensus

### Round 4+ — Iterate until agreed

Rounds alternate until both agents have explicitly stated agreement. Maximum 4 rounds; if unresolved after 4, escalate to the human.

### Final output

When consensus is reached, produce a single **Spec document** at `docs/specs/{feature-slug}.md`:

```markdown
# Spec: {Feature Name}

**Status:** Agreed
**Date:** {today}
**Authors:** analyst + architect

## Problem
{one paragraph}

## Solution
{one to three paragraphs, no code}

## Acceptance criteria
- [ ] Given … When … Then …
- [ ] …

## Architecture decisions
- {decision and rationale}
- …

## Out of scope
- {explicitly excluded items}

## Implementation hints
- Backend: {agent: backend}
- Frontend: {agent: frontend, ux}
- CI: {agent: devops} if migrations added
```

## Ground rules

- Neither agent may write code during this workflow — specs only
- The architect may not veto purely on "it's hard" — must propose an alternative
- The analyst may not override architectural constraints without justification
- Both agents must cite specific files or contracts when making claims about the codebase
