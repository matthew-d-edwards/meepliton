---
name: story-review
description: Collaborative story hardening workflow. Run after user stories or acceptance criteria are written, before implementation begins. Orchestrates a devil's advocate and the tester agent to challenge every assumption and validate testability. Stories that survive this review are implementation-ready.
user-invocable: true
argument-hint: <path to spec or story to review>
---

## Purpose

Catch gaps, ambiguities, and edge cases in user stories before they become bugs in production. A story that survives the devil's advocate and tester is genuinely ready to build.

## When to use

- After `spec-design` produces a spec — before implementation begins
- After `ui-design` produces a UI plan — before the `frontend` agent writes code
- After any acceptance criteria are written by the `analyst`
- When a bug fix requires a spec update — review the updated spec before re-implementing

Do **not** skip this step for features involving multiplayer state, game rules, or auth flows — they are especially prone to edge-case failures.

## Team

| Role | Agent | Perspective |
|---|---|---|
| Devil's advocate | `analyst` (adversarial mode) | Challenges every assumption; finds the happy-path bias; asks "what could go wrong?" |
| QA engineer | `tester` | Evaluates testability; identifies missing test cases; flags untestable acceptance criteria |

> The `analyst` agent is reused here in adversarial mode — it is explicitly **not** defending the spec it wrote. Its job in this workflow is to find holes.

## Workflow

### Round 1 — Devil's advocate challenges the spec

The `analyst` agent (adversarial) reads the spec and challenges:

**Scope challenges:**
- Is there a simpler solution that meets the same need?
- Does this spec encode implementation details that should be left to the developer?
- Are any acceptance criteria actually two stories pretending to be one?

**Edge case challenges** — for each acceptance criterion ask:
- What happens when the precondition is false?
- What happens under concurrent access (two players acting simultaneously)?
- What is the empty / zero / null case?
- What happens if the network drops mid-action?

**Assumption challenges:**
- Does this spec assume something that could change (player count, board size, auth state)?
- Does it assume a specific error message the backend doesn't guarantee?

Output: a numbered list of **challenges**, each one specific and actionable.

### Round 2 — Tester evaluates testability

The `tester` agent reads the original spec and the devil's advocate challenges, then:

1. **Scores each acceptance criterion** on testability:
   - ✅ Testable as written
   - ⚠️ Testable with modification — specifies required change
   - ❌ Untestable — explains why and proposes a rewrite

2. **Adds missing test cases** — at least one per acceptance criterion covering the unhappy path

3. **Flags SignalR / real-time concerns** — acceptance criteria involving live state updates need explicit "what does the client see?" assertions

4. **Estimates test complexity** — "straightforward xUnit" vs "requires mock SignalR hub" vs "requires full integration test" — so developers know what they're signing up for

### Round 3 — Spec owner responds

The original author (usually the `analyst`) reads all challenges and tester feedback and must:

- **Resolve** each challenge: either update the spec or explain why the challenge doesn't apply
- **Rewrite** untestable acceptance criteria
- **Add** missing edge case criteria if the devil's advocate found them valid

If more than 3 acceptance criteria need rewriting, restart the `spec-design` workflow.

### Final output — Hardened spec

Update the spec document in-place with:
- All acceptance criteria rewritten for testability
- New edge case criteria added
- A **Review summary** section appended:

```markdown
## Story review

**Reviewed by:** analyst (adversarial) + tester
**Date:** {today}
**Challenges raised:** {N}
**Resolved:** {N}
**Criteria added:** {N}
**Verdict:** Ready for implementation ✅  /  Needs redesign ❌

### Key edge cases to implement
- {edge case that must be handled}
- …

### Test complexity note
- {xUnit / mock SignalR / integration — and why}
```

## Ground rules

- The devil's advocate is not trying to kill the feature — it is trying to make it bulletproof
- Every challenge must be specific: "what happens when X?" not "this seems risky"
- The tester may not mark a criterion "untestable" without proposing a testable rewrite
- The spec owner may not mark a challenge "resolved" without making a concrete change or giving a concrete reason
- If the devil's advocate and tester agree that a spec is fundamentally broken, they escalate to the human — they do not try to fix a broken design through test engineering
