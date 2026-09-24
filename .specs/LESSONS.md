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

### L-005 - Build a dotnet test proof selector from the test class name, not the file name, and run the exact filter once to see it select a test
- signal: `gate_fail` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: jev-gaps
- evidence: checks.md C50 (round 1): FullyQualifiedName~CrossCuttingTests matched no test (api-tests)
- last seen: 2026-09-24T13:52:33Z

### L-006 - When a claim names the log one step writes, assert the entry by its id within the window of that step, not any entry carrying the property
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `observability` · harmful: 0
- features: jev-gaps
- evidence: round 1 fault: GapRecorder log removed survived C50 (observability)
- last seen: 2026-09-24T13:52:33Z

### L-007 - Give every foreign key's delete rule in Relations its own check, including SET NULL links added by a door
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `data-model` · harmful: 0
- features: jev-gaps
- evidence: round 1 coverage: Gap->Document SET NULL (door 7) had no check (data-model)
- last seen: 2026-09-24T13:52:34Z

### L-008 - Give each test that proves part of a check its own Proof line, so every half of the check is selected by name
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `web-ui` · harmful: 0
- features: jev-gaps
- evidence: round 1: C55 list-error test not selected by its -t filter (web-ui)
- last seen: 2026-09-24T13:52:34Z

### L-009 - When a claim says items are appended or ordered, assert the positions of the items, not only that each one is present
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `web-ui` · harmful: 0
- features: org-chat
- evidence: org-chat round 1: prepend in Thread.tsx survived C13 (web-ui)
- last seen: 2026-09-24T14:39:07Z

### L-010 - Give every field a list item renders its own assertion, including the fallback text for a null value
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `web-ui` · harmful: 0
- features: org-chat
- evidence: org-chat round 1: Base routingDescription unproven (OrganizationPage.tsx:154) (web-ui)
- last seen: 2026-09-24T14:39:07Z

### L-011 - Declare CSS variables read only from inline styles outside Tailwind @theme, and check the built CSS contains them
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `web-ui` · harmful: 0
- features: org-chat
- evidence: 0bd08bf: lane colors only in inline styles were dropped from Tailwind v4 build (web-ui)
- last seen: 2026-09-24T14:39:07Z

### L-012 - When a criterion lists several things that must never be logged, put a marker in each one and assert every marker, including outputs the fake only produces on a trigger
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `observability` · harmful: 0
- features: source-preview
- evidence: jev-choice round 1: AC 9 members (routing descriptions, router reply) had no assertion (observability)
- last seen: 2026-09-24T18:04:30Z

### L-013 - Prove a layout invariant in the state where the most components are on screen, not the simplest one
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `web-ui` · harmful: 0
- features: source-preview
- evidence: source-preview round 1: C17 proven only for not-found answers; SourcePreview called scrollIntoView (web-ui)
- last seen: 2026-09-24T18:04:30Z

### L-014 - Give every configuration key a criterion names its own check, not only the first key of the section
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `config` · harmful: 0
- features: source-preview
- evidence: source-preview round 1: AI:Routing:TimeoutSeconds unproven (config)
- last seen: 2026-09-24T18:04:30Z

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
