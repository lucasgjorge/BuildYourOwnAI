# RAG MVP verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 0579a65..875d5c6
**Round**: 3 - scoped
**Verifier**: independent sub-agent (author != verifier)

This round covers two things: the fix diff `9a9ca40..875d5c6` and every verdict from round 2 at `0747ee0` that was not PASS. The fix diff touches these files:

- `assistants.test.tsx`
- `AssistantsTests.cs`
- `checks.md`, which adds C50 and C51
- `README.md` and `.config/dotnet-tools.json`, which are not product code

Every proof ran in full at `875d5c6`. The API suite passed 81 of 81 and the web suite passed 22 of 22. All four ranked gaps from round 2 are closed:

1. **C47 now discriminates.** The test waits for the page heading, which renders only after the session guard resolves, before it asserts `Carregando...`. Fault W2 removed the list's own loader, and the test failed.
2. **C9 and C44 compare `createdAt` by value.** They check it against the POST response and the list, within 1 ms. Fault B1 made the list return a constant `createdAt`, and both tests failed.
3. **`RequireAuth` loading and non-401 error are proven by C50.** Fault W4 made the guard redirect on any error, and fault W6 removed the guard's loader. Both were killed.
4. **The create button's `Processando...` is proven by C51.** Fault W5 kept the button enabled while pending, and the test failed.

## Binding sources

Carried from bef8502. There is no binding source: the plan's `Sources` are the user's chat request and decisions, with no design or contract file. The profile is `standard`, so step 1 (`ui` only) does not run. The fix did not touch the interface. It changed only test files and specs, and no product source file.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| none (chat request / decisions only) | n/a | - | - |

## Checks

Proofs were re-run in full at 875d5c6 (verified at 875d5c6).

**API.** One full-project run:

```
$env:DOCKER_HOST="npipe://./pipe/dockerDesktopLinuxEngine"
dotnet test C:\Personal-Projects\BuildYourOwnAI\tests\BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"
```

- It exited 0 with `Total tests: 81, Passed: 81`.
- Each proof appears individually in the output as `Passed <FQN>`, with these counts per method:
  - `Protected_route_without_session_returns_401` x8
  - `Foreign_or_missing_assistant_returns_404` x12
  - `Create_valid_returns_201_with_location` x3
  - `Create_invalid_returns_400_keyed_by_field` x5
  - `TextChunkerTests` x5
  - `List_returns_only_own_newest_first` x1
  - `Get_returns_200_with_document_count` x1
  - every other method with its theory count

**Web.** One run:

```
npm --prefix C:/Personal-Projects/BuildYourOwnAI/src/web run test -- --reporter=verbose
```

- It exited 0 with `Tests 22 passed (22)`, and each test is listed with a check mark.
- These tests appear individually:
  - C47 `screen states added in verification round 2 > assistants list shows loading`
  - C50 `states added in verification round 3 > session guard states: loading while the session is unknown`
  - C50 `states added in verification round 3 > session guard states: a non-401 failure shows the problem title and stays`
  - C51 `states added in verification round 3 > create button shows processing`
- No filter matched zero tests.

**Citations.** Citations were refreshed for the two test files the fix touched:

- `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs`: the C9 hunk added 4 lines, so C10, C11 and C44 moved.
- `src/web/src/features/assistants/assistants.test.tsx`: the C47 hunk added 2 lines, so C48 and C49 moved, and C50 and C51 are appended.

Citations in the other files are carried from 0747ee0. Those files are not in `9a9ca40..875d5c6`, so their line numbers are unchanged.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | register 200, then login 200 | `AuthTests.Register_valid_returns_200_and_enables_login` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:22` - `Assert.Equal(HttpStatusCode.OK, register.StatusCode)`; `:25` - `Assert.Equal(HttpStatusCode.OK, login.StatusCode)` (carried from 0747ee0) | PASS |
| C2 | duplicate e-mail 400 problem+json with errors.DuplicateUserName | `AuthTests.Register_duplicate_email_returns_400_DuplicateUserName` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:38` - `Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode)`; `:40` - `GetProperty("errors").TryGetProperty("DuplicateUserName", out _)` (carried) | PASS |
| C3 | login 200 + cookie httponly/secure/samesite=strict | `AuthTests.Login_valid_sets_httponly_secure_strict_cookie` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:53` - `Assert.Equal(HttpStatusCode.OK, login.StatusCode)`; `:56-58` - `Assert.Contains("httponly"/"secure"/"samesite=strict", attributes)` (carried) | PASS |
| C4 | wrong password / unknown e-mail 401, no cookie | `AuthTests.Login_invalid_returns_401_without_cookie` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:76` - `Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode)`; `:77` - `Assert.DoesNotContain(SetCookies(login), c => c.StartsWith(SessionCookie))` (carried) | PASS |
| C5 | logout 204, cookie expired, next call 401 | `AuthTests.Logout_returns_204_and_ends_session` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:89` - `Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode)`; `:92` - `Assert.Equal(HttpStatusCode.Unauthorized, ...GetAsync("/api/assistants")...)` (carried) | PASS |
| C6 | 8 protected routes without cookie 401 | `AuthTests.Protected_route_without_session_returns_401` (8) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:123` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` (carried) | PASS |
| C7 | create 201, Location, trimmed name, instructions, createdAt | `AssistantsTests.Create_valid_returns_201_with_location` (3) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:22` - `Assert.Equal(HttpStatusCode.Created, response.StatusCode)`; `:25` - `Assert.Equal($"/api/assistants/{id}", response.Headers.Location?.OriginalString)`; `:26` - `Assert.Equal(name, ...name)`; `:28` - `createdAt > DateTimeOffset.UtcNow.AddMinutes(-5)` (verified at 875d5c6, above the fix hunk) | PASS |
| C8 | invalid name/instructions -> 400 keyed by field | `AssistantsTests.Create_invalid_returns_400_keyed_by_field` (5) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:46` - `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)`; `:47` - `GetProperty("errors").TryGetProperty(field, out _)` (verified at 875d5c6) | PASS |
| C9 | list [] for new user, then [B, A] by createdAt desc with documentCount, only own | `AssistantsTests.List_returns_only_own_newest_first` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs`, verified at 875d5c6:<br>- `:60` - `Assert.Equal(0, ...GetArrayLength())`<br>- `:70` - `Assert.Equal(2, list.GetArrayLength())`<br>- `:71-72` - `Assert.Equal(b, list[0]...id)`, `Assert.Equal(a, list[1]...id)`<br>- `:73-74` - documentCount 0 and 1<br>- `:75-78` - name and instructions values<br>- new `:80-81` - `AssertSameInstant(createdB...createdAt, list[0]...createdAt)` and `AssertSameInstant(createdA...createdAt, list[1]...createdAt)`<br>`:170-171` defines `AssertSameInstant` as `Assert.InRange((actual - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1))`. Fault B1 was killed. | PASS |
| C10 | 6 `{id}` routes x {foreign, missing} 404, foreign docs intact | `AssistantsTests.Foreign_or_missing_assistant_returns_404` (12) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:125` - `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)`; `:126` - `Assert.Equal(1, await DocumentCountAsync(foreignId))` (refreshed, +4) | PASS |
| C11 | owner delete 204, cascade | `AssistantsTests.Delete_returns_204_and_cascades` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:143` - `Assert.Equal(HttpStatusCode.NoContent, response.StatusCode)`; `:145` - `Assert.Equal(0, await DocumentCountAsync(id))`; `:146` - `Assert.Equal(0, await ScalarAsync(chunksOfDocs, ...))` (refreshed, +4) | PASS |
| C12 | .txt/.md/.pdf 201, chunks with 1536 dims | `DocumentsTests.Upload_supported_file_returns_201_and_persists_chunks` (3) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:32` - `Created`; `:36` - `Assert.True(chunkCount > 0)`; `:40` - `Assert.Equal(chunkCount, ...count(*) from chunks...)`; `:41-42` - `vector_dims(embedding) = 1536` (carried) | PASS |
| C13 | .exe/.docx/no-ext 415; NOTAS.TXT 201 | `DocumentsTests.Upload_extension_is_checked_case_insensitively` (4) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:58` - `Assert.Equal(expected, response.StatusCode)` over cases `:47-50` (carried) | PASS |
| C14 | 10 485 761 -> 413; 10 485 760 -> 201 | `DocumentsTests.Upload_size_limit_is_10485760_bytes` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:63-64` - `[InlineData(MaxBytes + 1, RequestEntityTooLarge)]`, `[InlineData(MaxBytes, Created)]`; `:76` - `Assert.Equal(expected, response.StatusCode)` (carried) | PASS |
| C15 | missing file / 0 bytes / non-multipart 400 | `DocumentsTests.Upload_without_file_returns_400` (3) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:97` - `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` (carried) | PASS |
| C16 | no text -> 422, 0 docs | `DocumentsTests.Upload_without_text_returns_422` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:113` - `UnprocessableEntity`; `:114` - `Assert.Equal(0, await DocumentCountAsync(id))` (carried) | PASS |
| C17 | same content 201 then 409; other assistant 201 | `DocumentsTests.Upload_duplicate_content_returns_409` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:127` - `Assert.Equal(HttpStatusCode.Conflict, ...)`; `:128` - `Assert.Equal(1, await DocumentCountAsync(id))`; `:129` - other assistant `Created` (carried) | PASS |
| C18 | embedding failure 502, nothing persisted | `DocumentsTests.Upload_embedding_failure_returns_502_and_persists_nothing` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:142` - `BadGateway`; `:143` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)`; `:144-145` - counts 0 (carried) | PASS |
| C19 | documents list newest first with 5 fields | `DocumentsTests.List_returns_newest_first` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:162-163` - `Assert.Equal(second, list[0]...)`, `Assert.Equal(first, list[1]...)`; `:164-165` - `TryGetProperty(field)` (carried) | PASS |
| C20 | document delete 204, removed from retrieval | `DocumentsTests.Delete_returns_204_and_removes_from_retrieval` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:180` - `NoContent`; `:181` - chunks count 0; `:183` - `Assert.DoesNotContain(...sources..., s => ...documentId == removed)` (carried) | PASS |
| C21 | sibling assistant's doc delete 404 | `DocumentsTests.Delete_document_of_other_assistant_returns_404` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:197` - `NotFound`; `:198` - `Assert.Equal(1, await DocumentCountAsync(b))` (carried) | PASS |
| C22 | top-5 by cosine in order; 2 chunks -> 2 | `AskTests.Ask_returns_answer_with_top5_sources` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:31` - `Assert.Equal([7, 6, 5, 4, 3], sources.Select(...))`; `:44` - `Assert.Equal(2, smallSources.GetArrayLength())` (carried) | PASS |
| C23 | ask to A never retrieves/prompts B | `AskTests.Ask_never_retrieves_from_another_assistant` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:66-67` - `Assert.All(sources, s => Assert.Contains(documentId, docsOfA))`; `:68` - `Assert.DoesNotContain(secretOfB, ...PromptContaining(marker))` (carried) | PASS |
| C24 | prompt contains instructions, chunks, question | `AskTests.Ask_prompt_contains_instructions_chunks_and_question` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:86` - `Assert.Contains(instructions, prompt)`; `:87` - question; `:88` - each chunk text (carried) | PASS |
| C25 | no documents -> 200, sources [] | `AskTests.Ask_without_documents_returns_empty_sources` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:100` - `OK`; `:102-103` - array, length 0 (carried) | PASS |
| C26 | question bounds | `AskTests.Ask_question_bounds` (4) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:120` - `Assert.Equal(expected, response.StatusCode)`; `:122` - `TryGetProperty("question", out _)` (carried) | PASS |
| C27 | provider failures 502 problem, no provider message | `AskTests.Ask_provider_failure_returns_502` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:137` - `BadGateway`; `:139` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)` (carried) | PASS |
| C28 | 20 ok, 21st 429, other user 200 | `AskTests.Ask_rate_limit_is_20_per_minute_per_user` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:152` - OK x20; `:154` - `TooManyRequests`; `:155` - other user OK (carried) | PASS |
| C29 | 401 session -> /login; /register opens | `redirects to login without session` (3 tests) passed | `src/web/src/features/auth/auth.test.tsx:14` - `waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login'))`; `:24-25` - heading 'Criar conta', location '/register' (carried) | PASS |
| C30 | empty list text + "Criar IA" | `shows empty state` passed | `src/web/src/features/assistants/assistants.test.tsx:14` - `findByText('Você ainda não criou nenhuma IA')`; `:15` - `getByRole('button', { name: 'Criar IA' })` (above the fix hunk, unchanged) | PASS |
| C31 | documents, upload field, question box | `assistant page shows documents upload and question` passed | `src/web/src/features/assistants/assistants.test.tsx:67-68` - fileNames; `:69` - `toHaveAttribute('type', 'file')`; `:71` - `getByLabelText('Pergunta')` (unchanged) | PASS |
| C32 | answer and source fileNames | `shows answer and source file names` passed | `src/web/src/features/assistants/assistants.test.tsx:93-94` - `within(answer).getByText('politicas.pdf'/'contrato.md')` (unchanged) | PASS |
| C33 | problem title shown and input kept | `shows problem title and keeps input` (2) passed | `src/web/src/features/assistants/assistants.test.tsx:30-31` - `toHaveTextContent('Nome inválido')`, `toHaveValue('Minha IA')`; `:108-109` - ask (unchanged) | PASS |
| C34 | delete confirms; cancel no DELETE | `delete asks for confirmation` (2) passed | `src/web/src/features/assistants/assistants.test.tsx:52` - `expect(deletes).toEqual([])`; `:56` - `toEqual(['a1'])`; `:129`, `:132` - `[]`, `['d1']` (unchanged) | PASS |
| C35 | upload/ask button disabled "Processando..." | `disables button while processing` (2) passed | `src/web/src/features/assistants/assistants.test.tsx:150-151` - `findByRole('button', { name: 'Processando...' })`, `toBeDisabled()`; `:171-172` (unchanged) | PASS |
| C36 | vector(1536) + hnsw cosine index | `SchemaTests.Chunk_embedding_is_vector1536_with_hnsw_cosine_index` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:31` - `Assert.Equal("vector(1536)", type)`; `:33` - hnsw + `vector_cosine_ops` (carried) | PASS |
| C37 | concurrent duplicates -> 201 + 409, 1 doc | `DocumentsTests.Concurrent_duplicate_uploads_yield_one_201_one_409` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:216` - `Assert.Equal([Created, Conflict], statuses)`; `:217` - 1 doc (carried) | PASS |
| C38 | Features never reference OpenAI | `ArchitectureTests.Features_do_not_reference_OpenAI` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:143` - `Assert.NotEmpty(files)`; `:147` - `Assert.Empty(offenders)` (carried) | PASS |
| C39 | 8 error statuses are problem+json | `ProblemDetailsTests.Error_responses_are_problem_json` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:70` - `Assert.Equal(status, (int)response.StatusCode)`; `:71` - `application/problem+json`; `:72` - body status (carried) | PASS |
| C40 | one Information log with ids/counts; no content in logs | `ObservabilityTests.Ingestion_logs_ids_and_counts_never_content` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:95` - `Assert.Single(ingestion)`; `:96` - `LogLevel.Information`; `:97-98` - ChunkCount/ElapsedMs keys; `:101-106` - `Assert.DoesNotContain(logged, text => text.Contains(documentSecret/questionSecret/answer))` over messages and property values (carried) | PASS |
| C41 | SPA fallback vs /api 404 problem | `SpaHostingTests.Spa_fallback_serves_index_but_not_for_api` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:121-123` - `OK`, `text/html`, SpaMarker; `:126-128` - `NotFound`, `application/problem+json`, no SpaMarker (carried) | PASS |
| C42 | chunker rules | `TextChunkerTests` (5) Passed | `tests/BuildYourOwnAI.Api.Tests/Common/TextChunkerTests.cs:11` - `Assert.Empty(...)`; `:18` - `Assert.Single(...)`; `:26` - whitespace; `:39` - `Assert.Equal(chunks[i - 1][^200..], chunks[i][..200])` (carried) | PASS |
| C43 | manage/info 200 with email with session, 401 without | `AuthTests.Manage_info_reflects_session` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:131` - `Unauthorized`; `:140` - `OK`; `:142` - `Assert.Equal(email, body.GetProperty("email").GetString())`; `:143` - `Assert.False(...isEmailConfirmed...)` (carried) | PASS |
| C44 | GET id 200 with id, name, instructions, createdAt, documentCount | `AssistantsTests.Get_returns_200_with_document_count` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs`, refreshed and verified at 875d5c6:<br>- `:160` - `Assert.Equal(HttpStatusCode.OK, response.StatusCode)`<br>- `:162-164` - id, `"Suporte"`, `"Responda em portugues"`<br>- new `:165-166` - `AssertSameInstant(listed.GetProperty("createdAt")..., body.GetProperty("createdAt")...)`<br>- `:167` - `Assert.Equal(2, body.GetProperty("documentCount").GetInt32())`<br>C44 also failed under fault B1. | PASS |
| C45 | /login: pending "Processando..." disabled; 401 -> "E-mail ou senha inválidos." keeps e-mail; 200 -> /assistants | `login form states: ...` (2) passed | `src/web/src/features/auth/auth.test.tsx:60` - `expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()`; `:62` - `toHaveTextContent('E-mail ou senha inválidos.')`; `:63` - `toHaveValue('ana@test.local')`; `:77-78` - heading 'Minhas IAs', location '/assistants' (carried from 0747ee0) | PASS |
| C46 | /register: pending disabled; problem title keeps e-mail; success -> /assistants | `register form states: ...` (2) passed | `src/web/src/features/auth/auth.test.tsx:94` - `findByRole('button', { name: 'Processando...' })).toBeDisabled()`; `:96` - `toHaveTextContent('E-mail já cadastrado')`; `:97` - `toHaveValue('ana@test.local')`; `:115-116` - heading, location '/assistants' (carried) | PASS |
| C47 | /assistants shows "Carregando..." while GET /api/assistants is pending, then the list | `assistants list shows loading` passed | `src/web/src/features/assistants/assistants.test.tsx`, verified at 875d5c6:<br>- `:190` - `expect(await screen.findByRole('heading', { name: 'Minhas IAs' })).toBeInTheDocument()`. The guard has resolved at this point, because `RequireAuth` renders the page only after the session is known.<br>- `:191` - `expect(screen.getByText('Carregando...')).toBeInTheDocument()`<br>- `:193` - `findByRole('link', { name: 'Suporte' })`<br>- `:194` - `queryByText('Carregando...')).not.toBeInTheDocument()`<br>Fault W2, which removed only the list loader at `AssistantsPage.tsx:53`, was killed. The failure read `Unable to find an element with the text: Carregando....` | PASS |
| C48 | /assistants/{id} with documents [] shows no document item and shows upload | `empty document list shows only upload` passed | `src/web/src/features/assistants/assistants.test.tsx:209` - `waitFor(() => expect(within(section).queryByText('Carregando...')).not.toBeInTheDocument())`; `:210` - `expect(within(section).queryAllByRole('listitem')).toHaveLength(0)`; `:212` - `getByRole('button', { name: 'Enviar' })` (refreshed, +2) | PASS |
| C49 | upload problem details -> title shown on assistant page | `upload error shows problem title` passed | `src/web/src/features/assistants/assistants.test.tsx:226` - `expect(await screen.findByRole('alert')).toHaveTextContent('Formato não suportado. Use .pdf, .txt, .md.')` (refreshed, +2) | PASS |
| C50 | manage/info pending -> "Carregando..." on protected route; 500 problem -> title in alert, no redirect to /login | `session guard states: loading while the session is unknown` passed; `session guard states: a non-401 failure shows the problem title and stays` passed | `src/web/src/features/assistants/assistants.test.tsx`, verified at 875d5c6:<br>- `:244` - `expect(await screen.findByText('Carregando...')).toBeInTheDocument()`<br>- `:245` - `expect(screen.queryByRole('heading', { name: 'Minhas IAs' })).not.toBeInTheDocument()`<br>- `:247` - heading appears after `release()`<br>- `:256` - `expect(await screen.findByRole('alert')).toHaveTextContent('Servidor indisponível')`<br>- `:257` - `expect(screen.getByTestId('location')).toHaveTextContent('/assistants')`<br>Faults W4 and W6 were killed. | PASS |
| C51 | /assistants create button disabled with "Processando..." while POST pending | `create button shows processing` passed | `src/web/src/features/assistants/assistants.test.tsx:276` - `expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()`; `:278` - `expect(await screen.findByRole('button', { name: 'Criar IA' })).toBeEnabled()` (verified at 875d5c6; fault W5 killed) | PASS |

**Level**: C1-C49 are carried from 0747ee0. C50 and C51 are Testing Library + MSW tests, which is the level the `Test policy` row requires for React components. There are no level gaps.

**Precision notes (non-failing)**:

- **C44's `createdAt` is compared with the list's value, not with the creation value.** A fault that changed both the list and GET-by-id `createdAt` identically would evade C44 alone. C9 would still catch it, because it compares the list against the POST response, and C7 bounds the POST value near `UtcNow`. The chain C7 -> C9 -> C44 therefore pins the value. The GET-by-id side alone (`GetAssistant.cs:17`) was not injected in this round, because the fault cap is 5. The assertion at `:166` is a value comparison within 1 ms, so a constant would fail it. (Verified at 875d5c6.)
- **C40's test logger ignores scopes.** `CapturingLoggerProvider.BeginScope` returns `null`, so data carried only in a logging scope is outside what C40 can see. Carried from 0747ee0.
- **Two `AssistantsPage` error branches have no test of their own.** The list-GET error alert (`AssistantsPage.tsx:54`) and the delete error alert (`:52`) are untested. The component's error state is proven through the create error (C33) and the shared `errorTitle` rendering. The `Test policy` expectation is per state, not per branch, so this is not a gap. Carried in substance from 0747ee0.

**Swept existing**: carried from bef8502. No `Swept` row resolves to an existing constraint.

**Startup configuration**: carried from bef8502. The fix did not touch `Program.cs`, `ApiFactory.cs` or any product source.

## Coverage

The rows whose authority the fix touched were recomputed at 875d5c6:

- `/assistants` screen states
- the `RequireAuth` guard
- the React component states for `RequireAuth` and `AssistantsPage`
- the `createdAt` fields of `GET /api/assistants` and `GET /api/assistants/{id}`

All other rows are carried from 0747ee0 (and bef8502).

The authority for screen states is the plan's `Observable` table (`plan.md:232-238`) plus the `Test policy` "cada estado" expectation. That gives 12 members, including `/assistants/{id}` unauthorised, which C29 proves.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| Observable screen states (12) - verified at 875d5c6 | `plan.md:232-238` | `/login` error C45 · `/login` loading C45 · `/register` error C46 · `/register` loading C46 · `/assistants` empty C30 · `/assistants` loading C47 (list loader, W2 killed), C51 (create button, W5 killed) · `/assistants` error C33 · `/assistants` unauthorised C29 · `/assistants/{id}` empty C48 · `/assistants/{id}` error C33, C49 · `/assistants/{id}` loading C35 · `/assistants/{id}` unauthorised C29 | - |
| `RequireAuth` guard branches (4) - verified at 875d5c6 | `src/web/src/features/auth/RequireAuth.tsx:9-13` | pending `:9` C50 `:244-245` (W6 killed) · 401 -> Navigate `:10` C29 `auth.test.tsx:14` · non-401 error alert `:11` C50 `:256-257` (W4 killed) · success Outlet `:13` C50 `:247`, C30 | - |
| React component states per Test policy - verified at 875d5c6 | components that branch on query or mutation state: `AuthForm.tsx`, `AssistantsPage.tsx`, `AssistantPage.tsx`, `RequireAuth.tsx` | `AuthForm` loading, error and success C45/C46 · `AssistantPage` empty C48, loading C35, error C33/C49, success C31/C32 · `AssistantsPage` empty C30, loading C47 + C51, error C33, success C47 `:193`/C34 · `RequireAuth` loading C50, error C50, unauthorised C29, success C50 `:247` | - |
| UI error display (5) - carried from 0747ee0 | checks row + `role="alert"` sites | create C33 · ask C33 · upload C49 · login C45 · register C46 (plus guard error C50, new) | - |
| `GET /api/assistants` item fields (5) - verified at 875d5c6 | plan Surface `Out` + `ListAssistants.cs:8` `Item(Id, Name, Instructions, CreatedAt, DocumentCount)` | id C9 `:71-72` · name C9 `:75,77` · instructions C9 `:76,78` · documentCount C9 `:73-74` · createdAt C9 `:80-81`, by value against the POST response (B1 killed) | - |
| `GET /api/assistants/{id}` fields (5) - verified at 875d5c6 | `GetAssistant.cs:9` `Response(Id, Name, Instructions, CreatedAt, DocumentCount)` | id `:162` · name `:163` · instructions `:164` · createdAt `:166`, by value against the list · documentCount `:167` (all C44) | - |
| `GET /api/auth/manage/info` fields (2) - carried from 0747ee0 | Surface `Out` | email C43 `:142` · isEmailConfirmed C43 `:143` | - |
| log content surfaces (C40) - carried from 0747ee0 | `CapturedLog`: Message, Properties | message C40 · property values C40 | - |
| all other rows - carried from bef8502 | as in round 1 | route statuses (12 routes), protected routes (8), `{id}` x {foreign, missing} (12), problem+json (8), extensions, size bounds, missing input, no text, name, instructions and question bounds, top-K, provider failures, rate limit, chunker rules, destructive UI (2), in-progress UI (2), doors (9), entities (3), startup config. None of these rows has an unproven member. | - |

## Test policy rows

The React component row was unmet in round 2, so it was re-judged at 875d5c6. The other rows are carried from 0747ee0: the fix touched no product file, and its backend change only strengthens C9 and C44 assertions.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Endpoint that decides (validation, ownership, dedup, limits) | `Features/**/*.cs` | HTTP boundary vs real Postgres | yes. Carried; the C9 and C44 assertions were strengthened at 875d5c6. |
| Pure decision behind an endpoint (chunker) | `Common/TextChunker.cs` | boundary C12 · own level C42 | yes. Carried from bef8502. |
| React component that decides what to show | `AuthForm.tsx` (+ `LoginPage.tsx`, `RegisterPage.tsx`, `auth/api.ts`), `AssistantsPage.tsx`, `AssistantPage.tsx`, `RequireAuth.tsx` | Testing Library + MSW | yes. Verified at 875d5c6: every component has empty (where applicable), loading, error and success proven. `AssistantsPage` loading is now discriminating: C47 was killed by W2 and C51 by W5. `RequireAuth` loading and non-401 error are covered by C50, which was killed by W6 and W4. |
| Instrumentation (DI, route mapping, DTO) | `AssistantsEndpoints.cs`, `AuthEndpoints.cs`, AI registration, records | covered by consumer's proof | yes. Verified at 875d5c6 for the list DTO: its `CreatedAt` mapping is now discriminated by C9 and C44 (B1 killed). |

## Faults injected

Verified at 875d5c6. The worktree was `git worktree add <scratchpad>\wt3 HEAD` at `875d5c6`.

- Each fault was applied alone and reverted with `git checkout -- .` inside the worktree. The worktree's `git status --porcelain` was empty between faults.
- Web runs used a directory junction to the real `src/web/node_modules`. The junction was unlinked with `(Get-Item <junction>).Delete()`, and the target still exists (`Test-Path ...\node_modules\vitest` is `True`). The worktree was then removed with `git worktree remove --force`, and `git worktree list` shows only the main tree.
- The real tree's `git status --porcelain` was empty before the faults and empty after, so it matches the baseline.
- The round-1 faults F1-F5 and the round-2 faults W1, W3 and B2 are carried as killed. The fix touched none of their surfaces.

| Mutation | Location | Killed |
| --- | --- | --- |
| W2 (re-run): list loader removed. `{assistants.isPending && <p>Carregando...</p>}` becomes a comment, and the guard's loader stays. | `src/web/src/features/assistants/AssistantsPage.tsx:53` | yes. C47 `assistants list shows loading` failed with `Unable to find an element with the text: Carregando....` |
| B1 (re-run): list DTO `createdAt` made constant. `new Item(a.Id, a.Name, a.Instructions, a.CreatedAt, ...)` becomes `default(DateTimeOffset)`. | `src/BuildYourOwnAI.Api/Features/Assistants/ListAssistants.cs:16` | yes. C9 `List_returns_only_own_newest_first` and C44 `Get_returns_200_with_document_count` both failed with `Assert.InRange() Failure: Value not in range`. |
| W4: guard redirects on any error. `if (session.error instanceof ApiError && session.error.status === 401) return <Navigate ...>` becomes `if (session.isError) return <Navigate to="/login" replace />`. | `src/web/src/features/auth/RequireAuth.tsx:10` | yes. C50 `session guard states: a non-401 failure shows the problem title and stays` failed with `Unable to find role="alert"`. |
| W5: create button not disabled while pending. `disabled={create.isPending}` becomes `disabled={false}`. | `src/web/src/features/assistants/AssistantsPage.tsx:47` | yes. C51 `create button shows processing` failed with `Received element is not disabled`. |
| W6: guard loader removed. `if (session.isPending) return <p className="p-6">Carregando...</p>` becomes a comment, so the guard falls through to `Outlet` while pending. | `src/web/src/features/auth/RequireAuth.tsx:9` | yes. C50 `session guard states: loading while the session is unknown` failed, and so did the error-state test. |

## Gate

- `dotnet test C:\Personal-Projects\BuildYourOwnAI\tests\BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` (DOCKER_HOST npipe) at 875d5c6: 81 passed, 0 failed, exit 0.
- `npm --prefix C:/Personal-Projects/BuildYourOwnAI/src/web run test -- --reporter=verbose` at 875d5c6: 22 passed, 0 failed, exit 0.

**Ranked gaps:** none. The precision notes above do not fail the feature.

Lessons (step 7) were not written, because the brief allows only `verification.md` to be modified. The round-2 lessons still apply for the orchestrator to distill:

- A loading-text assertion must be scoped away from a shared loader.
- An ordering-only comparison does not prove a field's value.
