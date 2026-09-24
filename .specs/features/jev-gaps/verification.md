# Organizações, Jev e Lacunas verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: a3edbe0..95be488
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Scope of this round: the fix diff `73b9262..95be488` plus every round-1 verdict that was not PASS (C50, the Gap -> Document coverage member, the C55 proof line). The fix also moved shared code out of the slices (`Features/Ask/AskAssistant.cs` pipeline -> `Common/AskPipeline.cs`, `UploadDocument.PrepareAsync` -> `Common/DocumentIngestion.cs`, `Features/Gaps/RecordGap.cs` -> `Common/GapRecorder.cs`, web types -> `src/web/src/features/<area>/types.ts`). The review was scoped by that diff, so every check whose code sits behind those files had its code citation refreshed, and faults were re-injected on the moved surfaces. The code diff was read in full: the moved bodies are byte-for-byte the round-1 bodies, apart from renames (`Response`/`Answer` -> `Answer.Text`, `internal` -> `public`). Web changes are type-only imports (no `*.test.tsx` touched).

Proof invocations (one per target, at 95be488, real tree), **verified at 95be488**:

- API: PowerShell, `$env:DOCKER_HOST = "npipe://./pipe/dockerDesktopLinuxEngine"; dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`: exit 0, **150 passed, 0 failed** (149 in round 1 + C63). Each named test is listed individually as `Passed`. The only lines starting with `Failed` are EF `Failed executing DbCommand` logs from the expected-duplicate tests, not test results.
- Web: `npm --prefix src/web run test -- --reporter=verbose`: exit 0, **37 passed, 0 failed** across 4 files. Each named test is listed with a check mark.
- Selector existence: every `FullyQualifiedName~X` in checks.md (51 distinct selectors, including the fixed C50 `ObservabilityTests.Jev_and_gaps_never_log_content` and the new C63 `GapsTests.Answer_links_document_and_deleting_it_unlinks_gap`) was grepped against the `Passed` lines of the API run. Every one matched at least one passed test (counts from 1 to 22). Both C55 `-t` names (`gaps loading and error states`, `gaps list error shows problem title`) appear as passed in the web run. The C50 and C63 filters were also run on their own, exactly as written in checks.md: `Total tests: 2`, both `Passed` (the round-1 vacuous-filter problem is gone).

## Binding sources

Carried from 73b9262 (the fix changed no interface).

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| no binding source. The plan's Sources are the 2026-09-24 conversation and docs/PRD.md, neither marked binding, and there is no design. The profile is standard, so step 1 does not apply | n/a | - | - |

## Checks

Proof run column: every proof re-run at 95be488 (both targets above). Evidence column: `verified at 95be488` = re-judged or citation refreshed because the test or the code behind it was touched by 95be488; `carried from 73b9262` = neither the test file nor the code behind it changed, and the cited lines were confirmed unchanged (`GapsTests.cs` only gained lines after `:344`; no other test file changed).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | POST org 1/100 chars trimmed -> 201, Location, id/name/createdAt | `OrganizationsTests.Create_valid_returns_201` x2 Passed | carried from 73b9262: `tests/BuildYourOwnAI.Api.Tests/Features/OrganizationsTests.cs:20` `Assert.Equal(HttpStatusCode.Created, ...)`; `:23` Location `/api/organizations/{id}`; `:24` trimmed name | PASS |
| C2 | name "", "   ", 101 -> 400 errors.name | `OrganizationsTests.Create_invalid_returns_400` x3 Passed | carried from 73b9262: `OrganizationsTests.cs:40` BadRequest; `:41` `TryGetProperty("name", out _)` | PASS |
| C3 | list [] then [B, A] with counts, never another user's | `OrganizationsTests.List_returns_only_own_newest_first_with_counts` Passed | carried from 73b9262: `OrganizationsTests.cs:54` length 0; `:66-68` `[b, a]`; `:71-74` counts 0/2, 0/1; `:75-76` createdAt | PASS |
| C4 | GET org 200 with documentCount and assistants[{id,name,routingDescription}] | `OrganizationsTests.Get_returns_assistants_and_document_count` Passed | carried from 73b9262: `OrganizationsTests.cs:98` documentCount 2; `:101-106` assistant ids, names, routingDescription / Null | PASS |
| C5 | 5 org {id} routes x {foreign, missing} -> 404 problem, foreign docs unchanged | `OrganizationsTests.Foreign_or_missing_organization_returns_404` x10 Passed | carried from 73b9262: `OrganizationsTests.cs:140` NotFound; `:141` problem+json; `:142` `Assert.Equal(1, await DocumentCountAsync(foreignId))` | PASS |
| C6 | POST assistant with org, 500-char routingDescription -> 201; absent -> null | `AssistantsTests.Create_valid_returns_201_with_location` x3 Passed | carried from 73b9262: `AssistantsTests.cs:26` Created; `:30` organizationId; `:33` routingDescription; `:35` Null | PASS |
| C7 | missing organizationId, 501 routingDescription, name/instructions -> 400 keyed | `AssistantsTests.Create_invalid_returns_400_keyed_by_field` x7 Passed | carried from 73b9262: `AssistantsTests.cs:59` BadRequest; `:60` `TryGetProperty(field, out _)` | PASS |
| C8 | foreign/missing organizationId -> 404, count unchanged | `AssistantsTests.Create_in_foreign_or_missing_organization_returns_404` x2 Passed | carried from 73b9262: `AssistantsTests.cs:77` NotFound; `:79` count equals `before` | PASS |
| C9 | GET assistant 200 with organizationId/organizationName/routingDescription | `AssistantsTests.Get_returns_organization_and_routing_description` Passed | carried from 73b9262: `AssistantsTests.cs:99-104` organizationName "ACME", routingDescription, createdAt | PASS |
| C10 | one doc cited by two assistants of the org | `AskTests.Assistants_of_same_organization_share_documents` Passed | verified at 95be488 (code moved): test `AskTests.cs:171` `Assert.Contains(...sources..., s => ...documentId == document)` over both assistants `:168`; code now `src/BuildYourOwnAI.Api/Common/AskPipeline.cs:99` retrieval by `c.Document.OrganizationId == organizationId` | PASS |
| C11 | ask in org A never cites B's doc | `AskTests.Retrieves_only_from_own_organization` Passed | verified at 95be488 (code moved): `AskTests.cs:192` `Assert.Equal([own], sources)`; `:193` `Assert.DoesNotContain(secret, Factory.Chat.PromptContaining(marker))`; code `AskPipeline.cs:99` | PASS |
| C12 | DELETE org 204, cascades, other org kept | `OrganizationsTests.Delete_returns_204_and_cascades` Passed | carried from 73b9262: `OrganizationsTests.cs:161` NoContent; `:162-166` counts 0; `:167-169` kept | PASS |
| C13 | same content twice in org 201 then 409; other org 201 | `DocumentsTests.Same_content_is_unique_per_organization` Passed | verified at 95be488 (code moved): `DocumentsTests.cs:229` Created; `:230` Conflict; `:231` Created; code `Common/DocumentIngestion.cs:29-30` org-scoped dedup, killed by fault 5 | PASS |
| C14 | rag-mvp upload tests on /api/organizations/{id}/documents | `DocumentsTests` 22 Passed incl. `Concurrent_duplicate_uploads_yield_one_201_one_409` | verified at 95be488 (code moved): `DocumentsTests.cs:32`, `:40`, `:58` 415, `:76` 413, `:97` 400, `:113` 422, `:127` 409, `:142-144` 502, `:159-163`, `:180` 204, `:216` `[Created, Conflict]`; code `UploadDocument.cs:39,43,71` and `DocumentIngestion.cs:30,35,39,54` | PASS |
| C15 | old routes -> 404/405 problem+json | `DocumentsTests.Old_assistant_document_routes_are_gone` Passed | carried from 73b9262: `DocumentsTests.cs:255` NotFound or MethodNotAllowed; `:256` problem+json | PASS |
| C16 | migration backfill | `MigrationTests.Backfill_creates_one_organization_per_assistant` Passed | carried from 73b9262: `MigrationTests.cs:51` 3 orgs; `:54-55`; `:57-62`; `:63-64` | PASS |
| C17 | final schema | `MigrationTests.Schema_has_organization_ownership_shape` Passed | carried from 73b9262: `MigrationTests.cs:73-75`, `:77-80`, `:83-89`, `:91-95` | PASS |
| C18 | router prompt only own assistants with non-blank description | `JevTests.Router_sees_only_own_assistants_with_description` Passed | verified at 95be488 (`JevAsk.cs` touched): `JevTests.cs:35-40` Contains; `:41-43` DoesNotContain; code `Features/Jev/JevAsk.cs:64` eligibility filter | PASS |
| C19 | confident choice 2 -> answered via 2nd assistant | `JevTests.Confident_choice_answers_through_chosen_assistant` Passed | verified at 95be488: `JevTests.cs:63` "answered"; `:65-67`; `:68-69` answer/found; `:70` sources; `:72` alternatives; code `JevAsk.cs:91-98` via `AskPipeline.AnswerAsync` | PASS |
| C20 | 5 eligible -> alternatives exactly 3 | `JevTests.Alternatives_are_capped_at_three` Passed | verified at 95be488: `JevTests.cs:90` `Assert.Equal(3, ...GetArrayLength())`; code `JevAsk.cs:15` `MaxOptions = 3`, `:98` | PASS |
| C21 | confident=false -> clarify, chat not called | `JevTests.Low_confidence_returns_clarify_without_answering` Passed | verified at 95be488: `JevTests.cs:108`, `:110` InRange 1..3, `:113` no answer, `:114` `Assert.False(Factory.Chat.ReceivedCallContaining(question))`; code `JevAsk.cs:88-89` | PASS |
| C22 | noMatch + 1 open gap with null org and assistant | `JevTests.No_match_records_gap_without_organization` Passed | verified at 95be488 (`GapRecorder` moved): `JevTests.cs:129` "noMatch"; `:130-132` count 1 with null org and assistant; code `JevAsk.cs:82` `GapRecorder.RecordAsync(db, ..., null, null, ...)` | PASS |
| C23 | router failures -> clarify, 5 candidates by name | `JevTests.Router_failure_falls_back_to_clarify` x4 Passed | verified at 95be488: `JevTests.cs:153` "clarify"; `:154` `["A","B","C","D","E"]`; code `JevAsk.cs:75`, `:120-123`, `:146` | PASS |
| C24 | no eligible -> 422, router not called | `JevTests.No_eligible_assistant_returns_422` x2 Passed | verified at 95be488: `JevTests.cs:170-172`; code `JevAsk.cs:68-71` | PASS |
| C25 | question bounds | `JevTests.Question_bounds` x4 Passed | verified at 95be488 (validator moved): `JevTests.cs:189` status, `:191` errors.question; code `JevAsk.cs:60` -> `AskPipeline.cs:27` `InvalidQuestion` | PASS |
| C26 | 10 ask + 10 jev, 21st -> 429 | `JevTests.Shares_rate_limit_with_ask` Passed | verified at 95be488 (policy constant moved): `JevTests.cs:207` TooManyRequests; `:208` problem+json; code `Program.cs:40` `AskPipeline.RateLimitPolicy`, `AskAssistant.cs` and `JevAsk.cs` both `RequireRateLimiting(AskPipeline.RateLimitPolicy)` | PASS |
| C27 | router never gets instructions or document markers | `JevTests.Router_never_receives_instructions_or_documents` Passed | verified at 95be488: `JevTests.cs:226-227` DoesNotContain both markers | PASS |
| C28 | answer failure after routing -> 502 without provider message | `JevTests.Answer_failure_after_routing_returns_502` Passed | verified at 95be488: `JevTests.cs:239-241`; code `JevAsk.cs:94-95` returns `AskPipeline.cs:57` chat failure | PASS |
| C29 | no router key -> 200 clarify | `UnconfiguredAiTests.Jev_without_router_key_falls_back_to_clarify` Passed | verified at 95be488 (`Program.cs` touched): `UnconfiguredAiTests.cs:52` OK; `:54` "clarify"; `Program.cs:51` `builder.Services.AddAi(builder.Configuration);` unchanged | PASS |
| C30 | Jev answered, switch to Professor | `jev answered shows who answered and switches` passed | verified at 95be488 (`JevPage.tsx` import-only change, test file unchanged): `src/web/src/features/jev/jev.test.tsx:34`, `:39`, `:41` | PASS |
| C31 | clarify lets the user pick | `jev clarify lets the user pick` passed | verified at 95be488 (import-only): `jev.test.tsx:56-60` | PASS |
| C32 | noMatch message | `jev no match points to gaps` passed | verified at 95be488 (import-only): `jev.test.tsx:72` | PASS |
| C33 | 422 message + link | `jev without eligible assistants explains how to enable` passed | verified at 95be488 (import-only): `jev.test.tsx:82-84` | PASS |
| C34 | pending disabled; 429/502 title, keep question | `jev loading and error states (429)`, `(502)` passed | verified at 95be488 (import-only): `jev.test.tsx:101`, `:103`, `:104` | PASS |
| C35 | found=false -> gap; found=true no gap | `GapsTests.Unanswered_question_opens_gap` Passed | verified at 95be488 (code moved): `GapsTests.cs:52` `Assert.False(...found)`; `:53-55` count 1 with org, assistant, ask_count 1; `:56-57` found true, 0 gaps; code `AskPipeline.cs:60-62`, `:80`. Fault 4 killed at `:52` | PASS |
| C36 | Jev-routed found=false -> gap with org and assistant | `GapsTests.Jev_routed_unanswered_question_opens_gap` Passed | verified at 95be488: `GapsTests.cs:71-75`; code `JevAsk.cs:91-93` -> `AskPipeline.cs:62` | PASS |
| C37 | normalized grouping | `GapsTests.Same_normalized_question_increments_count` Passed | verified at 95be488 (`GapRecorder` moved): `GapsTests.cs:97`, `:99` askCount 2, `:100`, `:102`, `:104-105`; code `Common/GapRecorder.cs:10` `Normalize`, `:20` ON CONFLICT | PASS |
| C38 | concurrent identical -> 1 gap ask_count 2 | `GapsTests.Concurrent_same_question_yields_one_gap` Passed | verified at 95be488: `GapsTests.cs:120-121`; code `GapRecorder.cs:17-23` | PASS |
| C39 | non-JSON reply -> raw text, found=true, no gap | `GapsTests.Unstructured_chat_reply_is_answered_without_gap` Passed | verified at 95be488: `GapsTests.cs:134-136`; code `AskPipeline.cs:85` `return (text, true);` | PASS |
| C40 | GET gaps own open only, ordered | `GapsTests.List_returns_own_open_gaps_most_asked_first` Passed | carried from 73b9262: `GapsTests.cs:161` order; `:163-171` fields (`ListGaps.cs` untouched) | PASS |
| C41 | answer 1/4000 -> 200, doc named, gap leaves list | `GapsTests.Answer_creates_document_and_closes_gap` x2 Passed | verified at 95be488 (`AnswerGap.cs` touched): `GapsTests.cs:186-190`; `:193` fileName; `:194` DoesNotContain; code `AnswerGap.cs:53-55`, `:73`, `:80` | PASS |
| C42 | answered gap retrievable | `GapsTests.Answered_gap_becomes_retrievable_knowledge` Passed | verified at 95be488: `GapsTests.cs:211-212` | PASS |
| C43 | org-less gap: 400 / 404 / 200 | `GapsTests.Gap_without_organization_requires_one` Passed | verified at 95be488: `GapsTests.cs:232-238`; code `AnswerGap.cs:42-49` | PASS |
| C44 | answer bounds, gap stays open | `GapsTests.Answer_bounds` x3 Passed | verified at 95be488: `GapsTests.cs:254-256`; code `AnswerGap.cs:29-33` | PASS |
| C45 | closed gap -> 409 | `GapsTests.Closed_gap_returns_409` Passed | verified at 95be488: `GapsTests.cs:274-277`; code `AnswerGap.cs:38-39`, `DismissGap.cs` untouched | PASS |
| C46 | embedding failure -> 502, gap open, no doc | `GapsTests.Embedding_failure_keeps_gap_open` Passed | verified at 95be488 (ingestion moved): `GapsTests.cs:291-294`; code `DocumentIngestion.cs:38-40` -> `AnswerGap.cs:56-57` | PASS |
| C47 | dismiss -> 204, status dismissed | `GapsTests.Dismiss_closes_gap` Passed | carried from 73b9262: `GapsTests.cs:306-308` | PASS |
| C48 | foreign/missing gap -> 404, foreign gap open | `GapsTests.Foreign_or_missing_gap_returns_404` x4 Passed | verified at 95be488: `GapsTests.cs:326-328`; code `AnswerGap.cs:35-37` | PASS |
| C49 | deleting assistant keeps gap | `GapsTests.Deleting_assistant_keeps_its_gaps` Passed | carried from 73b9262: `GapsTests.cs:339`, `:342-343` | PASS |
| C50 | no marker in any log; the unanswered ask logs `GapId` of the created gap before the gap is answered | `ObservabilityTests.Jev_and_gaps_never_log_content` Passed, in the full run and under the exact checks.md filter (`Total tests: 2` with C63) | verified at 95be488 (re-judged): `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:124` `var afterAsk = Factory.Logs.Entries.Count;` taken right after the unanswered ask; `:134-135` `Assert.Contains(Factory.Logs.Entries.Skip(before).Take(afterAsk - before), e => e.Properties.TryGetValue("GapId", out var v) && v?.ToString() == gap.ToString())` - the window ends before the Jev calls and the gap answer, and the id must equal the gap created by that ask; `:137-138` `Assert.DoesNotContain(logged, text => text.Contains(secret))` over all 6 markers incl. `FakeRouterClient.OutputMarker`. Code `Common/GapRecorder.cs:25`. The round-1 surviving mutant is now killed (fault 1) | PASS |
| C51 | nav gap count | `nav shows open gap count (0)`, `(1)` passed | verified at 95be488 (import-only change in `GapsPage.tsx`): `src/web/src/features/gaps/gaps.test.tsx:54` | PASS |
| C52 | gaps empty text | `gaps empty state` passed | verified at 95be488 (import-only): `gaps.test.tsx:63` | PASS |
| C53 | org-less gap needs org | `gaps answer requires organization when missing` passed | verified at 95be488 (import-only): `gaps.test.tsx:74`, `:78-79`, `:82`, `:87` | PASS |
| C54 | dismiss confirms | `gaps dismiss confirms` passed | verified at 95be488 (import-only): `gaps.test.tsx:100-104` | PASS |
| C55 | "Carregando lacunas…" while loading; problem title on list and answer errors | both proofs passed: `gaps loading and error states` and `gaps list error shows problem title` (the second is now on the checks.md Proof line) | verified at 95be488 (re-judged): `gaps.test.tsx:121` `expect(await screen.findByText('Carregando lacunas…')).toBeInTheDocument()`; `:126` `expect(await owned.findByRole('alert')).toHaveTextContent('O provedor de IA falhou.')` (answer error); `:135` `expect(await screen.findByRole('alert')).toHaveTextContent('Falha ao listar lacunas.')` (list error, MSW `problem(500, ...)` at `:131`) | PASS |
| C56 | /organizations empty + create | `organizations empty state and create` passed | verified at 95be488 (import-only change in organizations `api.ts`): `src/web/src/features/organizations/organizations.test.tsx:28`, `:32` | PASS |
| C57 | org page lists docs and assistants; form sends routingDescription | `organization page lists documents and assistants` passed | verified at 95be488 (import-only in `OrganizationPage.tsx`): `organizations.test.tsx:128-133`, `:139-141` | PASS |
| C58 | delete org confirms | `organization delete confirms` passed | carried from 73b9262: `organizations.test.tsx:69`, `:70`, `:73` | PASS |
| C59 | org page empty, loading, 404 | `organization page empty, loading and not found` passed | verified at 95be488 (import-only): `organizations.test.tsx:160`, `:162`, `:166` | PASS |
| C60 | /assistants/{id} links to org, no upload, sources | `assistant page asks and links to organization` passed | verified at 95be488 (assistants `api.ts` import-only): `organizations.test.tsx:279`, `:280`, `:285-286` | PASS |
| C61 | 14 routes without cookie -> 401, never 302 | `AuthTests.Protected_route_without_session_returns_401` x14 Passed | carried from 73b9262: `AuthTests.cs:129` Unauthorized over rows `:101-114`; `ApiFactory.cs:68` `AllowAutoRedirect = false` | PASS |
| C62 | rag-mvp ask/assistant tests inside an org | `AskTests` 13 Passed; `AssistantsTests.Foreign_or_missing_assistant_returns_404` x6 Passed | verified at 95be488 (ask code moved): `AskTests.cs:31` top-5 `[7, 6, 5, 4, 3]`; `:120-122` question bounds; `:137-139` 502 without provider message; `:154` 429; `AssistantsTests.cs:134` NotFound; code `AskAssistant.cs:37`, `:40`, `AskPipeline.cs:27`, `:49`, `:57` | PASS |
| C63 | answering writes `gaps.document_id`; deleting that document -> 204 and gap stays `answered` with null `document_id` | `GapsTests.Answer_links_document_and_deleting_it_unlinks_gap` Passed, in the full run and under the exact checks.md filter | verified at 95be488 (new): `tests/BuildYourOwnAI.Api.Tests/Features/GapsTests.cs:354` `Assert.Equal(1, await ScalarAsync("select count(*) from gaps where id = @g and document_id = @d", ("g", gap), ("d", documentId)))` with `documentId` read from the answer response at `:353`; `:358` `Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode)`; `:359-360` `Assert.Equal(1, ... "select count(*) from gaps where id = @g and document_id is null and status = 'answered'")`. Code `AnswerGap.cs:73` `SetProperty(g => g.DocumentId, document!.Id)`, `AppDbContext.cs:67` `OnDelete(DeleteBehavior.SetNull)`, migration `20260924131937_OrganizationsJevGaps.cs:120-125`. Killed faults 2 (at `:354`) and 3 (at `:359`) | PASS |

**Result: 63/63 checks proven with located evidence.**

## Coverage

Rows whose authority the fix touched were recomputed at 95be488 from the code (and, for Relations, from the plan plus the model and migration). The others are carried from 73b9262: their authority (route mapping, `CreateOrganization`/`CreateAssistant` validators, `ListGaps`, `DismissGap`, migrations of `InitialCreate`) is not in the fix diff.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/organizations` statuses (3) | carried from 73b9262: `CreateOrganization.cs:20,29` | 201 C1 · 400 C2 · 401 C61 | - |
| `GET /api/organizations` (2) | carried from 73b9262 | 200 C3 · 401 C61 | - |
| `GET /api/organizations/{id}` (3) | carried from 73b9262 | 200 C4 · 401 C61 · 404 C5 | - |
| `DELETE /api/organizations/{id}` (3) | carried from 73b9262 | 204 C12 · 401 C61 · 404 C5 | - |
| `POST /api/organizations/{id}/documents` (9) | verified at 95be488: `UploadDocument.cs:29` 404, `:32,36` 400, `:39` 415, `:43` 413, `:71` 201, `:62-64` race 409; `DocumentIngestion.cs:29-30` 409, `:35` 422, `:39-40` 502 | 201 C13/C14 · 400 C14 · 401 C61 · 404 C5 · 409 C13 · 413 C14 · 415 C14 · 422 C14 · 502 C14 | - |
| `GET /api/organizations/{id}/documents` (3) | carried from 73b9262 | 200 C14 · 401 C61 · 404 C5 | - |
| `DELETE /api/organizations/{id}/documents/{documentId}` (3) | carried from 73b9262 | 204 C14, C63 · 401 C61 · 404 C5 | - |
| `POST /api/assistants` (4) | carried from 73b9262 | 201 C6 · 400 C7 · 401 C61 · 404 C8 | - |
| `GET /api/assistants/{id}` (3) | carried from 73b9262 | 200 C9 · 401 C61 · 404 C62 | - |
| `POST /api/assistants/{id}/ask` (6) | verified at 95be488: `AskAssistant.cs:37` 404, `:40` 200; `AskPipeline.cs:27` 400, `:49-50` and `:57-58` 502; rate policy `Program.cs:40` | 200 C35/C62 · 400 C62 · 401 C61 · 404 C62 · 429 C26/C62 · 502 C62 | - |
| `POST /api/jev/ask` (6) | verified at 95be488: `JevAsk.cs:60-61` 400, `:68-71` 422, `:94-95` 502, `:106` 200 | 200 C19 · 400 C25 · 401 C61 · 422 C24 · 429 C26 · 502 C28 | - |
| `GET /api/gaps` (2) | carried from 73b9262 | 200 C40 · 401 C61 | - |
| `POST /api/gaps/{id}/answer` (6) | verified at 95be488: `AnswerGap.cs:29-33` 400, `:36-37` 404, `:38-39` 409, `:43-47` 400, `:48-49` 404, `:56-57` 502 (and 409 dup), `:65-67` 409, `:74-75` 409, `:80` 200 | 200 C41 · 400 C44/C43 · 401 C61 · 404 C48/C43 · 409 C45 · 502 C46 | - |
| `POST /api/gaps/{id}/dismiss` (4) | carried from 73b9262 | 204 C47 · 401 C61 · 404 C48 · 409 C45 | - |
| removed routes (4) | carried from 73b9262 | all four C15 | - |
| Jev `kind` (3) | verified at 95be488: `JevAsk.cs:75,80,83,89,98` | answered C19 · clarify C21 · noMatch C22 | - |
| router failures (5) | verified at 95be488: `JevAsk.cs:120-123`, `:126-129`, `:146`; `AiServiceCollectionExtensions.cs:44-47` | throw, non-JSON, 0, N+1 C23 · no key C29 | - |
| eligibility (4) | verified at 95be488: `JevAsk.cs:64` + owner query filter | with description, null, whitespace-only, other user: all C18 | - |
| ask chat output (3) | verified at 95be488: `AskPipeline.cs:71-86` `ParseReply` | found=true C35 · found=false C35 (fault 4) · non-JSON C39 | - |
| gap origin (3) | verified at 95be488: callers of `GapRecorder.RecordAsync` are `AskPipeline.cs:62` (used by `AskAssistant.cs:39` and `JevAsk.cs:91`) and `JevAsk.cs:82`; searched `rg "GapRecorder.RecordAsync" src` - only those two sites | direct ask C35 · Jev routed C36 · Jev noMatch C22 | - |
| gap grouping (4) | verified at 95be488: `GapRecorder.cs:10`, `:20` | normalized C37 · other org C37 · null org C37 · concurrent C38 | - |
| `Gap` transitions (5) | verified at 95be488: `AnswerGap.cs:38-39`, `:71-75`; `DismissGap.cs` carried | open C35 · open->answered C41, C63 · open->dismissed C47 · answered->x C45 · dismissed->x C45 | - |
| org `name` borders (5) | carried from 73b9262 | "", "   ", 101 C2 · 1, 100 C1 | - |
| `routingDescription` borders (3) | carried from 73b9262 | absent C6 · 500 C6 · 501 C7 | - |
| `organizationId` on assistant creation (3) | carried from 73b9262 | absent C7 · foreign C8 · missing C8 | - |
| Jev `question` borders (4) | verified at 95be488: `AskPipeline.cs:27-33` via `JevAsk.cs:60` | "", "   ", 2001 C25 · 2000 C25 | - |
| gap `answer` borders (5) | verified at 95be488: `AnswerGap.cs:29` | "", "   ", 4001 C44 · 1, 4000 C41 | - |
| `organizationId` on gap answer (3) | verified at 95be488: `AnswerGap.cs:42-49` | absent, foreign, own: all C43 | - |
| list caps (2) | verified at 95be488: `JevAsk.cs:15-16` | alternatives <= 3 C20 · fallback <= 5 C23 | - |
| new `{id}` routes x {foreign, missing} (14) | carried from 73b9262 | 10 org cases C5 · 4 gap cases C48 | - |
| protected routes without session (14) | carried from 73b9262 | C61, 14 rows Passed | - |
| screens (5) and Observable states (13) | verified at 95be488 for the pages whose imports changed (no rendering logic changed; test files unchanged) | Organizações C56, C58 · Organização C57, C59 · IA C60 · Jev C30-C34 · Lacunas C51-C55 (list error now on C55's Proof line) | - |
| doors (7) | verified at 95be488 for door 7 (authority: plan Landing + `AnswerGap.cs:51-80` + FK); others carried | 1 C10, C11, C16, C17 · 2 C5, C48, C18 · 3 C6, C17 · 4 C29, C18, C27 · 5 C19, C23 · 6 C35, C38 · 7 C41, C42, C63 | - |
| Relations delete behaviours (8) | verified at 95be488: plan Relations + `AppDbContext.cs:46,54,66,67` + migration `20260924131937_OrganizationsJevGaps.cs:113-125` | Org->Assistant cascade C12, C17 · Org->Document cascade C12, C17 · Document->Chunk cascade C12 · Org->Gap cascade C12 · Assistant->Gap set null C49 · Gap->Document set null C63 (fault 3) · User->Organization and User->Gap owner scoping C3, C40 | - |
| `Gap` -> `Document` link (2) | verified at 95be488: `AnswerGap.cs:73` write, `AppDbContext.cs:67` / migration `:120-125` set null | written on answer C63 `:354` (fault 2) · nulled when the document is deleted C63 `:359-360` (fault 3) | - |
| entities (5) | carried from 73b9262 | Organization C1, C12 · Assistant C6, C49 · Document C13, C41 · Chunk C12, C16 · Gap C35, C40 | - |
| startup config: keyed `router` client (2 assemblies) | verified at 95be488 (`Program.cs` touched): `src/BuildYourOwnAI.Api/Program.cs:51` `builder.Services.AddAi(builder.Configuration);` still present; test assembly `ApiFactory.cs:60` unchanged | `Program.cs` via `AddAi` C29 · `ApiFactory` keyed override C18 | - |

Observations that do not change a status set (carried from 73b9262, lines refreshed): `AnswerGap.cs:56-57` (dup content via `DocumentIngestion.cs:29-30`) and `:65-67` return 409 when the answer duplicates an existing document of the organization, and `:74-75` returns 409 when a concurrent close wins. These are extra causes of the 409 already proven by C45, and no AC names them. The configured-key OpenRouter branch has no proof by design (tests do not call providers).

## Test policy rows

Rows classifying files touched by 95be488 were re-judged at 95be488; there were no unmet rows in round 1. The migration row is carried from 73b9262 (migration untouched).

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| rag-mvp: endpoint that decides (validation, ownership, dedup, limits) | verified at 95be488: `Features/Ask/AskAssistant.cs`, `Features/Documents/UploadDocument.cs`, `Features/Gaps/AnswerGap.cs`, `Features/Jev/JevAsk.cs`, and the shared code they now call (`Common/AskPipeline.cs`, `Common/DocumentIngestion.cs`, `Common/GapRecorder.cs`); other slices carried | HTTP boundary against real Postgres: every accepted and rejected input, every error path | yes. Every status, border and error path in Coverage has a boundary proof; the lacuna -> document link (door 7) now has C63 at the boundary (answer + delete over HTTP, asserted in Postgres) |
| rag-mvp: pure decision behind an endpoint (chunker) | carried from 73b9262: `TextChunker` untouched | boundary + own level | yes. `TextChunkerTests` 5 Passed at 95be488. `GapRecorder.Normalize` is classified by the approved Evidence under the boundary row and is proven by C37 |
| rag-mvp: React component that decides what to show | verified at 95be488: `JevPage.tsx`, `GapsPage.tsx`, `OrganizationPage.tsx` (type-import changes only); others carried | Testing Library + MSW: empty, loading, error, success | yes. Lacunas: empty C52, loading C55 `:121`, list error C55 `:135`, answer error C55 `:126`, success C53. Jev: C30-C34. Organização: C57, C59 |
| rag-mvp: instrumentation (DI, route mapping, DTO) | verified at 95be488: `Program.cs` (rate policy constant), web `features/*/types.ts` | none of its own | yes, covered by consumer proofs (C26 for the rate policy, C61 for mapping, C29/C18 for keyed DI, all web tests compile and pass against the new types) |
| jev-gaps: migration with backfill | carried from 73b9262: `20260924131937_OrganizationsJevGaps.cs` | against real Postgres, from `InitialCreate` with data to the end | yes. C16, C17 Passed at 95be488 |

Swept rows resolving to **existing**: none (carried from 73b9262). Every Swept row in checks.md cites a check id of this feature.

## Faults injected

Verified at 95be488. Isolation: `git worktree add <scratchpad>/wt HEAD` (detached at 95be488). Baseline `git status --porcelain` of the real tree was empty before; the worktree was reset with `git checkout -- .` between faults; `git worktree remove --force` + `git worktree prune` afterwards; the real tree's porcelain was compared to the baseline and matched (empty). No `git stash`. An unmutated baseline run in the worktree of the C50 and C63 filters passed (2/2) before any fault.

| Mutation | Location | Killed |
| --- | --- | --- |
| round-1 surviving mutant re-injected: gap-creation log removed (`logger.LogInformation("Gap {GapId} recorded ...")` -> `_ = ids.Single();`) | `src/BuildYourOwnAI.Api/Common/GapRecorder.cs:25` | yes. `ObservabilityTests.Jev_and_gaps_never_log_content` Failed: `Assert.Contains() Failure: Filter not matched in collection` (`CrossCuttingTests.cs:134`) |
| answering stops writing the link (`.SetProperty(g => g.DocumentId, document!.Id)` dropped) | `src/BuildYourOwnAI.Api/Features/Gaps/AnswerGap.cs:73` | yes. `GapsTests.Answer_links_document_and_deleting_it_unlinks_gap` Failed at `GapsTests.cs:354` (Expected 1, Actual 0) |
| gap -> document FK `ReferentialAction.SetNull` -> `ReferentialAction.Cascade` (deleting the document silently deletes the answered gap) | `src/BuildYourOwnAI.Api/Infrastructure/Data/Migrations/20260924131937_OrganizationsJevGaps.cs:125` | yes. `GapsTests.Answer_links_document_and_deleting_it_unlinks_gap` Failed at `GapsTests.cs:359` (Expected 1, Actual 0) |
| moved ask pipeline: `ParseReply` treats `found=false` as true (`found.GetBoolean()` -> `true`) | `src/BuildYourOwnAI.Api/Common/AskPipeline.cs:80` | yes. `GapsTests.Unanswered_question_opens_gap` Failed at `GapsTests.cs:52` (`Assert.False() Failure`) |
| moved ingestion: dedup check loses its organization scope (`d.OrganizationId == organizationId && ` removed) | `src/BuildYourOwnAI.Api/Common/DocumentIngestion.cs:29` | yes. `DocumentsTests.Same_content_is_unique_per_organization` Failed at `DocumentsTests.cs:231` (Expected Created, Actual Conflict) |

Cap of five reached. Not re-injected (carried from 73b9262, where each was killed and whose code the fix only moved or did not touch): Jev index bound `JevAsk.cs:146`, `GapRecorder.Normalize` lower-casing, retrieval org filter (now `AskPipeline.cs:99`), Lacunas answer button disable in `GapsPage.tsx`.

## Gate

- `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` (PowerShell, DOCKER_HOST set) at 95be488: 150 passed, 0 failed
- `npm --prefix src/web run test -- --reporter=verbose` at 95be488: 37 passed, 0 failed
- C50 + C63 exact checks.md filters: 2 passed, 0 failed (no vacuous filter)

Round-1 gaps, all closed at 95be488:

1. C50 selector and creation-log assertion: closed (`CrossCuttingTests.cs:124`, `:134-135`; fault 1 killed).
2. Gap -> Document link (door 7): closed by C63 (`GapsTests.cs:354`, `:358-360`; faults 2 and 3 killed).
3. C55 list-error proof: closed (second Proof line in checks.md; `gaps.test.tsx:135`).
