# Chat da organização com Jev verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 9bd4b53..2e197a18171ace9399eca5feff5aca61f4ac7c1f
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

The FAIL rests on two things. First, a mutant survived: the thread order. Second, two coverage members named in the plan have no proof. All 33 checks are proven with located evidence.

## Binding sources

No source is marked binding, so step 1 has nothing to compare. The profile is standard, which does not require step 1.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| none marked binding | n/a | - | - |

## Checks

Proof runs (one invocation per target, at HEAD 2e197a1):

- API: `$env:DOCKER_HOST = "npipe://./pipe/dockerDesktopLinuxEngine"; dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`: exit 0, 164 passed, 0 failed. Every `OrganizationJevTests` case is listed individually as `Passed`: `Router_sees_only_this_organization`, `Confident_choice_answers_within_organization`, `No_match_records_gap_in_organization`, `Foreign_or_missing_organization_returns_404(missing: False|True)`, `No_eligible_in_this_organization_returns_422(withUndescribedAssistant: False|True)`, `Keeps_global_jev_rules(scenario: empty|too-long|low-confidence|router-throws|chat-fails)` and `Shares_rate_limit_with_ask_and_global_jev`. `AuthTests.Protected_route_without_session_returns_401` ran 15 cases, all passed, one for each of the 15 `ProtectedRoutes` rows, including the new `POST /api/organizations/{id}/jev/ask` row.
- Web: `npm --prefix src/web run test -- --reporter=verbose`: exit 0, 7 files, 54 passed. Every selector named in checks.md shows a `✓` line, including each alternative in the C25, C27 and C28 `-t` patterns: 6/6, 5/5 (the jev loading test ran as the 429 and 502 cases) and 6/6 (nav count ran as the (0) and (1) cases).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | the router prompt holds only org A's eligible assistants | API run, `Router_sees_only_this_organization` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/OrganizationJevTests.cs:35` - `Assert.Contains(hr, prompt)`; `:37-39` - `Assert.DoesNotContain("Vendas"/otherOrg/foreign, prompt)`; `:36` - `Assert.DoesNotContain("SemDescricao", prompt)` | PASS |
| C2 | 200 answered, assistant, answer, found and sources from A; alternatives only from A | `Confident_choice_answers_within_organization` Passed | `OrganizationJevTests.cs:60` - `Assert.Equal("answered", ...kind)`; `:61` - `Assert.Equal(hr, ...assistant.id)`; `:65` - `Assert.Equal([document], ...sources.documentId)`; `:66` - `Assert.Equal([culture], ...alternatives.id)` | PASS |
| C3 | noMatch records 1 open gap with organization_id = A and a null assistant_id | `No_match_records_gap_in_organization` Passed | `OrganizationJevTests.cs:81` - `Assert.Equal("noMatch", ...)`; `:82-84` - `Assert.Equal(1, ScalarAsync("... status = 'open' and organization_id = @o and assistant_id is null", ...))` | PASS |
| C4 | a foreign or missing org returns 404 problem details; the router is not called | `Foreign_or_missing_organization_returns_404` x2 Passed | `OrganizationJevTests.cs:102` - `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:103` - `"application/problem+json"`; `:104` - `Assert.False(Factory.Router.ReceivedCallContaining(question))` | PASS |
| C5 | no eligible assistant in the org returns 422 problem details; the router is not called | `No_eligible_in_this_organization_returns_422` x2 Passed | `OrganizationJevTests.cs:122` - `Assert.Equal(HttpStatusCode.UnprocessableEntity, ...)`; `:123` problem+json; `:124` - `Assert.False(Factory.Router.ReceivedCallContaining(question))` | PASS |
| C6 | 400 errors.question; clarify on low confidence or a router exception; 502 without the provider message | `Keeps_global_jev_rules` x5 Passed | `OrganizationJevTests.cs:154-155` - `BadRequest` + `errors.TryGetProperty("question")`; `:158-159` - `OK` + `"clarify"`; `:162-163` - `BadGateway` + `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)` | PASS |
| C7 | 10 ask + 5 global + 5 org requests pass; the 21st returns 429 | `Shares_rate_limit_with_ask_and_global_jev` Passed | `OrganizationJevTests.cs:177,179,181` - `Assert.Equal(HttpStatusCode.OK, ...)` in the three loops; `:183` - `Assert.Equal(HttpStatusCode.TooManyRequests, ... "pergunta 21")` | PASS |
| C8 | the new route returns 401 without a cookie | `Protected_route_without_session_returns_401` 15/15 Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:112` - row `{ "POST", $"/api/organizations/{id}/jev/ask" }`; `:130` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C9 | /organizations opens /organizations/o1 on the Conversa tab with the Mensagem field | web ✓ | `src/web/src/features/chat/chat.test.tsx:50` - `toHaveTextContent('/organizations/o1')`; `:51` - `findByLabelText('Mensagem')`; `:52` - Conversa link `aria-current="page"` | PASS |
| C10 | with no orgs, the create form shows and creating lands on /knowledge | web ✓ | `chat.test.tsx:66` - heading `'Crie sua primeira organização'`; `:70` - `toHaveTextContent('/organizations/o9/knowledge')` | PASS |
| C11 | the sidebar has the org links, Nova organização, Jev (todas), Lacunas (2) and Sair | web ✓ | `chat.test.tsx:80-81` - org links `href '/organizations/o1'`, `'/organizations/o2'`; `:82` - button `/Nova organização/`; `:83` - `'Jev (todas)'` href `/jev`; `:84` - `'Lacunas (2)'` href `/gaps`; `:85` - button `'Sair'` | PASS |
| C12 | Jev decide posts to the org Jev; the thread shows the question, answerer, via Jev, source and alternative | web ✓ | `chat.test.tsx:139` - `expect(sent).toEqual([{ question: 'Como peço férias?' }])`; `:141-144` - `'Respondido por RH'`, `'via Jev'`, `'01_rh.txt'`, button `'Perguntar a Culture'` | PASS |
| C13 | Perguntar a Culture calls the assistant ask with the same question; both answers stay in the thread | web ✓ | `chat.test.tsx:164-166` - `'Resposta do RH.'`, `'Respondido por RH'`, `'Respondido por Culture'` all present; `:167` - `expect(asked).toEqual([{ question: 'Como peço férias?' }])`. The order of the two answers is not asserted (see Faults) | PASS |
| C14 | a pinned assistant is asked directly, without via Jev | web ✓ | `chat.test.tsx:184` - `'Respondido por Tech Team'`; `:185` - `queryByText('via Jev')).not.toBeInTheDocument()`; `:186` - `expect(jevCalls).toBe(0)` | PASS |
| C15 | clarify lists candidates; picking one appends that answer | web ✓ | `chat.test.tsx:199` - `'Qual destas IAs deve responder?'`; `:202` - `'Respondido por Culture'` | PASS |
| C16 | noMatch shows the message and a link to /gaps | web ✓ | `chat.test.tsx:213` - `/Nenhuma IA desta organização sabe responder isso ainda\. A pergunta foi para Lacunas\./`; `:214` - link href `/gaps` | PASS |
| C17 | found=false is flagged; found=true is not | web ✓ | `chat.test.tsx:228` - `getByText('Não encontrado nos documentos - registrado em Lacunas')`; `:232` - `queryByText(...)).not.toBeInTheDocument()` on the found answer | PASS |
| C18 | pending shows who is working and disables Enviar | web ✓ | `chat.test.tsx:247-248` - `'Jev está escolhendo…'` + Enviar `toBeDisabled()`; `:254-255` - `'Tech Team está respondendo…'` + `toBeDisabled()` | PASS |
| C19 | a 502 shows the title as an alert and restores the message | web ✓ | `chat.test.tsx:267` - `findByRole('alert')).toHaveTextContent('O provedor de IA falhou. ...')`; `:268` - `getByLabelText('Mensagem')).toHaveValue('Como peço férias?')` | PASS |
| C20 | a 422 shows the text and a link to the Base tab | web ✓ | `chat.test.tsx:279` - `toHaveTextContent("Nenhuma IA desta organização tem 'Quando usar' preenchido")`; `:280` - link href `/organizations/o1/knowledge` | PASS |
| C21 | an org without assistants shows the message and a Base link, and no Mensagem field | web ✓ | `chat.test.tsx:95` - `'Esta organização ainda não tem IAs'`; `:96` - href `/organizations/o1/knowledge`; `:97` - `queryByLabelText('Mensagem')).not.toBeInTheDocument()` | PASS |
| C22 | the empty thread lists each assistant's name and routingDescription | web ✓ | `chat.test.tsx:109-110` - `within(list).getByText(a.name)` / `getByText(a.routingDescription)` for all 3 | PASS |
| C23 | /assistants/{id} opens the org chat with the assistant pinned | web ✓ | `chat.test.tsx:120` - location `/organizations/o1`; `:121` - radio `'Culture'` `toBeChecked()`; `:122` - `'Jev decide'` not checked | PASS |
| C24 | switching org shows an empty thread | web ✓ | `chat.test.tsx:293-294` - `queryByText('Resposta do RH.')` / `('Como peço férias?')` `.not.toBeInTheDocument()` after clicking ACME | PASS |
| C25 | the Base tab tests pass at /knowledge without weakened assertions | web ✓ 6/6 selectors | `src/web/src/features/organizations/organizations.test.tsx:15` - `BASE = '/organizations/o1/knowledge'`; `:82-84` documents and assistants; `:116` - `'Nenhuma IA nesta organização'`; `:139,141` / `:160,162` - `deletes` `[]` then `['d1']` / `['a1']`; `:205` - `'Processando...'` disabled; `:223` - 0 listitems; `:238` - alert `'Formato não suportado...'`. The diff removed no assertion of these tests; the only removals are the ask-page tests that checks.md replaces with C18/C19/C23 | PASS |
| C26 | deleting the org confirms, cancel sends no DELETE, confirm deletes and goes to /organizations | web ✓ | `organizations.test.tsx:182` - `toMatch(/IAs, os documentos e as lacunas/)`; `:183` - `expect(deletes).toEqual([])`; `:185` - `['o1']`; `:186` - location `/^\/organizations$/` | PASS |
| C27 | the global Jev tests pass with the shared thread | web ✓ 5/5 selectors (6 cases) | `src/web/src/features/jev/jev.test.tsx:34` - `'Respondido por Direto · ACME'`; `:56` - `'Qual destas IAs deve responder?'`; `:72` - noMatch text; `:82-84` - alert + link `/organizations`. The diff only renames labels, as checks.md approves (Mensagem, Enviar, Perguntar a X, pending text in the thread) | PASS |
| C28 | the Lacunas tests pass in the new layout | web ✓ 6/6 selectors (7 cases) | `src/web/src/features/gaps/gaps.test.tsx:54` nav count href `/gaps`; `:63` empty state; `:79` answers payload; `:100-104` dismiss confirm; `:121,126` loading/error; `:135` list error. The file is unchanged in the diff | PASS |
| C29 | CSS has :focus-visible with an outline, and reduced-motion zeroes animation and transition | web ✓ | `src/web/src/styles.test.ts:12` - `toMatch(/:focus-visible\s*\{[^}]*outline:\s*2px solid/)`; `:15-16` - `animation:\s*none !important`, `transition:\s*none !important` inside the reduced-motion block (source `src/web/src/index.css:35-36,72-77`) | PASS |
| C30 | / without a session shows the title and the 3 steps in order, with no redirect | web ✓ | `src/web/src/features/home/home.test.tsx:13` - h1 `/Uma IA para cada assunto/`; `:16` - `toEqual(['Monte a organização', 'Crie as IAs', 'Pergunte ao Jev'])`; `:17` - location `/^\/$/` | PASS |
| C31 | at least 3 examples, each with its assistants; mentions Lacunas | web ✓ | `home.test.tsx:28` - `examples.length).toBeGreaterThanOrEqual(3)`; `:31` - listitems `> 0` for each; `:33` - `toHaveTextContent('Lacunas')` | PASS |
| C32 | no session shows Criar conta -> /register and Entrar -> /login, without Abrir o chat | web ✓ | `home.test.tsx:43-45` - hrefs `/register`, `/login`; `queryByRole('link', { name: 'Abrir o chat' })).not.toBeInTheDocument()` | PASS |
| C33 | a session shows Abrir o chat -> /organizations, without Criar conta | web ✓ | `home.test.tsx:55` - href `/organizations`; `:56` - `'Criar conta'` not in document | PASS |

## Coverage

Recomputed from the plan (Surface, Landing, Criteria, Observable) and from the code (`JevAsk.cs` and `App.tsx` routes).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/organizations/{id}/jev/ask` statuses (7) | plan Surface + `JevAsk.cs:52-61,79-95` | 200 C2 · 400 C6 · 401 C8 · 404 C4 · 422 C5 · 429 C7 · 502 C6 | - |
| `kind` (3) | `JevAsk.cs` `Response` kinds | answered C2 · clarify C6 · noMatch C3 | - |
| router scope exclusions (3) | AC 1 | other org of the same user C1 (`:37-38`) · another user C1 (`:39`) · no description C1 (`:36`) and C5 | - |
| `{id}` not owned (2) | AC 4 | foreign C4 (missing: False) · missing C4 (missing: True) | - |
| message target (2) | AC 10, 12 | Jev decide C12 · pinned C14 | - |
| thread messages (7) | AC 10-18 | answered C12 · appended alternative C13 · clarify C15 · noMatch C16 · found=false C17 · error C19 · 422 C20 | - |
| Conversa observable decisions (6) | plan Observable | no assistants C21 · empty thread C22 · loading C18 · error C19/C20 · unauthorised existing (`App.tsx:20` `RequireAuth` wraps `/organizations/:id`) · **ordering** ("thread em ordem de envio (AC 11: acrescenta ao fim)") | ordering - no assertion on the order of thread entries; prepend mutant survived (Thread.tsx:56) |
| product entries (4) | AC 7, 8, 21, 22 | orgs C9 · no orgs C10 · `/assistants/{id}` C23 · switch C24 | - |
| sidebar (5) | AC 9 | orgs · Nova organização · Jev (todas) · Lacunas (N) · Sair, all C11 | - |
| Base tab contents (AC 23: 4) | AC 23 | upload + documents C25 (`organizations.test.tsx:82,86-87`) · IA list C25 (`:84-85`) · create with "Quando usar esta IA" C25 (`:90-95`) · **IA list with its `routingDescription`** | IA routingDescription on Base - rendered at `OrganizationPage.tsx:154` (`a.routingDescription ?? 'Fora do Jev'`), but no web test asserts it (searched `Fora do Jev` / `respostas curtas` in *.test.tsx: no display assertion) |
| screens (6) | App.tsx routes | Conversa C12-C24 · Base C25-C26 · empty Organizations C10 · Jev C27 · Lacunas C28 · Home C30-C33 | - |
| home CTA by session (2) | AC 31, 32 | no session C32 · session C33 | - |
| doors (2) | Landing | 1: route + 404 before router + shared rate limit C1, C4, C7 (and `JevAsk.cs:58-61`) · 2: client-only thread, reset on switch C24 | - |
| quality (2) | AC 27 | focus-visible C29 · reduced motion C29 | - |

Swept `existing` re-read:
- observability: `JevAsk.cs:131` logs only `{Kind}` and `{EligibleCount}`, and the router failure at `:148` logs only the exception type. It holds.
- `RequireAuth` (Conversa unauthorised): `App.tsx:20` wraps the authenticated routes. It holds.

## Test policy rows

checks.md adds no new rows: "Sem linhas novas", deferring to the rag-mvp/jev-gaps policy. There is nothing to judge.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| none in checks.md | - | - | n/a |

## Faults injected

Isolation: I ran every fault in a `git worktree add <scratchpad>/wt HEAD`. Web faults reused the real `src/web/node_modules` through a junction; I removed the junction before `git worktree remove --force`. The real tree's `git status --porcelain` was empty before and after, and matches the baseline.

| Mutation | Location | Killed |
| --- | --- | --- |
| F1 drop the org filter: `.Where(a => organizationId == null \|\| a.OrganizationId == organizationId)` -> `.Where(a => true)` | `src/BuildYourOwnAI.Api/Features/Jev/JevAsk.cs:86` | yes - C1 `Router_sees_only_this_organization` failed: `Assert.DoesNotContain() Failure: Sub-string found` |
| F2 drop the 404 ownership check before routing: `if (organizationId is { } orgId && !await db.Organizations.AnyAsync(...))` -> `if (false)` | `JevAsk.cs:82` | yes - C4 both cases failed: `Expected: NotFound, Actual: UnprocessableEntity` |
| F3 record the noMatch gap without its organization: `GapRecorder.RecordAsync(db, ..., organizationId, null, ...)` -> `(db, ..., null, null, ...)` | `JevAsk.cs:108` | yes - C3 `No_match_records_gap_in_organization` failed: `Expected: 1, Actual: 0` |
| F4 thread prepends instead of appending: `[...current, ...added]` -> `[...added, ...current]` | `src/web/src/features/chat/Thread.tsx:56` | no - survived: chat.test.tsx + jev.test.tsx all 22 passed, including C13 "asking an alternative appends to the thread". The thread order (plan Observable "ordering", AC 11 "acrescenta ao fim") is never asserted |
| F5 home CTA ignores the session: `const signedIn = session.isSuccess` -> `false` | `src/web/src/features/home/HomePage.tsx:30` | yes - C33 "home with session opens the chat" failed: `Unable to find role="link" and name "Abrir o chat"` |

A fault that replaces the thread instead of appending would be killed by C13 at `chat.test.tsx:164-165`, which asserts the RH answer is still present, so I did not inject it separately. The prepend mutant is the plausible wrong implementation that gets through.

## Gate

- `dotnet test tests/BuildYourOwnAI.Api.Tests` - 164 passed, 0 failed
- `npm --prefix src/web run test -- --reporter=verbose` - 54 passed, 0 failed
- `python C:/Users/luccc/.claude/skills/tlc-spec-lean/scripts/validate_verification.py org-chat` - see the exit code reported with this verification

Ranked gaps:
1. Thread order unproven, and the mutant survived. Prepending new entries passes every test. The claim at risk is the plan Observable "ordering: thread em ordem de envio (AC 11: acrescenta ao fim)", tied to C13 but not asserted by it. Evidence: `src/web/src/features/chat/Thread.tsx:56` and `src/web/src/features/chat/chat.test.tsx:163-166` (presence only, no order). Fix: assert the order of the thread's listitems (question, RH answer, Culture answer) in C13.
2. The Base tab never asserts that the IA list shows each assistant's `routingDescription`, although AC 23 names it. The value is rendered at `src/web/src/features/organizations/OrganizationPage.tsx:154`, and `organizations.test.tsx:71-97` does not check it. This gap is inherited, not a weakening: the 9bd4b53 version did not assert it either.
3. Observation, not failing: the removed rag-mvp test "assistant page asks and links to organization" was the only one asserting that an answer renders two sources (`politicas.pdf`, `contrato.md`). C12 checks one source only.
