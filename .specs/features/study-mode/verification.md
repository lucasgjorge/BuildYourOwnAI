# Study Mode verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 2d13fc7..c3e6833 (round 2 scope: fix `7e6c269` + docs `c3e6833`, over round 1 at `273dd9c`)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Round 1 (`273dd9c`) was FAIL with four findings: surviving `ChosenOption == null` mutant, malformed
model JSON answering 500, AC 25 wording vs "Ver resultado", AC 26 answer-error half unproven. This
round re-runs every proof at `c3e6833`, re-judges C8, C9, C29, C30, C31, refreshes citations in the
files `7e6c269` touched, and carries the rest from `273dd9c`.

Commits in the range that are not study-mode, and whether they reach study-mode surfaces:

- `7f8b252`, `15bbdf0` - docs only (user-name / admin-usage plans). No study surface.
- `e8cb234` (user-name, API) - touches shared test surfaces: `ApiTestBase.cs:26` now registers with
  `fullName` (every study test's `NewUserClientAsync` goes through it) and `AuthTests.cs` (C20 rows
  moved to `:114`/`:115`, a `/api/auth/me` row added). No change to `Features/Study`.
- `6912d8e` (user-name, web) - touches `src/web/src/test/server.ts` (`loggedIn()` now mocks
  `/api/auth/me`, used by `renderApp` in the study tests) and `AppLayout.tsx` (new header above the
  `<Outlet />` that hosts StudyPage). No change to `features/study`.
- `e6322d3` (user-name verification docs) landed on `main` while this round ran; `.specs` only.

All of these are exercised by the full re-run below, which is green, so none of them breaks a study check.

## Binding sources

Carried from `273dd9c`: no source is marked binding in `plan.md` (Sources: the 2026-09-24
conversation and `docs/PRD.md`). Step 1 does not apply; the fix did not touch the interface beyond
the AC 25 wording, which is plan text, not a binding design.

## Checks

Verified at `c3e6833` (proofs re-run in full, real tree, HEAD `c3e6833`):

- API: `$env:DOCKER_HOST = "npipe://./pipe/dockerDesktopLinuxEngine"; dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` - exit 0, **254 passed, 0 failed**. Every named study test appears individually as `Passed` (theories: every row, incl. `chunk-as-text`, `correct-as-text`, `top-level-array`, `Simultaneous_answers_only_one_wins`). The C20 theory prints truncated paths; the `POST /api/study-sessions/...` row is visible, the `POST /api/organizations/<id>/study-sessions` row is one of three truncated `POST /api/organizations/...` rows, all `Passed`, 0 failed in the run.
- Web: `npm --prefix src/web run test -- --reporter=verbose` - exit 0, **78 passed (9 files), 0 failed**; each of the 9 study names appears as `✓ src/features/study/study.test.tsx > study mode > <name>`.

Every Proof selector in `checks.md` matches a test that ran and passed. Citations for
`StudySessionsTests.cs` (lines after `:218` moved), `StudyAnswerTests.cs` (appended C31) and
`study.test.tsx` (appended C30 lines) refreshed at `c3e6833`; `AuthTests.cs` refreshed because
`e8cb234` moved it. Rows marked *carried* keep the round-1 judgment, with the line re-checked unchanged.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | 201, Location, id, createdAt, 5 questions, positions 1..5, 4 options | `StudySessionsTests.Create_returns_questions_with_four_options` passed (carried judgment) | `tests/BuildYourOwnAI.Api.Tests/Features/StudySessionsTests.cs:76` - `Assert.Equal(HttpStatusCode.Created, response.StatusCode)`; `:79` - `Assert.Equal($"/api/study-sessions/{id}", response.Headers.Location?.OriginalString)`; `:83` - `Assert.Equal([1, 2, 3, 4, 5], ...position)`; `:88` - `Assert.Equal(4, q.GetProperty("options").GetArrayLength())` | PASS |
| C2 | only chosen documents in the prompt, no repeated chunk; absent documentIds uses all | `StudySessionsTests.Samples_only_chosen_documents_without_repeating` passed (carried) | `StudySessionsTests.cs:105` - `Assert.DoesNotContain(markerB[..4], prompt)`; `:108` - `Assert.Equal(chunks.Count, chunks.Distinct().Count())`; `:117` - `Assert.Contains(markerD[..4], all)` | PASS |
| C3 | 3 chunks, questionCount 10 -> 3 questions | `StudySessionsTests.Fewer_chunks_than_requested_caps_questions` passed (carried) | `StudySessionsTests.cs:131` - `Assert.Equal(3, body.GetProperty("questions").GetArrayLength())` | PASS |
| C4 | creation body hides the answer fields | `StudySessionsTests.Creation_never_reveals_the_answer` passed (carried) | `StudySessionsTests.cs:146` - `Assert.False(q.TryGetProperty(hidden, out _), hidden)` over the six names | PASS |
| C5 | 0, 3, 7, 21 -> 400 errors.questionCount; [] -> errors.documentIds; 5, 10, 20 accepted | `StudySessionsTests.Invalid_input_returns_400` passed, 9 rows (carried) | `StudySessionsTests.cs:173` - `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:174` - `...GetProperty("errors").TryGetProperty(field, out _)`; `:170` - `Assert.Equal(HttpStatusCode.Created, ...)` | PASS |
| C6 | foreign org, missing org, document of other org -> 404, model not called | `StudySessionsTests.Foreign_or_missing_returns_404` passed, 3 rows (carried) | `StudySessionsTests.cs:198` - `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:200` - `Assert.False(Factory.Chat.ReceivedCallContaining(marker[..4]))` | PASS |
| C7 | no documents -> 422, no model call | `StudySessionsTests.No_chunks_returns_422` passed (carried) | `StudySessionsTests.cs:213` - `Assert.Equal(HttpStatusCode.UnprocessableEntity, ...)`; `:215` - `Assert.Equal(before, await StudyCallsAsync())` | PASS |
| C8 | each of 11 invalid kinds (incl. `chunk` as text, `correct` as text) mixed with a valid item stores only the valid question | `StudySessionsTests.Invalid_model_items_are_discarded` passed, 11 rows (re-judged) | kinds at `StudySessionsTests.cs:218-220`, the two new items at `:241`-`:242` (`chunk = "2"`, `correct = "0"`); `:250` - `var question = Assert.Single(body.GetProperty("questions").EnumerateArray())`; `:251` - `Assert.Equal("Válida?", question.GetProperty("prompt").GetString())`; code: `src/BuildYourOwnAI.Api/Features/Study/CreateStudySession.cs:113,119` use `IsInt`, `:140-144` checks `ValueKind == Number` before `TryGetInt32` | PASS |
| C9 | throw / not JSON / top-level list / no valid item -> 502 without provider message, no session row | `StudySessionsTests.Model_failure_returns_502_without_saving` passed, 4 rows (re-judged) | `StudySessionsTests.cs:259` - `[InlineData("top-level-array")]`, reply at `:271` - `JsonSerializer.Serialize(new[] { Item(1) })`; `:277` - `Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode)`; `:279` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)`; `:280` - `Assert.Equal(0, ... count(*) from study_sessions where organization_id = @o ...)`; code: `CreateStudySession.cs:106` root `ValueKind != JsonValueKind.Object` returns empty -> `:68` 502 | PASS |
| C10 | stored correct_option points to "certa n"; positions not all equal | `StudySessionsTests.Options_are_shuffled_keeping_the_answer` passed (line refresh) | `StudySessionsTests.cs:299` - `Assert.StartsWith("certa ", ((string[])row[0]!)[(short)row[1]!])`; `:300` - `Assert.True(rows.Select(r => (short)r[1]!).Distinct().Count() > 1)` | PASS |
| C11 | 10 asks + 10 creations pass, 21st creation 429 | `StudySessionsTests.Shares_rate_limit_with_ask` passed (line refresh) | `StudySessionsTests.cs:314` - `Assert.Equal(HttpStatusCode.Created, ...)`; `:317` - `Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode)` | PASS |
| C18 | cascades, checks, unique (session_id, position) | `StudySessionsTests.Schema_has_study_tables` passed (line refresh) | `StudySessionsTests.cs:327` - `Assert.Equal(1, await Count(... delete_rule = 'CASCADE' ...))` per FK; `:333` - `Assert.Equal(1, ... conname = '{check}' and contype = 'c')`; `:334` - unique index count 1 | PASS |
| C19 | deleting a document removes its questions only; deleting the org removes sessions | `StudySessionsTests.Deletes_cascade_to_study_data` passed (line refresh) | `StudySessionsTests.cs:354` - `Assert.Equal(0, ... document_id = @d ... a)`; `:355` - `Assert.Equal(1, ... b)`; `:358` - `Assert.Equal(0, ... study_sessions where id = @s ...)` | PASS |
| C20 | both new routes without cookie -> 401 | `AuthTests.Protected_route_without_session_returns_401` passed (line refresh, file moved by `e8cb234`) | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:114` and `:115` - the two study routes; `:139` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C21 | no log carries chunk, prompt, option or explanation text | `StudySessionsTests.Study_never_logs_content` passed (line refresh) | `StudySessionsTests.cs:383` - `Assert.DoesNotContain(logged, text => text.Contains(secret))` | PASS |
| C12 | right answer -> 200, correct true, chosen/correct, explanation, source; row stored | `StudyAnswerTests.Right_answer_is_graded_correct` passed (carried) | `tests/BuildYourOwnAI.Api.Tests/Features/StudyAnswerTests.cs:44` - `Assert.True(body.GetProperty("correct").GetBoolean())`; `:47` - `Assert.Equal("explicação 1", ...)`; `:29`-`:31` source; `:49` - `Assert.Equal((short)stored.Correct, await ChosenAsync(...))`; `:50` - `answered_at is not null` count 1 | PASS |
| C13 | wrong answer -> correct false, chosen sent, correct stored, explanation, source | `StudyAnswerTests.Wrong_answer_returns_the_right_one` passed (carried) | `StudyAnswerTests.cs:62` - `Assert.False(...correct)`; `:63` - `Assert.Equal(wrong, ...chosenOption)`; `:64` - `Assert.Equal(stored.Correct, ...correctOption)` | PASS |
| C14 | second answer -> 409, first chosen_option kept | `StudyAnswerTests.Second_answer_returns_409` passed (carried) | `StudyAnswerTests.cs:79` - `Assert.Equal(HttpStatusCode.Conflict, again.StatusCode)`; `:81` - `Assert.Equal((short)first, await ChosenAsync(...))` | PASS |
| C15 | option -1 and 4 -> 400 errors.option | `StudyAnswerTests.Option_out_of_range_returns_400` passed, 3 rows (carried) | `StudyAnswerTests.cs:95` - `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:96` - `...TryGetProperty("option", out _)` | PASS |
| C16 | other user's session, missing session, question of other session -> 404, nothing stored | `StudyAnswerTests.Foreign_or_missing_returns_404` passed, 3 rows (carried) | `StudyAnswerTests.cs:117` - `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:119` - `Assert.Null(await ChosenAsync(stored.Question))` | PASS |
| C31 | 8 simultaneous answers -> exactly one 200, seven 409, stored chosen_option is the accepted one | `StudyAnswerTests.Simultaneous_answers_only_one_wins` passed (new, judged) | `StudyAnswerTests.cs:129-130` - `Task.WhenAll(Enumerable.Range(0, 8).Select(i => AnswerAsync(..., i % 4)))`; `:132` - `var accepted = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK)`; `:133` - `Assert.All(responses.Where(r => r != accepted), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode))`; `:135` - `Assert.Equal((short)chosen, await ChosenAsync(stored.Question))` | PASS |
| C22 | tabs Conversa, Estudar, Base in order; Estudar -> /organizations/o1/study | `organization has conversa, estudar and base tabs` ✓ (carried) | `src/web/src/features/study/study.test.tsx:70` - `toEqual(['Conversa', 'Estudar', 'Base'])`; `:71` - `toHaveAttribute('href', '/organizations/o1/study')` | PASS |
| C23 | boxes checked, 10 default, Começar; sends only checked and count 5 | `study setup sends chosen documents and count` ✓ (carried) | `study.test.tsx:83` - `radio '10' ... toBeChecked()`; `:91` - `expect(api.created).toEqual([{ documentIds: ['d2'], questionCount: 5 }])` | PASS |
| C24 | no documents -> message, link to Base, no Começar | `study without documents points to base` ✓ (carried) | `study.test.tsx:100` - `findByText('Suba documentos na Base para estudar')`; `:101` - `href '/organizations/o1/knowledge'`; `:102` - `Começar ... not.toBeInTheDocument()` | PASS |
| C25 | "Gerando perguntas…" and Começar disabled while pending | `study shows generating state` ✓ (carried) | `study.test.tsx:118` - `findByText('Gerando perguntas…')`; `:119` - `Começar ... toBeDisabled()` | PASS |
| C26 | "Pergunta 1 de 3", 4 options, Responder disabled until a choice | `study shows one question at a time` ✓ (carried) | `study.test.tsx:134` - `Responder ... toBeDisabled()`; `:136` - `toBeEnabled()` after a click | PASS |
| C27 | right: correct state, Certo!, explanation, no auto preview, Ver no documento opens it | `right answer shows certo and optional preview` ✓ (carried) | `study.test.tsx:150` - `toHaveAttribute('data-state', 'correct')`; `:152` - preview `not.toBeInTheDocument()`; `:158` - `toEqual(['d2/3'])` | PASS |
| C28 | wrong: wrong/correct states, "A resposta certa é: B1", preview opens on the source chunk | `wrong answer shows the right one with the preview` ✓ (carried) | `study.test.tsx:171` - `findByText('A resposta certa é: B1')`; `:172` - `data-state 'wrong'`; `:173` - `data-state 'correct'`; `:177` - `toEqual(['d2/3'])` | PASS |
| C29 | Próxima pergunta closes preview and shows 2 de 3; the last shows its correction and "Ver resultado"; click shows "Você acertou 1 de 3"; Estudar de novo returns to setup (AC 25 amended) | `study moves through questions to the result` ✓ (re-judged) | `study.test.tsx:191` - `findByText('Pergunta 2 de 3')`; `:192` - preview `not.toBeInTheDocument()`; `:199` - `await user.click(await screen.findByRole('button', { name: 'Ver resultado' }))` (findByRole throws if absent; the button renders only inside the correction block, `StudyPage.tsx:195-209`, label ternary at `:209`); `:201` - `findByText('Você acertou 1 de 3')`; `:203` - `findByRole('button', { name: 'Começar' })`. Matches amended plan AC 25 (`plan.md:123`) | PASS |
| C30 | 502 on creation shows title and keeps choices; 409 on answer shows title, keeps the chosen option and Responder enabled | `study errors show title and keep choices` ✓ (re-judged) | `study.test.tsx:216` - `toHaveTextContent('Não foi possível gerar perguntas. Tente novamente.')`; `:217` - `apostila-2.pdf ... not.toBeChecked()`; `:218` - `radio '20' ... toBeChecked()`; `:229` - `toHaveTextContent('Esta pergunta já foi respondida.')`; `:230` - `expect(option('A1')).toHaveAttribute('aria-pressed', 'true')`; `:231` - `expect(screen.getByRole('button', { name: 'Responder' })).toBeEnabled()` | PASS |

30/30 checks with located evidence.

## Coverage

Rows the fix touched: verified at `c3e6833`. Other rows: carried from `273dd9c` (their authority -
`AnswerStudyQuestion.cs`, `Entities.cs`, `Program.cs`, the plan's Surface - is unchanged by `7e6c269`).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| invalid model items (11) - verified at `c3e6833` | plan door 3 / AC 8; `CreateStudySession.cs:111-128` (each `continue` branch) | chunk out of range C8 · repeated chunk C8 · 3 options C8 · 5 options C8 · empty option C8 · repeated options C8 · correct 4 C8 · correct -1 C8 · empty prompt C8 · chunk non-number C8 (`IsInt`, `:113`) · correct non-number C8 (`:119`) | - |
| model failures (4) - verified at `c3e6833` | plan AC 9; `CreateStudySession.cs:58-68`, `:105-108`, `:133` | provider throws C9 · not JSON (`JsonException`, `:133`) C9 · root not an object (`:106`) C9 · no valid item (empty list -> `:68`) C9 | - |
| simultaneous answers (1) / answered-once guard (2 paths) - verified at `c3e6833` | Swept concurrency; `AnswerStudyQuestion.cs:37-44` (pre-read `ChosenOption is null` + conditional `ExecuteUpdateAsync` `Where(... ChosenOption == null)`) | sequential second answer C14 · 8 concurrent answers C31 | - |
| tab states (8, incl. both error halves) - verified at `c3e6833` | plan Observable + AC 17-26 (AC 25 amended) | no documents C24 · setup C23 · generating C25 · question C26 · right C27 · wrong C28 · end via "Ver resultado" C29 · creation error keeps choices C30 · answer error keeps chosen option + Responder enabled C30 | - |
| create statuses (7) - carried from `273dd9c` | plan Surface; `CreateStudySession.cs:36,39,44,54,68,77` + `:24` rate limit | 201 C1 · 400 C5 · 401 C20 · 404 C6 · 422 C7 · 429 C11 · 502 C9 | - |
| answer statuses (5) - carried | plan Surface; `AnswerStudyQuestion.cs:25,35,44,46` + auth | 200 C12 · 400 C15 · 401 C20 · 404 C16 · 409 C14, C31 | - |
| questionCount values (7 + null) - carried | `Entities.cs` `AllowedQuestionCounts`; C5 | 0, 3, 7, 21, null, 5, 10, 20 C5 | - |
| documentIds forms (4) - carried | `CreateStudySession.cs:33,41-44` | absent C2 · empty C5 · own org C1 · other org C6 | - |
| option edges (4) - carried | `AnswerStudyQuestion.cs:24` | -1 C15 · 4 C15 · null C15 · right C12 · wrong C13 | - |
| answer 404 (3) - carried | AC 16; `AnswerStudyQuestion.cs:30-35` | other user C16 · missing session C16 · question of other session C16 | - |
| doors (3) - carried, door 1 and 3 members refreshed | plan Landing | 1 C18, C19, C14, C31 · 2 C1, C12, C20 · 3 C8, C9, C10 | - |
| entities (2) - carried | plan Impact | `StudySession` C1, C19 · `StudyQuestion` C12, C19 | - |
| startup config: ask rate limit (1 assembly) - carried | `src/BuildYourOwnAI.Api/Program.cs:45`, `CreateStudySession.cs:24` | C11 (same `Program` booted by `WebApplicationFactory`) | - |

Recomputation note on `Parse`: after the fix, every typed accessor is guarded by a `ValueKind` test
(`:106` root, `:112` item, `:113`/`:119` numbers via `IsInt`, `:115` prompt string, `:117` options
array, `:124` option strings, `:129` explanation), so no remaining JSON shape reaches an uncaught
`InvalidOperationException`. A non-integer number (`1.5`) fails `TryGetInt32` and is discarded.

## Test policy rows

Carried from `273dd9c`, re-judged for the touched files (`CreateStudySession.cs`, the three test files):
`checks.md` adds no rows ("Os specs anteriores já respondem"), so the repo convention decides.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| repo convention: HTTP boundary vs real Postgres, web via Testing Library + MSW, fake AI | `Features/Study/*.cs`, `StudyPage.tsx` | route/status claims through `WebApplicationFactory` + Testcontainers (C8, C9, C31 are HTTP-level); screen claims through `renderApp` + MSW (C29, C30) | yes |

## Faults injected

Verified at `c3e6833`. Detached worktree under the session scratchpad
(`git worktree add --detach <scratch>/wt c3e6833`), `src/web/node_modules` linked by a junction
and the junction deleted before `git worktree remove`. Baseline porcelain of the real tree: empty;
after cleanup: empty. (`main` advanced to `e6322d3` during the run - another session's docs commit,
not this Verifier.) One mutation per run, `git checkout -- .` between them.

| Mutation | Location | Killed |
| --- | --- | --- |
| `q.ChosenOption == null` removed from the conditional UPDATE | `src/BuildYourOwnAI.Api/Features/Study/AnswerStudyQuestion.cs:39` | yes - C31 `Simultaneous_answers_only_one_wins` failed: `Assert.Single() Failure: The collection contained 8 matching items` |
| `IsInt` guard reverted to `chunk.TryGetInt32` / `correct.TryGetInt32` | `CreateStudySession.cs:113,119` | yes - C8 rows `chunk-as-text` and `correct-as-text` failed: `Expected: Created, Actual: InternalServerError` |
| root-is-object check dropped (`if (false \|\| !TryGetProperty(...))`) | `CreateStudySession.cs:106` | yes - C9 row `top-level-array` failed: `Expected: BadGateway, Actual: InternalServerError` |
| web: `onError: () => setChoice(null)` added to the answer mutation | `src/web/src/features/study/StudyPage.tsx:139` | yes - C30 `study errors show title and keep choices` failed at the `aria-pressed` assertion (`:230`) |

Carried from `273dd9c` (surfaces untouched by the fix, killed in round 1):

| Mutation | Location | Killed |
| --- | --- | --- |
| creation response carries `CorrectOption` | `CreateStudySession.cs` response record | yes - C4 |
| shuffle without moving the key | `CreateStudySession.cs` shuffle | yes - C10 |
| repeated-options validation dropped | `CreateStudySession.cs:125` | yes - C8 `repeated-options` |
| preview opens on right answers too | `StudyPage.tsx:143` | yes - C27 |

## Round-1 findings, resolved

1. Surviving `ChosenOption == null` mutant - C31 added; re-injected mutant now killed.
2. Malformed model JSON -> 500 - `IsInt` + root-kind guard; C8 (+2 rows) and C9 (+1 row) prove discard / 502; both reverts killed.
3. AC 25 wording - plan AC 25 amended with user confirmation (`c3e6833`, `plan.md:123`); C29 wording matches the "Ver resultado" flow and the test.
4. AC 26 answer-error half - C30 now asserts `aria-pressed="true"` and Responder enabled after the 409; clearing the choice is killed.

Minor note (not a gap): C29's "a última mostra a correção" is proven structurally - "Ver resultado"
exists only inside the correction block (`StudyPage.tsx:195-209`) - rather than by an explicit
assertion on the last question's "Certo!"/"A resposta certa é" text.

## Gate

API `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` - 254 passed, 0 failed. Web `npm --prefix src/web run test -- --reporter=verbose` - 78 passed, 0 failed (both at `c3e6833`).
