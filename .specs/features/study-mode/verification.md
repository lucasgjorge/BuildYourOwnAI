# Study Mode verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 2d13fc7..273dd9c (study-mode commits 330796b, 58ab02a, 63621eb, b50e89c, 87f2667, 273dd9c; the rest of the range is admin-usage/jev-choice docs)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Proofs and faults ran in a detached worktree at `273dd9c` (not in the real tree): the real tree carried
uncommitted changes of the `user-name` feature, and during the run another session committed
`7f8b252` and `e8cb234` (user-name) on `main`. Neither touches `Features/Study`, `features/study`
or the study tests (`git diff --stat 273dd9c..HEAD` over those paths is empty), so the evidence
below holds at the current HEAD too.

## Binding sources

No source is marked binding in `plan.md` (Sources: the 2026-09-24 conversation and `docs/PRD.md`, neither binding). Step 1 does not apply.

## Checks

API proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` at 273dd9c - 236 passed, 0 failed, exit 0; each named test below appears individually as `Passed` (theories: every row).
Web proof: `npm --prefix src/web run test -- --reporter=verbose` at 273dd9c - 72 passed, 0 failed, exit 0; each named test below appears as `✓ src/features/study/study.test.tsx > study mode > <name>`.
All test files are new in `63621eb` / `b50e89c` (AuthTests rows added in `63621eb`).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | 201, Location, id, createdAt, 5 questions, positions 1..5, 4 options | `StudySessionsTests.Create_returns_questions_with_four_options` passed | `tests/BuildYourOwnAI.Api.Tests/Features/StudySessionsTests.cs:76` - `Assert.Equal(HttpStatusCode.Created, response.StatusCode)`; `:79` - `Assert.Equal($"/api/study-sessions/{id}", response.Headers.Location?.OriginalString)`; `:83` - `Assert.Equal([1, 2, 3, 4, 5], ...position)`; `:88` - `Assert.Equal(4, q.GetProperty("options").GetArrayLength())` | PASS |
| C2 | only chosen documents in the prompt, no repeated chunk; absent documentIds uses all | `StudySessionsTests.Samples_only_chosen_documents_without_repeating` passed | `StudySessionsTests.cs:105` - `Assert.DoesNotContain(markerB[..4], prompt)`; `:108` - `Assert.Equal(chunks.Count, chunks.Distinct().Count())`; `:117` - `Assert.Contains(markerD[..4], all)` | PASS |
| C3 | 3 chunks, questionCount 10 -> 3 questions | `StudySessionsTests.Fewer_chunks_than_requested_caps_questions` passed | `StudySessionsTests.cs:131` - `Assert.Equal(3, body.GetProperty("questions").GetArrayLength())` | PASS |
| C4 | creation body has no correctOption/correct/explanation/source/documentId/chunkIndex | `StudySessionsTests.Creation_never_reveals_the_answer` passed | `StudySessionsTests.cs:146` - `Assert.False(q.TryGetProperty(hidden, out _), hidden)` over the six names at `:145` | PASS |
| C5 | 0, 3, 7, 21 -> 400 errors.questionCount; [] -> errors.documentIds; 5, 10, 20 accepted | `StudySessionsTests.Invalid_input_returns_400` passed (9 rows, incl. null) | `StudySessionsTests.cs:173` - `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:174` - `...GetProperty("errors").TryGetProperty(field, out _)`; `:170` - `Assert.Equal(HttpStatusCode.Created, ...)` for 5/10/20 | PASS |
| C6 | foreign org, missing org, document of other org -> 404, model not called | `StudySessionsTests.Foreign_or_missing_returns_404` passed (3 rows) | `StudySessionsTests.cs:198` - `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:200` - `Assert.False(Factory.Chat.ReceivedCallContaining(marker[..4]))` | PASS |
| C7 | no documents -> 422, no model call | `StudySessionsTests.No_chunks_returns_422` passed | `StudySessionsTests.cs:213` - `Assert.Equal(HttpStatusCode.UnprocessableEntity, ...)`; `:215` - `Assert.Equal(before, await StudyCallsAsync())` | PASS |
| C8 | each of the 9 invalid kinds is discarded, only the valid question stored | `StudySessionsTests.Invalid_model_items_are_discarded` passed (9 rows) | `StudySessionsTests.cs:247` - `Assert.Single(body.GetProperty("questions").EnumerateArray())`; `:248` - `Assert.Equal("Válida?", ...prompt)` | PASS |
| C9 | throw / not JSON / no valid item -> 502, no provider message, no session row | `StudySessionsTests.Model_failure_returns_502_without_saving` passed (3 rows) | `StudySessionsTests.cs:272` - `Assert.Equal(HttpStatusCode.BadGateway, ...)`; `:274` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)`; `:275` - `Assert.Equal(0, ... count(*) from study_sessions ...)` | PASS |
| C10 | stored correct_option points to "certa n"; positions not all equal | `StudySessionsTests.Options_are_shuffled_keeping_the_answer` passed | `StudySessionsTests.cs:294` - `Assert.StartsWith("certa ", ((string[])row[0]!)[(short)row[1]!])`; `:295` - `Assert.True(rows.Select(r => (short)r[1]!).Distinct().Count() > 1)` | PASS |
| C11 | 10 asks + 10 creations pass, 21st creation 429 | `StudySessionsTests.Shares_rate_limit_with_ask` passed | `StudySessionsTests.cs:309` - `Assert.Equal(HttpStatusCode.Created, ...)`; `:312` - `Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode)` | PASS |
| C18 | cascades, 3 checks, unique (session_id, position) | `StudySessionsTests.Schema_has_study_tables` passed | `StudySessionsTests.cs:322` - `Assert.Equal(1, ... delete_rule = 'CASCADE')` per FK; `:328` - `Assert.Equal(1, ... conname = '{check}' and contype = 'c')`; `:329` - unique index `(session_id, "position")` | PASS |
| C19 | deleting a document removes its questions only; deleting the org removes sessions | `StudySessionsTests.Deletes_cascade_to_study_data` passed | `StudySessionsTests.cs:349` - `Assert.Equal(0, ... document_id = a)`; `:350` - `Assert.Equal(1, ... document_id = b)`; `:353` - `Assert.Equal(0, ... study_sessions where id = @s)` | PASS |
| C20 | both new routes without cookie -> 401 | `AuthTests.Protected_route_without_session_returns_401` passed (both study rows) | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:109` and `:110` - the two study routes; `:133` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C21 | no log carries chunk, prompt, option or explanation text | `StudySessionsTests.Study_never_logs_content` passed | `StudySessionsTests.cs:378` - `Assert.DoesNotContain(logged, text => text.Contains(secret))` over the 4 secrets | PASS |
| C12 | right answer -> 200, correct true, chosen/correct, explanation, source; row stored | `StudyAnswerTests.Right_answer_is_graded_correct` passed | `tests/BuildYourOwnAI.Api.Tests/Features/StudyAnswerTests.cs:44` - `Assert.True(body.GetProperty("correct").GetBoolean())`; `:47` - `Assert.Equal("explicação 1", ...)`; `:29`-`:31` source fields; `:49` - `Assert.Equal((short)stored.Correct, await ChosenAsync(...))`; `:50` - `answered_at is not null` count 1 | PASS |
| C13 | wrong answer -> correct false, chosenOption sent, correctOption stored, explanation, source | `StudyAnswerTests.Wrong_answer_returns_the_right_one` passed | `StudyAnswerTests.cs:62` - `Assert.False(body.GetProperty("correct").GetBoolean())`; `:63` - `Assert.Equal(wrong, ...chosenOption)`; `:64` - `Assert.Equal(stored.Correct, ...correctOption)`; `:66` - `AssertSource(body, stored)` | PASS |
| C14 | second answer -> 409, first chosen_option kept | `StudyAnswerTests.Second_answer_returns_409` passed | `StudyAnswerTests.cs:79` - `Assert.Equal(HttpStatusCode.Conflict, again.StatusCode)`; `:81` - `Assert.Equal((short)first, await ChosenAsync(...))`. Sequential only - see Faults (conditional update mutant survived) | PASS |
| C15 | option -1 and 4 -> 400 errors.option | `StudyAnswerTests.Option_out_of_range_returns_400` passed (-1, 4, null) | `StudyAnswerTests.cs:95` - `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:96` - `...TryGetProperty("option", out _)`; `:97` - `Assert.Null(await ChosenAsync(...))` | PASS |
| C16 | other user's session, missing session, question of other session -> 404, nothing stored | `StudyAnswerTests.Foreign_or_missing_returns_404` passed (3 rows) | `StudyAnswerTests.cs:117` - `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:119` - `Assert.Null(await ChosenAsync(stored.Question))` | PASS |
| C22 | tabs Conversa, Estudar, Base in order; Estudar -> /organizations/o1/study | `organization has conversa, estudar and base tabs` ✓ | `src/web/src/features/study/study.test.tsx:70` - `expect(...map(l => l.textContent)).toEqual(['Conversa', 'Estudar', 'Base'])`; `:71` - `toHaveAttribute('href', '/organizations/o1/study')` | PASS |
| C23 | boxes checked, 10 default, Começar; sends only checked and count 5 | `study setup sends chosen documents and count` ✓ | `study.test.tsx:81` - `expect(first).toBeChecked()`; `:83` - `expect(screen.getByRole('radio', { name: '10' })).toBeChecked()`; `:91` - `expect(api.created).toEqual([{ documentIds: ['d2'], questionCount: 5 }])` | PASS |
| C24 | no documents -> message, link to Base, no Começar | `study without documents points to base` ✓ | `study.test.tsx:100` - `findByText('Suba documentos na Base para estudar')`; `:101` - `toHaveAttribute('href', '/organizations/o1/knowledge')`; `:102` - `queryByRole('button', { name: 'Começar' })).not.toBeInTheDocument()` | PASS |
| C25 | "Gerando perguntas…" and Começar disabled while pending | `study shows generating state` ✓ | `study.test.tsx:118` - `findByText('Gerando perguntas…')`; `:119` - `getByRole('button', { name: 'Começar' })).toBeDisabled()` | PASS |
| C26 | "Pergunta 1 de 3", 4 options, Responder disabled until a choice | `study shows one question at a time` ✓ | `study.test.tsx:133` - the 4 options present; `:134` - `Responder ... toBeDisabled()`; `:136` - `toBeEnabled()` after a click; `:132` - next question absent | PASS |
| C27 | right: data-state correct, Certo!, explanation, no auto preview, Ver no documento opens it | `right answer shows certo and optional preview` ✓ | `study.test.tsx:150` - `toHaveAttribute('data-state', 'correct')`; `:152` - `queryByRole('complementary', { name: 'Prévia do trecho' })).not.toBeInTheDocument()`; `:153` - `expect(api.chunksRequested).toEqual([])`; `:158` - `toEqual(['d2/3'])` after the click | PASS |
| C28 | wrong: wrong/correct states, "A resposta certa é: B1", explanation, preview opens on the source chunk | `wrong answer shows the right one with the preview` ✓ | `study.test.tsx:171` - `findByText('A resposta certa é: B1')`; `:172` - `data-state 'wrong'`; `:173` - `data-state 'correct'`; `:176` - `toHaveAttribute('aria-current', 'true')`; `:177` - `toEqual(['d2/3'])` | PASS |
| C29 | Próxima pergunta closes preview and shows 2 de 3; after the last, "Você acertou 1 de 3"; Estudar de novo returns to setup | `study moves through questions to the result` ✓ | `study.test.tsx:191` - `findByText('Pergunta 2 de 3')`; `:192` - preview `not.toBeInTheDocument()`; `:199` - click `Ver resultado`; `:201` - `findByText('Você acertou 1 de 3')`; `:203` - `findByRole('button', { name: 'Começar' })`. Precision gap: see finding 3 (AC 25 wording) | PASS |
| C30 | 502 on creation shows title and keeps choices; 409 on answer shows title | `study errors show title and keep choices` ✓ | `study.test.tsx:216` - `toHaveTextContent('Não foi possível gerar perguntas. Tente novamente.')`; `:217` - `apostila-2.pdf ... not.toBeChecked()`; `:218` - `radio '20' ... toBeChecked()`; `:229` - `toHaveTextContent('Esta pergunta já foi respondida.')` | PASS |

## Coverage

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| create statuses (7) | plan Surface; `CreateStudySession.cs:36,39,44,54,62,68,77` + rate limiter | 201 C1 · 400 C5 · 401 C20 · 404 C6 · 422 C7 · 429 C11 · 502 C9 | - |
| answer statuses (5) | plan Surface; `AnswerStudyQuestion.cs:177,187,196,198` + auth | 200 C12 · 400 C15 · 401 C20 · 404 C16 · 409 C14 | - |
| questionCount values (7 + null) | `Entities.cs` `AllowedQuestionCounts = [5, 10, 20]`; C5 claim | 0, 3, 7, 21, null, 5, 10, 20 C5 | - |
| documentIds forms (4) | `CreateStudySession.cs:33,41-44` | absent C2 · empty C5 · own org C1 · other org C6 (foreign user's org documents reach the same count check, covered by the filter) | - |
| invalid model items (door 3 list, 9 + malformed types) | plan door 3 / AC 8; `CreateStudySession.cs:111-126` | 9 listed kinds C8 · item whose `chunk` or `correct` is not a number: no proof, and a verifier probe shows it answers **500** (uncaught `InvalidOperationException` from `TryGetInt32`, `CreateStudySession.cs:112,118`) instead of being discarded | chunk/correct of non-number JSON type |
| model failures (AC 9) | plan AC 9; `CreateStudySession.cs:58-69,103-134` | throws C9 · not JSON C9 · no valid item C9 · JSON whose root is not an object (e.g. `[]`): no proof, probe shows **500** (`TryGetProperty` on a non-object throws, `CreateStudySession.cs:106`; only `JsonException` is caught at `:132`) | root not an object |
| option edges (4) | `AnswerStudyQuestion.cs:176` | -1 C15 · 4 C15 · null C15 · right C12 · wrong C13 | - |
| answer 404 (3) | AC 16; `AnswerStudyQuestion.cs:182-187` + `StudyQuestion` query filter | other user C16 · missing session C16 · question of other session C16 | - |
| answered-once guard (2 paths) | Swept "concurrency"; `AnswerStudyQuestion.cs:190-194` | sequential second answer C14 · concurrent answers (the `ChosenOption == null` condition in the UPDATE): no proof; removing it survives (Faults) | concurrent answers |
| tab states (8) | plan Observable + AC 17-26 | no documents C24 · setup C23 · generating C25 · question C26 · right C27 · wrong C28 · end C29 · creation error C30 · answer error keeps the chosen option (AC 26 "manter a escolha feita"): C30 asserts only the title at `study.test.tsx:229`, not that the choice is kept | answer error keeps the choice |
| doors (3) | plan Landing | 1 C18, C19, C14 · 2 C1, C12, C20 · 3 C8, C10 | - (members of door 3 above) |
| entities (2) | plan Impact | `StudySession` C1, C19 · `StudyQuestion` C12, C19 | - |
| startup config: ask rate limit (1 assembly) | `src/BuildYourOwnAI.Api/Program.cs:45` `limiter.AddPolicy(AskPipeline.RateLimitPolicy, ...)`, `CreateStudySession.cs:24` | C11 (same `Program` booted by `WebApplicationFactory`) | - |

## Test policy rows

`checks.md` says "Os specs anteriores já respondem. Sem linhas novas." - no rows of its own; the repo convention (AGENTS.md) decides.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| repo convention: HTTP boundary vs real Postgres, web via Testing Library + MSW | `Features/Study/*.cs`, `StudyPage.tsx` | every route/status claim through `WebApplicationFactory` + Testcontainers; every screen claim through `renderApp` + MSW; no real AI | yes |

## Faults injected

All in a detached worktree at 273dd9c; real tree untouched by this run (node_modules junction removed before the worktree).

| Mutation | Location | Killed |
| --- | --- | --- |
| creation response `Question` also carries `CorrectOption` | `src/BuildYourOwnAI.Api/Features/Study/CreateStudySession.cs:15,80` | yes - C4 `Creation_never_reveals_the_answer` failed |
| options shuffled but the key not moved (`CorrectOption = (short)g.Correct`) | `CreateStudySession.cs:94` | yes - C10 failed at `Assert.StartsWith("certa ", ...)` |
| repeated-options validation dropped from `Parse` | `CreateStudySession.cs:125` | yes - C8 `repeated-options` row failed (`Assert.Single` saw 2) |
| `q.ChosenOption == null` removed from the conditional UPDATE | `src/BuildYourOwnAI.Api/Features/Study/AnswerStudyQuestion.cs:191` | no - C14 `Second_answer_returns_409` still passed: the pre-read at `:190` answers 409 sequentially, so the Swept concurrency claim has no proof |
| web: preview opens on right answers too | `src/web/src/features/study/StudyPage.tsx:143` | yes - C27 failed (`complementary 'Prévia do trecho'` present) |

## Findings

1. **Surviving mutant - concurrency of answered-once (C14 / Swept concurrency).** `AnswerStudyQuestion.cs:191`: the UPDATE's `ChosenOption == null` guard is what makes two simultaneous answers unable to both win, and no test exercises it; C14 is satisfied by the read at `:190`. The code as written is correct (Postgres re-checks the WHERE on the locked row under READ COMMITTED), but the Swept line's claim is untested. Needs a test that fires two answers concurrently (or pre-reads before a direct DB write) and asserts exactly one 200 and one 409.
2. **Malformed model JSON returns 500, not 502 (AC 9, door 3).** `CreateStudySession.cs:106,112,118`: `TryGetProperty` on a non-object root and `TryGetInt32` on a non-number `chunk`/`correct` throw `InvalidOperationException`, which the `catch (JsonException)` at `:132` does not catch. Verifier probe (scratch test, not committed) against HEAD code: root `[]`, `"chunk": "1"`, `"correct": "0"` each answered `500 InternalServerError` instead of `502`/discard. No check enumerates these members.
3. **AC 25 wording vs shipped behaviour (precision gap on C29).** Plan AC 25: "WHEN a última pergunta é respondida THEN ... mostrar 'Você acertou X de M'". Code shows the correction of the last question and a "Ver resultado" button (`StudyPage.tsx:209`), and only the click shows the score (`:153`). The Handoff records this as "settled mid-build" but the plan's AC was not amended and C29 ("depois da última") is loose enough to pass either way. The plan should be amended to name "Ver resultado", with user confirmation.
4. **AC 26 answer-error half unproven.** On an answer failure the choice is kept in code (`StudyPage.tsx:121,193` - `choice` untouched), but C30 only asserts the title (`study.test.tsx:229`), not `aria-pressed`/selected state after the 409.

## Gate

API `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` - 236 passed, 0 failed. Web `npm --prefix src/web run test -- --reporter=verbose` - 72 passed, 0 failed (both at 273dd9c).
