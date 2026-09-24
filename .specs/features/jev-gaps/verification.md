# Organizações, Jev e Lacunas verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: a3edbe0..73b9262
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Verified at 73b9262. Every check in C1-C62 was evaluated over the whole range (5d5ebaf docs, 2580098 api, 73b9262 web), not only the last commit.

Proof invocations (one per target, at HEAD, real tree):

- API: PowerShell, `$env:DOCKER_HOST = "npipe://./pipe/dockerDesktopLinuxEngine"; dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`: exit 0, 149 passed, 0 failed. Each named test is listed individually as `Passed` in the output. A first attempt from Git Bash with `DOCKER_HOST=npipe:////./pipe/...` failed all 143 container tests with "The endpoint is not a npipe URI". That was an environment problem in the invocation, not in the code, so it was discarded and rerun from PowerShell.
- Web: `npm --prefix src/web run test -- --reporter=verbose`: exit 0, 37 passed across 4 files. Each named test is listed with a check mark.
- Existence cross-check: every `FullyQualifiedName~X` in checks.md was grepped against the list of passed tests. All of them match at least one passed test **except C50**. See below.

## Binding sources

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| no binding source. The plan's Sources are the 2026-09-24 conversation and docs/PRD.md, neither marked binding, and there is no design. The profile is standard, so step 1 does not apply | n/a | - | - |

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | POST org 1/100 chars trimmed -> 201, Location, id/name/createdAt | API run: `OrganizationsTests.Create_valid_returns_201` (1),(100) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/OrganizationsTests.cs:20` `Assert.Equal(HttpStatusCode.Created, ...)`; `:23` `Assert.Equal($"/api/organizations/{id}", response.Headers.Location?.OriginalString)`; `:24` `Assert.Equal(name, body.GetProperty("name").GetString())` | PASS |
| C2 | name "", "   ", 101 chars -> 400 errors.name | `OrganizationsTests.Create_invalid_returns_400` x3 Passed | `OrganizationsTests.cs:40` `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:41` `...GetProperty("errors").TryGetProperty("name", out _)` | PASS |
| C3 | list [] then [B, A] with counts 0/2 and 0/1, same name/createdAt, never another user's | `OrganizationsTests.List_returns_only_own_newest_first_with_counts` Passed | `OrganizationsTests.cs:54` `Assert.Equal(0, ...GetArrayLength())`; `:66-68` `Assert.Equal(2, list.GetArrayLength())`, `Assert.Equal(b, list[0]...)`, `Assert.Equal(a, list[1]...)`; `:71-74` assistantCount 0/2, documentCount 0/1; `:75-76` `AssertSameInstant(createdB..., list[0]...createdAt)` | PASS |
| C4 | GET org 200 with id/name/createdAt/documentCount/assistants[{id,name,routingDescription}] | `OrganizationsTests.Get_returns_assistants_and_document_count` Passed | `OrganizationsTests.cs:98` `Assert.Equal(2, body.GetProperty("documentCount").GetInt32())`; `:101-106` assistant ids, names, `routingDescription` "respostas curtas" / `JsonValueKind.Null` | PASS |
| C5 | 5 org {id} routes x {foreign, missing} -> 404 problem, foreign docs unchanged | `OrganizationsTests.Foreign_or_missing_organization_returns_404` x10 Passed | `OrganizationsTests.cs:140` `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:141` `Assert.Equal("application/problem+json", ...)`; `:142` `Assert.Equal(1, await DocumentCountAsync(foreignId))`; routes at `:114-118` | PASS |
| C6 | POST assistant with org, 500-char routingDescription -> 201 with all fields; absent -> null | `AssistantsTests.Create_valid_returns_201_with_location` x3 Passed | `AssistantsTests.cs:26` Created; `:30` `Assert.Equal(organizationId, body.GetProperty("organizationId").GetGuid())`; `:33` `Assert.Equal(routingDescription, ...)`; `:35` `Assert.Equal(JsonValueKind.Null, body.GetProperty("routingDescription").ValueKind)`; data `:14` `InlineData(10, 4000, 500)` | PASS |
| C7 | missing organizationId -> errors.organizationId; 501 -> errors.routingDescription; name/instructions kept | `AssistantsTests.Create_invalid_returns_400_keyed_by_field` x7 Passed | `AssistantsTests.cs:59` BadRequest; `:60` `...GetProperty("errors").TryGetProperty(field, out _)`; rows `:41-47` | PASS |
| C8 | foreign/missing organizationId -> 404, assistant count unchanged | `AssistantsTests.Create_in_foreign_or_missing_organization_returns_404` x2 Passed | `AssistantsTests.cs:77` NotFound; `:79` `Assert.Equal(before, await ScalarAsync("select count(*) from assistants"))` | PASS |
| C9 | GET assistant 200 with organizationId/organizationName/routingDescription/... | `AssistantsTests.Get_returns_organization_and_routing_description` Passed | `AssistantsTests.cs:99-104` `Assert.Equal("ACME", body.GetProperty("organizationName").GetString())`, `Assert.Equal("quando a pessoa quer entender", ...routingDescription...)`, `AssertSameInstant(...createdAt...)` | PASS |
| C10 | one doc cited by two assistants of the org | `AskTests.Assistants_of_same_organization_share_documents` Passed | `AskTests.cs:171` `Assert.Contains(body.GetProperty("sources").EnumerateArray(), s => s.GetProperty("documentId").GetGuid() == document)` in a loop over both assistants (`:168`) | PASS |
| C11 | ask in org A never cites B's doc even when B's is more similar | `AskTests.Retrieves_only_from_own_organization` Passed | `AskTests.cs:192` `Assert.Equal([own], sources)`; `:193` `Assert.DoesNotContain(secret, Factory.Chat.PromptContaining(marker))` | PASS |
| C12 | DELETE org 204; no assistants/documents/chunks/gaps of it remain; other org's remain | `OrganizationsTests.Delete_returns_204_and_cascades` Passed | `OrganizationsTests.cs:161` NoContent; `:162-166` counts 0 for organizations/assistants/documents/chunks/gaps; `:167-169` kept org 1/1/1 | PASS |
| C13 | same content twice in org -> 201 then 409; other org -> 201 | `DocumentsTests.Same_content_is_unique_per_organization` Passed | `DocumentsTests.cs:229` Created; `:230` Conflict; `:231` Created (globex) | PASS |
| C14 | rag-mvp upload tests pass on /api/organizations/{id}/documents | `DocumentsTests` 22 Passed incl. `Concurrent_duplicate_uploads_yield_one_201_one_409` | `DocumentsTests.cs:32` Created + `:40` chunk count; `:58` 415 by extension; `:76` 413/201 at 10485760; `:97` 400; `:113` 422; `:127` 409; `:142-144` 502 without provider message, nothing persisted; `:159-163` list newest first; `:180` 204; `:216` `Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses)` | PASS |
| C15 | old assistant document routes and GET /api/assistants -> 404/405 problem+json | `DocumentsTests.Old_assistant_document_routes_are_gone` Passed | `DocumentsTests.cs:255` `Assert.Contains(response.StatusCode, new[] { NotFound, MethodNotAllowed })`; `:256` `Assert.Equal("application/problem+json", ...)` over the 4 requests `:247-250` | PASS |
| C16 | migration backfill: 3 orgs with name+owner, docs in origin org, same ids, chunks intact | `MigrationTests.Backfill_creates_one_organization_per_assistant` Passed | `MigrationTests.cs:51` `Assert.Equal(3, ... "select count(*) from organizations")`; `:54-55` per-assistant join on owner_id + name; `:57-62` doc ids in the org of their origin assistant, 0 for the assistant with no docs; `:63-64` chunk counts 1/2 | PASS |
| C17 | final schema: unique (organization_id, content_sha256), no assistant_id/owner_id, NOT NULL + CASCADE, routing_description varchar(500) null | `MigrationTests.Schema_has_organization_ownership_shape` Passed | `MigrationTests.cs:73-75` unique index like `(organization_id, content_sha256)`; `:77-80` 0 columns; `:83-89` `is_nullable = 'NO'` and `delete_rule = 'CASCADE'` for both tables; `:91-95` varchar 500 nullable | PASS |
| C18 | router prompt: only own assistants with non-blank description, with name + org | `JevTests.Router_sees_only_own_assistants_with_description` Passed | `JevTests.cs:35-40` `Assert.Contains("Direto"/"ACME"/directDescription/"Professor"/"Globex"/teacherDescription, prompt)`; `:41-43` `Assert.DoesNotContain("SemDescricao"/"SoEspacos"/foreignDescription, prompt)` | PASS |
| C19 | choice 2 confident -> answered with the 2nd assistant, answer/found/sources, alternatives | `JevTests.Confident_choice_answers_through_chosen_assistant` Passed | `JevTests.cs:63` `Assert.Equal("answered", ...kind)`; `:65-67` id/name/"Globex"; `:68-69` answer/found; `:70` `Assert.Equal([globexDoc], ...sources...)`; `:72` `Assert.Equal([direct], Ids(alternatives))` | PASS |
| C20 | 5 eligible -> alternatives exactly 3 | `JevTests.Alternatives_are_capped_at_three` Passed | `JevTests.cs:90` `Assert.Equal(3, body.GetProperty("alternatives").GetArrayLength())`; `:91` chosen not included | PASS |
| C21 | confident=false -> clarify, 1-3 candidates, no answer, chat not called | `JevTests.Low_confidence_returns_clarify_without_answering` Passed | `JevTests.cs:108` "clarify"; `:110` `Assert.InRange(candidates.Count, 1, 3)`; `:113` `Assert.False(body.TryGetProperty("answer", out _))`; `:114` `Assert.False(Factory.Chat.ReceivedCallContaining(question))` | PASS |
| C22 | choice null confident -> noMatch + 1 open gap, org and assistant null | `JevTests.No_match_records_gap_without_organization` Passed | `JevTests.cs:129` "noMatch"; `:130-132` count 1 `... status = 'open' and organization_id is null and assistant_id is null` | PASS |
| C23 | router throw / non-JSON / 0 / N+1 -> clarify with <=5 candidates ordered by name | `JevTests.Router_failure_falls_back_to_clarify` THROW, TEXT, 0, 7 Passed | `JevTests.cs:153` "clarify"; `:154` `Assert.Equal(["A","B","C","D","E"], ...candidates names)` with 6 eligible (`:146`) | PASS |
| C24 | no eligible (none / only undescribed) -> 422 problem, router not called | `JevTests.No_eligible_assistant_returns_422` x2 Passed | `JevTests.cs:170` UnprocessableEntity; `:171` problem+json; `:172` `Assert.False(Factory.Router.ReceivedCallContaining(question))` | PASS |
| C25 | question "", "   ", 2001 -> 400 errors.question; 2000 not 400 | `JevTests.Question_bounds` x4 Passed | `JevTests.cs:189` `Assert.Equal(expected, response.StatusCode)` (2000 -> OK at `:180`); `:191` `...TryGetProperty("question", out _)` | PASS |
| C26 | 10 ask + 10 jev pass, 21st jev -> 429 problem | `JevTests.Shares_rate_limit_with_ask` Passed | `JevTests.cs:202`/`:204` OK x20; `:207` `Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode)`; `:208` problem+json | PASS |
| C27 | router never gets instructions or document markers | `JevTests.Router_never_receives_instructions_or_documents` Passed | `JevTests.cs:226` `Assert.DoesNotContain(instructionsMarker, prompt)`; `:227` `Assert.DoesNotContain(documentMarker, prompt)` | PASS |
| C28 | answer fails after routing -> 502 problem without provider message | `JevTests.Answer_failure_after_routing_returns_502` Passed | `JevTests.cs:239` BadGateway; `:240` problem+json; `:241` `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)` | PASS |
| C29 | no AI:OpenRouter:ApiKey -> app starts, jev -> 200 clarify | `UnconfiguredAiTests.Jev_without_router_key_falls_back_to_clarify` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/UnconfiguredAiTests.cs:52` OK; `:54` `Assert.Equal("clarify", ...)`. The fake router's default is choice 1 confident (`FakeAi.cs:143`), so clarify here can only come from `UnconfiguredAiClient` | PASS |
| C30 | Jev answered shows "Respondido por Direto · ACME", switches to Professor | web run: `jev answered shows who answered and switches` passed | `src/web/src/features/jev/jev.test.tsx:34` `findByText('Respondido por Direto · ACME')`; `:39` `findByText('Respondido por Professor · ACME')`; `:41` `expect(asked).toEqual([{ question: 'O que é fotossíntese?' }])` | PASS |
| C31 | clarify shows the question prompt and one button per candidate; click asks that assistant | `jev clarify lets the user pick` passed | `jev.test.tsx:56` `findByText('Qual destas IAs deve responder?')`; `:57-58` candidate buttons; `:60` `findByText('Respondido por Professor · ACME')` | PASS |
| C32 | noMatch message | `jev no match points to gaps` passed | `jev.test.tsx:72` `findByText('Nenhuma IA sabe responder isso ainda. A pergunta foi para Lacunas.')` | PASS |
| C33 | 422 message + link to /organizations | `jev without eligible assistants explains how to enable` passed | `jev.test.tsx:82-83` alert `toHaveTextContent("Preencha 'Quando usar esta IA' em pelo menos uma IA para usar o Jev")`; `:84` link `toHaveAttribute('href', '/organizations')` | PASS |
| C34 | pending "Jev está escolhendo…" disabled; 429/502 show title, keep question | `jev loading and error states (429)`, `(502)` passed | `jev.test.tsx:101` `findByRole('button', { name: 'Jev está escolhendo…' })).toBeDisabled()`; `:103` alert `toHaveTextContent(title)`; `:104` `toHaveValue('Qual o horário?')` | PASS |
| C35 | found=false -> 200 found=false + 1 open gap (org, assistant, ask_count 1); found=true no gap | `GapsTests.Unanswered_question_opens_gap` Passed | `GapsTests.cs:51-52` answer / `Assert.False(...found)`; `:53-55` count 1 `... organization_id = @o and assistant_id = @a and ask_count = 1`; `:56-57` found true, 0 gaps | PASS |
| C36 | Jev-routed found=false -> answered, found=false, gap with org and assistant | `GapsTests.Jev_routed_unanswered_question_opens_gap` Passed | `GapsTests.cs:71-72` "answered", found false; `:73-75` count 1 with `organization_id = @o and assistant_id = @a` | PASS |
| C37 | normalized repeat -> 1 gap askCount 2, last > first; other org -> new gap; 2 noMatch -> 1 null-org gap askCount 2 | `GapsTests.Same_normalized_question_increments_count` Passed | `GapsTests.cs:97` 2 gaps for the question; `:99` `Assert.Equal(2, acme...askCount)`; `:100` lastAskedAt > firstAskedAt; `:102` globex 1; `:104-105` jev askCount 2, organization Null | PASS |
| C38 | concurrent identical -> 1 open gap with ask_count 2 | `GapsTests.Concurrent_same_question_yields_one_gap` Passed | `GapsTests.cs:120` `Assert.Equal(1, await OpenGapCountAsync(question))`; `:121` `Assert.Equal(2, ... "select ask_count ...")` | PASS |
| C39 | non-JSON chat reply -> raw text, found=true, no gap | `GapsTests.Unstructured_chat_reply_is_answered_without_gap` Passed | `GapsTests.cs:134` `Assert.Equal("fake answer", ...)`; `:135` found true; `:136` 0 gaps | PASS |
| C40 | GET gaps: own open only, fields, askCount desc then lastAskedAt desc | `GapsTests.List_returns_own_open_gaps_most_asked_first` Passed | `GapsTests.cs:161` `Assert.Equal([thrice, jevQuestion, later, once], ...question)`; `:163-171` askCount 3, first < last, organization/assistant id+name, nulls for jev gap, id | PASS |
| C41 | answer 1/4000 -> 200 id/status/documentId; doc in org list with "Lacuna - " + 60 chars + ".md"; gap leaves list | `GapsTests.Answer_creates_document_and_closes_gap` (1),(4000) Passed | `GapsTests.cs:186-190` OK, id, "answered", documentId; `:193` `Assert.Equal($"Lacuna - {question[..60]}.md", document.GetProperty("fileName").GetString())`; `:194` `Assert.DoesNotContain(await ListAsync(client), g => ...== gap)` | PASS |
| C42 | after answer, another assistant of the org cites the answer doc, excerpt contains answer | `GapsTests.Answered_gap_becomes_retrievable_knowledge` Passed | `GapsTests.cs:211` `Assert.Single(...sources..., s => ...documentId == documentId)`; `:212` `Assert.Contains(answer, source.GetProperty("excerpt").GetString())` | PASS |
| C43 | org-less gap: no organizationId -> 400; foreign -> 404; own -> 200 doc in that org | `GapsTests.Gap_without_organization_requires_one` Passed | `GapsTests.cs:232-233` BadRequest + errors.organizationId; `:234` NotFound; `:236` OK; `:238` count 1 `documents where id = @d and organization_id = @o` | PASS |
| C44 | answer "", "   ", 4001 -> 400 errors.answer, gap stays open | `GapsTests.Answer_bounds` x3 Passed | `GapsTests.cs:254-255` BadRequest + errors.answer; `:256` `Assert.Equal(1, await OpenGapCountAsync(question))` | PASS |
| C45 | answer/dismiss on answered and dismissed gap -> 409 problem | `GapsTests.Closed_gap_returns_409` Passed | `GapsTests.cs:274-277` Conflict + problem+json for answer and dismiss, looped over both gaps (`:270`) | PASS |
| C46 | embedding fails on answer -> 502 without message, gap still listed, no doc | `GapsTests.Embedding_failure_keeps_gap_open` Passed | `GapsTests.cs:291-294` BadGateway, no ProviderSecretMessage, `Assert.Contains(await ListAsync(client), ...gap)`, `Assert.Equal(0, await DocumentCountAsync(organization))` | PASS |
| C47 | dismiss open -> 204, leaves list, status dismissed | `GapsTests.Dismiss_closes_gap` Passed | `GapsTests.cs:306-308` NoContent; DoesNotContain in list; count 1 `status = 'dismissed'` | PASS |
| C48 | answer/dismiss foreign or missing gap -> 404, foreign gap stays open | `GapsTests.Foreign_or_missing_gap_returns_404` x4 Passed | `GapsTests.cs:326-328` NotFound, problem+json, `Assert.Equal(1, await OpenGapCountAsync(question))` | PASS |
| C49 | deleting assistant keeps gap with assistant null, same organization | `GapsTests.Deleting_assistant_keeps_its_gaps` Passed | `GapsTests.cs:339` NoContent; `:342` `Assert.Equal(JsonValueKind.Null, gap.GetProperty("assistant").ValueKind)`; `:343` same organization id | PASS |
| C50 | no marker in any log after ask/jev answered/jev noMatch/gap answer; **gap creation** logs GapId | **Proof as written matches no test**: `dotnet test ... --filter "FullyQualifiedName~CrossCuttingTests.Jev_and_gaps_never_log_content"` printed "No test matches the given testcase filter" and **exited 0**. The test's real FQN is `ObservabilityTests.Jev_and_gaps_never_log_content` (class at `CrossCuttingTests.cs:77`). Under that name it Passed in the full run | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:134-135` `Assert.DoesNotContain(logged, text => text.Contains(secret))` covers the markers. But `:132` `Assert.Contains(entries, e => e.Properties.ContainsKey("GapId"))` does not target gap **creation**: the same test answers a gap (`:128`), and `AnswerGap.cs:79` logs `{GapId}`. A mutant that removes the creation log (`RecordGap.cs:25`) survived (see Faults) | FAIL |
| C51 | nav "Lacunas (2)" with 2 gaps, "Lacunas" with [] | `nav shows open gap count (0)`, `(1)` passed | `src/web/src/features/gaps/gaps.test.tsx:54` `within(nav).getByRole('link', { name: label })).toHaveAttribute('href', '/gaps')` with labels `:46-47` | PASS |
| C52 | gaps empty text | `gaps empty state` passed | `gaps.test.tsx:63` `findByText('Nenhuma lacuna. Suas IAs responderam tudo o que foi perguntado.')` | PASS |
| C53 | org-less gap blocked until an org is chosen; sends answer+organizationId; item leaves; owned gap sends without selector | `gaps answer requires organization when missing` passed | `gaps.test.tsx:74` `getByRole('button', { name: 'Responder' })).toBeDisabled()`; `:78-79` leaves list, `answers` equals `{ id: 'g2', ..., organizationId: 'o1' }`; `:82` selector absent for owned; `:87` `organizationId: null` | PASS |
| C54 | dismiss confirm text; cancel no call; confirm calls | `gaps dismiss confirms` passed | `gaps.test.tsx:100` `toContain('A pergunta sai da lista e não volta')`; `:101` `expect(api.dismissed).toEqual([])`; `:104` `toEqual(['g1'])` | PASS |
| C55 | "Carregando lacunas…" while loading; problem title on list and answer errors | `gaps loading and error states` passed. The list-error half is only in `gaps list error shows problem title` (passed in the full run), which the check's `-t` filter does not select | `gaps.test.tsx:121` `findByText('Carregando lacunas…')`; `:126` answer alert `toHaveTextContent('O provedor de IA falhou.')`; `:135` list alert `toHaveTextContent('Falha ao listar lacunas.')` | PASS |
| C56 | /organizations empty text + form; create navigates to /organizations/{id} | `organizations empty state and create` passed | `src/web/src/features/organizations/organizations.test.tsx:28` `findByText('Crie sua primeira organização')`; `:32` `getByTestId('location')).toHaveTextContent('/organizations/o1')` | PASS |
| C57 | org page lists docs and assistants; upload; assistant form with "Quando usar esta IA" sends routingDescription + organizationId | `organization page lists documents and assistants` passed | `organizations.test.tsx:128-133` file names, assistant links, file input, Enviar; `:139-141` `created` equals `{ organizationId: 'o1', ..., routingDescription: 'quando a pessoa quer um resumo' }` | PASS |
| C58 | delete org confirm mentions IAs/docs/lacunas; cancel no DELETE; confirm DELETE | `organization delete confirms` passed | `organizations.test.tsx:69` `toMatch(/IAs, os documentos e as lacunas/)`; `:70` `expect(deletes).toEqual([])`; `:73` `toEqual(['o1'])` | PASS |
| C59 | org page no assistants text; 404 title; "Carregando..." | `organization page empty, loading and not found` passed | `organizations.test.tsx:160` `getByText('Carregando...')`; `:162` `findByText('Nenhuma IA nesta organização')`; `:166` alert `toHaveTextContent('Organização não encontrada.')` | PASS |
| C60 | /assistants/{id} links to org, has ask, no upload, shows sources | `assistant page asks and links to organization` passed | `organizations.test.tsx:279` link `'← ACME'` href `/organizations/o1`; `:280` `queryByLabelText(/Arquivo/)).not.toBeInTheDocument()`; `:285-286` sources in answer | PASS |
| C61 | 14 routes without cookie -> 401, never 302 | `AuthTests.Protected_route_without_session_returns_401` x14 Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:129` `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` over the 14 rows `:101-114`. The client does not follow redirects (`ApiFactory.cs:68` `AllowAutoRedirect = false`), so a 302 would fail | PASS |
| C62 | rag-mvp ask/assistant tests pass with the assistant created inside an org | `AskTests` 13 Passed; `AssistantsTests.Foreign_or_missing_assistant_returns_404` x6 Passed | `AskTests.cs:31` `Assert.Equal([7, 6, 5, 4, 3], ...)` top-5; `:120-122` question bounds + errors.question; `:137-139` 502 problem without provider message; `:154` TooManyRequests on the 21st; `AssistantsTests.cs:134` NotFound for GET/DELETE/ask x {foreign, missing} | PASS |

**Result: 61/62 checks proven with located evidence. C50 is not proven.**

## Coverage

Recomputed from the code (route mapping in `Features/*/…Endpoints.cs`, `JevAsk.cs:43-46`, handler `Results.*` sites) and from the plan's Surface, Relations and Landing. The author's table was not read back as the answer.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/organizations` statuses (3) | `CreateOrganization.cs:20,29` + group `RequireAuthorization` `OrganizationsEndpoints.cs:9` | 201 C1 · 400 C2 · 401 C61 | - |
| `GET /api/organizations` (2) | `ListOrganizations.cs:19` | 200 C3 · 401 C61 | - |
| `GET /api/organizations/{id}` (3) | `GetOrganization.cs:30` + not-found | 200 C4 · 401 C61 · 404 C5 | - |
| `DELETE /api/organizations/{id}` (3) | `DeleteOrganization.cs:16` | 204 C12 · 401 C61 · 404 C5 | - |
| `POST /api/organizations/{id}/documents` (9) | `UploadDocument.cs:42-47,75,99-100,123,138` + 502 handler | 201 C13/C14 · 400 C14 (`DocumentsTests.cs:97`) · 401 C61 · 404 C5 · 409 C13 · 413 C14 · 415 C14 · 422 C14 · 502 C14 | - |
| `GET /api/organizations/{id}/documents` (3) | `ListDocuments.cs:23` | 200 C14 · 401 C61 · 404 C5 | - |
| `DELETE /api/organizations/{id}/documents/{documentId}` (3) | `DeleteDocument.cs:22` | 204 C14 · 401 C61 · 404 C5 | - |
| `POST /api/assistants` (4) | `CreateAssistant.cs:33,49` | 201 C6 · 400 C7 · 401 C61 · 404 C8 | - |
| `GET /api/assistants/{id}` (3) | `GetAssistant.cs:27` | 200 C9 · 401 C61 · 404 C62 | - |
| `POST /api/assistants/{id}/ask` (6) | `AskAssistant.cs:31` rate limited | 200 C35/C62 · 400 C62 · 401 C61 · 404 C62 · 429 C26/C62 · 502 C62 | - |
| `POST /api/jev/ask` (6) | `JevAsk.cs:43-46,71-73,96-97,108` | 200 C19 · 400 C25 · 401 C61 · 422 C24 · 429 C26 · 502 C28 | - |
| `GET /api/gaps` (2) | `ListGaps.cs:30` | 200 C40 · 401 C61 | - |
| `POST /api/gaps/{id}/answer` (6) | `AnswerGap.cs:31,38,40,45,50,68,76,81` | 200 C41 · 400 C44/C43 · 401 C61 · 404 C48/C43 · 409 C45 · 502 C46 | - |
| `POST /api/gaps/{id}/dismiss` (4) | `DismissGap.cs:16-18` | 204 C47 · 401 C61 · 404 C48 · 409 C45 | - |
| removed routes (4) | plan Surface "Saem" | all four C15 (`DocumentsTests.cs:247-250`) | - |
| Jev `kind` (3) | `JevAsk.cs:77,82,85,91,99` | answered C19 · clarify C21 · noMatch C22 | - |
| router failures (5) | `JevAsk.cs:122-126,128-131,148` + `AiServiceCollectionExtensions.cs:44-47` | throw, non-JSON, 0, N+1 C23 · no key C29 | - |
| eligibility (4) | `JevAsk.cs:65-66` + owner query filter | with description, null, whitespace-only, other user: all C18 | - |
| ask chat output (3) | `AskAssistant.cs:102-112` | found=true C35 · found=false C35 · non-JSON C39 | - |
| gap origin (3) | callers of `RecordGap.RecordAsync` (`AskAssistant.cs:94`, `JevAsk.cs:84`) | direct ask C35 · Jev routed C36 (via the same `AskAssistant.AnswerAsync`) · Jev noMatch C22 | - |
| gap grouping (4) | `RecordGap.cs:10,20` | normalized C37 · other org C37 · null org C37 · concurrent C38 | - |
| `Gap` transitions (5) | `AnswerGap.cs:39-40,72-74`, `DismissGap.cs:12-18` | open C35 · open->answered C41 · open->dismissed C47 · answered->x C45 · dismissed->x C45 | - |
| org `name` borders (5) | `CreateOrganization.cs` | "", "   ", 101 C2 · 1, 100 C1 | - |
| `routingDescription` borders (3) | `CreateAssistant.cs` | absent C6 · 500 C6 · 501 C7 | - |
| `organizationId` on assistant creation (3) | `CreateAssistant.cs` | absent C7 · foreign C8 · missing C8 | - |
| Jev `question` borders (4) | `AskAssistant.InvalidQuestion` via `JevAsk.cs:62` | "", "   ", 2001 C25 · 2000 C25 | - |
| gap `answer` borders (5) | `AnswerGap.cs:29-30` | "", "   ", 4001 C44 · 1, 4000 C41 | - |
| `organizationId` on gap answer (3) | `AnswerGap.cs:43-50` | absent, foreign, own: all C43 | - |
| list caps (2) | `JevAsk.cs:17-18` | alternatives <= 3 C20 · fallback <= 5 C23 | - |
| new `{id}` routes x {foreign, missing} (14) | route mapping | 10 org-route cases C5 (`OrganizationsTests.cs:114-118`) · 4 gap cases C48 (`GapsTests.cs:313-316`) | - |
| protected routes without session (14) | `RequireAuthorization` on 3 groups + `JevAsk.cs:44` | C61, 14 theory rows `AuthTests.cs:101-114`, 14 Passed | - |
| screens (5) and Observable states (13) | plan Observable | Organizações C56, C58 + loading (`organizations.test.tsx:77`) · Organização C57, C59 · IA C60 · Jev C30-C34 · Lacunas C51-C55 | - |
| doors (7) | plan Landing | 1 C10, C11, C16, C17 · 2 C5, C48, C18 · 3 C6, C17 · 4 C29, C18, C27 · 5 C19, C23 · 6 C35, C38 · 7 C41, C42 (except the door-7 literal FK, next row) | - |
| **Relations delete behaviours (8)**, a set the checks gave no row | plan Relations + `AppDbContext.cs:61-66`, migration `20260924131937_OrganizationsJevGaps.cs:115-127` | Org->Assistant cascade C12, C17 · Org->Document cascade C12, C17 · Document->Chunk cascade C12 · Org->Gap cascade C12 · Assistant->Gap set null C49 · User->Organization / User->Gap owner (C3, C40 scoping) | Gap->Document `ON DELETE SET NULL` (door 7 literal shape, `...OrganizationsJevGaps.cs:121-125`): no check, and `rg "document_id\|DocumentId"` over the tests finds no gap-related assertion. Nothing asserts that `gaps.document_id` is written on answer or nulled when the document is deleted |
| entities (5) | plan Impact | Organization C1, C12 · Assistant C6, C49 · Document C13, C41 · Chunk C12, C16 · Gap C35, C40 | - |
| startup config: keyed `router` client (2 assemblies) | read directly | `src/BuildYourOwnAI.Api/Program.cs:51` `builder.Services.AddAi(builder.Configuration);` registers `UnconfiguredAiClient` keyed `router` without a key (`AiServiceCollectionExtensions.cs:44-47`), exercised by C29 · test assembly `ApiFactory.cs:60` `services.AddKeyedSingleton<IChatClient>(AiServiceCollectionExtensions.RouterKey, Router)` exercised by C18 | - |

Observations that do not change a status set:

- `AnswerGap.cs:66-68` returns 409 when the answer document duplicates existing content in the organization, and `:75-76` returns 409 when a concurrent close wins. Both are extra causes of an already-proven status (409, via C45), and no AC names them. They are recorded here, not counted as unproven.
- The configured-key branch `AiServiceCollectionExtensions.cs:50-52` (real OpenRouter endpoint) has no proof, by design: tests do not call providers.

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| rag-mvp: endpoint that decides (validation, ownership, dedup, limits) | `Features/Organizations/*`, `Features/Assistants/*`, `Features/Documents/*`, `Features/Ask/AskAssistant.cs`, `Features/Jev/JevAsk.cs`, `Features/Gaps/*` (checks.md Evidence classifies JevAsk and the gap upsert here) | HTTP boundary against real Postgres: every accepted and rejected input, every error path | yes. Every route status, border and error path in Coverage has a boundary proof. The one relation left unproven is a schema/data-lifecycle member (see Coverage), not an HTTP one |
| rag-mvp: pure decision behind an endpoint (chunker) | `TextChunker` (untouched by this diff) | boundary + own level | yes. Not touched, and `TextChunkerTests` still pass (5 Passed). Note: `RecordGap.Normalize` is also a pure public function, but the approved Evidence classifies the gap upsert under the boundary row. Its rules are proven at the boundary by C37, and fault 2 killed it |
| rag-mvp: React component that decides what to show | `OrganizationsPage.tsx`, `OrganizationPage.tsx`, `AssistantPage.tsx`, `JevPage.tsx`, `GapsPage.tsx`, `AppLayout.tsx` | Testing Library + MSW: empty, loading, error, success | yes. Organizações: empty C56, loading `organizations.test.tsx:77`, error `:36-48`, success C58. Organização: C59 empty/loading/404, C57 success, upload error `:249`. IA: C60, error `:290`, pending `:305`. Jev: C30-C34. Lacunas: C51-C55 + list error `gaps.test.tsx:130` |
| rag-mvp: instrumentation (DI, route mapping, DTO) | `*Endpoints.cs`, `AiServiceCollectionExtensions.cs`, `Program.cs` | none of its own | yes, covered by the consumer proofs (C61 for mapping, C29/C18 for keyed DI) |
| jev-gaps: migration with backfill | `20260924131937_OrganizationsJevGaps.cs` | against real Postgres, from `InitialCreate` with data to the end | yes. `MigrationTests.cs:27` migrates to `InitialCreate`, seeds 2 owners and 3 assistants (one with no document, `:38`) with docs and chunks, then migrates to the end at `:49`. Final schema: C17 |

Swept rows resolving to **existing**: none. Every Swept row in checks.md cites a check id of this feature, so there was no pre-existing constraint to re-read.

## Faults injected

Isolation: `git worktree add <scratchpad>/wt HEAD`. For the web fault, the worktree's `src/web/node_modules` was a junction to the real tree's `node_modules` (read-only use). The junction was removed with `rmdir` before `git worktree remove --force`. Baseline `git status --porcelain` of the real tree was empty before and empty after, and the real `node_modules` was still intact. The worktree was reset with `git checkout -- .` between faults.

| Mutation | Location | Killed |
| --- | --- | --- |
| Jev router index bound `index <= count` -> `index <= count + 1` (N+1 accepted) | `src/BuildYourOwnAI.Api/Features/Jev/JevAsk.cs:148` | yes. `JevTests.Router_failure_falls_back_to_clarify(behaviour: "7")` Failed (`Assert.Equal() Failure`) |
| gap normalization drops `ToLowerInvariant()` | `src/BuildYourOwnAI.Api/Features/Gaps/RecordGap.cs:10` | yes. `GapsTests.Same_normalized_question_increments_count` Failed |
| organization retrieval filter `c.Document.OrganizationId == organizationId` -> `... \|\| true` | `src/BuildYourOwnAI.Api/Features/Ask/AskAssistant.cs:131` | yes. `AskTests.Retrieves_only_from_own_organization` Failed (`Collections differ`) |
| Lacunas answer button no longer disabled while an org-less gap has no organization chosen | `src/web/src/features/gaps/GapsPage.tsx:67` | yes. `gaps answer requires organization when missing` failed (`Received element is not disabled`) |
| gap-creation log removed (`logger.LogInformation("Gap {GapId} recorded ...")` -> `_ = ids.Single();`) | `src/BuildYourOwnAI.Api/Features/Gaps/RecordGap.cs:25` | no. Survived: `ObservabilityTests.Jev_and_gaps_never_log_content` Passed, because `CrossCuttingTests.cs:132` is satisfied by the `{GapId}` log in `AnswerGap.cs:79` |

Not injected, because of the five-fault cap: the gap-answer close in the same transaction (`AnswerGap.cs:72-78`). Its observable half is asserted by C41 `GapsTests.cs:194` and C46 `:293`.

## Gate

- `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` (PowerShell, DOCKER_HOST set): 149 passed, 0 failed
- `npm --prefix src/web run test -- --reporter=verbose`: 37 passed, 0 failed
- `dotnet test ... --filter "FullyQualifiedName~CrossCuttingTests.Jev_and_gaps_never_log_content"`: **0 tests matched, exit 0** (C50 proof is vacuous)

Ranked gaps:

1. C50: the proof filter matches no test and exits 0, so it gates nothing (`checks.md:176`; the class is `ObservabilityTests` in `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:77`). The "gap creation logs GapId" assertion at `CrossCuttingTests.cs:132` does not target creation: the mutant that removes `RecordGap.cs:25` survived.
2. Coverage: the Relations/door-7 member Gap->Document `ON DELETE SET NULL` (`20260924131937_OrganizationsJevGaps.cs:121-125`) has no check. Nothing asserts that `gaps.document_id` is written on answer or nulled when that document is deleted.
3. Minor, not failing: C55's list-error half is asserted only in `gaps.test.tsx:130-135` (`gaps list error shows problem title`), which the check's `-t "gaps loading and error states"` filter does not select. Add it to the Proof line.
