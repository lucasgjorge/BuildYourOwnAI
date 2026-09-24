# LESSONS - auto-maintained by scripts/lessons.py

> Machine-owned. Do NOT hand-edit. Changes are overwritten on the next `lessons.py` write.
> Canonical state lives in `.specs/lessons.json`. Edit lessons only via the script.
> promote_threshold=2 distinct features · window_days=45 · quarantine_threshold=2

## Confirmed (load these at Plan/Checks)

Corroborated across multiple features. Safe to apply as guidance.

_none_

## Candidates (under observation - do NOT load as guidance yet)

Seen once or not yet corroborated. Tracked, not trusted.

### L-001 - Give every screen state listed in the plan's Observable table its own check member, not only the ones the criterion text names
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `web-ui` · harmful: 0
- features: rag-mvp
- evidence: .specs/features/rag-mvp/verification.md round 1 Coverage - Observable screen states (web-ui)
- last seen: 2026-09-24T02:04:44Z

### L-002 - Assert every field a Surface route declares in its Out column, not only the fields the claim mentions
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `api-contract` · harmful: 0
- features: rag-mvp
- evidence: round 1 C9/C43 response fields (api-contract)
- last seen: 2026-09-24T02:04:45Z

### L-003 - Wait for the page's own landmark before asserting a loading text that a parent guard also renders
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `web-ui` · harmful: 0
- features: rag-mvp
- evidence: round 2 W2 - C47 matched RequireAuth loader (web-ui)
- last seen: 2026-09-24T02:04:45Z

### L-004 - Assert timestamp values against the value returned at creation, not only their relative order
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `api-contract` · harmful: 0
- features: rag-mvp
- evidence: round 2 B1 - ListAssistants createdAt constant (api-contract)
- last seen: 2026-09-24T02:04:45Z

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
