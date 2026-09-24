# RAG MVP verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 0579a65..0747ee0
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Round 2 scope: the fix diff `e579476..0747ee0` plus every verdict that was not PASS at `bef8502`. At `0747ee0` all 49 proofs ran and passed: API 81/81, web 19/19. The fix closes most of round 1's gaps:

- login and register error, loading and success are now proven (C45, C46)
- the empty document list is proven (C48)
- the upload error is proven (C49)
- `isEmailConfirmed` is proven (C43)
- C40 now checks structured property values

The verdict is still FAIL, for three reasons:

1. **C47 is hollow.** The test that proves the `/assistants` loading state still passed after the list's `Carregando...` was deleted. The loading text it finds comes from `RequireAuth`'s session loader, not from the list.
2. **The `createdAt` assertion added to C9 does not catch a wrong value.** A list DTO that returns a constant `createdAt` survives, because the test only checks `>=` between two items.
3. **The React-component `Test policy` row is still unmet.** `RequireAuth`'s loading and non-401 error states, which round 1 named, are still untested, and so is the create button's `Processando...` on `/assistants`.

## Binding sources

Carried from bef8502. There is no binding source: the plan's `Sources` are the user's chat request and decisions, with no design or contract file. The profile is `standard`, so step 1 (`ui` only) does not run. The fix did not touch the interface.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| none (chat request / decisions only) | n/a | - | - |

## Checks

Proofs were re-run in full at `0747ee0` (verified at 0747ee0):

- **API**: one full-project run: `$env:DOCKER_HOST="npipe://./pipe/dockerDesktopLinuxEngine"; dotnet test C:\Personal-Projects\BuildYourOwnAI\tests\BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`. It exited 0 with `Total tests: 81, Passed: 81`. Every proof name was counted individually in the output as `Passed <FQN>`, including all theory cases (for example `Protected_route_without_session_returns_401` x8, `Foreign_or_missing_assistant_returns_404` x12 and `TextChunkerTests` x5). The `Failed executing DbCommand` lines in the log are EF logs from the expected unique-violation path in C37, not test failures.
- **Web**: one run: `npm --prefix C:/Personal-Projects/BuildYourOwnAI/src/web run test -- --reporter=verbose`. It exited 0 with `Tests 19 passed (19)`, and each test is listed with a check mark. The new names all appear:
  - `login form states: disabled while pending, then invalid credentials keep the e-mail`
  - `login form states: success goes to /assistants`
  - `register form states: disabled while pending, then problem title keeps the e-mail`
  - `register form states: success goes to /assistants`
  - `assistants list shows loading`
  - `empty document list shows only upload`
  - `upload error shows problem title`

  There were no zero-match filters.

Evidence citations are refreshed for the files the fix touched: `AssistantsTests.cs`, `AuthTests.cs`, `CrossCuttingTests.cs`, `auth.test.tsx` and `assistants.test.tsx`. Citations in untouched files are carried from bef8502, and their line numbers are unchanged because those files are not in the fix diff.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | register 200, then login 200 | `AuthTests.Register_valid_returns_200_and_enables_login` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:22` - `Assert.Equal(HttpStatusCode.OK, register.StatusCode)`; `:25` - `Assert.Equal(HttpStatusCode.OK, login.StatusCode)` (carried from bef8502, lines unchanged) | PASS |
| C2 | duplicate e-mail 400 problem+json with errors.DuplicateUserName | `AuthTests.Register_duplicate_email_returns_400_DuplicateUserName` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:38` - `Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode)`; `:40` - `GetProperty("errors").TryGetProperty("DuplicateUserName", out _)` | PASS |
| C3 | login 200 + cookie httponly/secure/samesite=strict | `AuthTests.Login_valid_sets_httponly_secure_strict_cookie` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:53` - `Assert.Equal(HttpStatusCode.OK, login.StatusCode)`; `:56-58` - `Assert.Contains("httponly"/"secure"/"samesite=strict", attributes)` | PASS |
| C4 | wrong password / unknown e-mail 401, no cookie | `AuthTests.Login_invalid_returns_401_without_cookie` (2 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:76` - `Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode)`; `:77` - `Assert.DoesNotContain(SetCookies(login), c => c.StartsWith(SessionCookie))` | PASS |
| C5 | logout 204, cookie expired, next call 401 | `AuthTests.Logout_returns_204_and_ends_session` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:89` - `Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode)`; `:92` - `Assert.Equal(HttpStatusCode.Unauthorized, ...GetAsync("/api/assistants")...)` | PASS |
| C6 | 8 protected routes without cookie 401 | `AuthTests.Protected_route_without_session_returns_401` (8 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:123` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C7 | create 201, Location, trimmed name, instructions | `AssistantsTests.Create_valid_returns_201_with_location` (3 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:22` - `Assert.Equal(HttpStatusCode.Created, ...)`; `:25` - `Assert.Equal($"/api/assistants/{id}", response.Headers.Location?.OriginalString)` (lines above the fix hunk, unchanged) | PASS |
| C8 | invalid name/instructions -> 400 keyed by field | `AssistantsTests.Create_invalid_returns_400_keyed_by_field` (5 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:46` - `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:47` - `GetProperty("errors").TryGetProperty(field, out _)` | PASS |
| C9 | list [] for new user, then [B, A] with documentCount, only own | `AssistantsTests.List_returns_only_own_newest_first` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:60` - `Assert.Equal(0, ...GetArrayLength())`; `:68` - `Assert.Equal(2, list.GetArrayLength())`; `:69-70` - `Assert.Equal(b, list[0]...id)`, `Assert.Equal(a, list[1]...id)`; `:71-72` - documentCount 0/1; new `:73-76` - `Assert.Equal("B", ...name)`, `JsonValueKind.Null` instructions, `Assert.Equal("A", ...)`, `Assert.Equal("instrucoes de A", ...)`; `:77` - `Assert.True(list[0].createdAt >= list[1].createdAt)` (verified at 0747ee0). The claim itself is met. The weak `createdAt` assertion is a Coverage and fault finding below. | PASS |
| C10 | 6 `{id}` routes x {foreign, missing} 404 | `AssistantsTests.Foreign_or_missing_assistant_returns_404` (12 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:121` - `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)`; `:122` - `Assert.Equal(1, await DocumentCountAsync(foreignId))` (refreshed, +5) | PASS |
| C11 | owner delete 204, cascade | `AssistantsTests.Delete_returns_204_and_cascades` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:139` - `Assert.Equal(HttpStatusCode.NoContent, ...)`; `:141` - `Assert.Equal(0, await DocumentCountAsync(id))`; `:142` - `Assert.Equal(0, await ScalarAsync(chunksOfDocs, ...))` (refreshed) | PASS |
| C12 | .txt/.md/.pdf 201, chunks with 1536 dims | `DocumentsTests.Upload_supported_file_returns_201_and_persists_chunks` (3) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:32` - `Created`; `:36` - `Assert.True(chunkCount > 0)`; `:40` - `Assert.Equal(chunkCount, ...count(*) from chunks...)`; `:41-42` - `vector_dims(embedding) = 1536` | PASS |
| C13 | .exe/.docx/no-ext 415; NOTAS.TXT 201 | `DocumentsTests.Upload_extension_is_checked_case_insensitively` (4) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:58` - `Assert.Equal(expected, response.StatusCode)` over cases `:47-50` | PASS |
| C14 | 10 485 761 -> 413; 10 485 760 -> 201 | `DocumentsTests.Upload_size_limit_is_10485760_bytes` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:63-64` - `[InlineData(MaxBytes + 1, RequestEntityTooLarge)]`, `[InlineData(MaxBytes, Created)]`; `:76` - `Assert.Equal(expected, response.StatusCode)` | PASS |
| C15 | missing file / 0 bytes / non-multipart 400 | `DocumentsTests.Upload_without_file_returns_400` (3) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:97` - `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` | PASS |
| C16 | no text -> 422, 0 docs | `DocumentsTests.Upload_without_text_returns_422` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:113` - `UnprocessableEntity`; `:114` - `Assert.Equal(0, await DocumentCountAsync(id))` | PASS |
| C17 | same content 201 then 409; other assistant 201 | `DocumentsTests.Upload_duplicate_content_returns_409` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:127` - `Assert.Equal(HttpStatusCode.Conflict, ...)`; `:128` - `Assert.Equal(1, await DocumentCountAsync(id))`; `:129` - other assistant `Created` | PASS |
| C18 | embedding failure 502, nothing persisted | `DocumentsTests.Upload_embedding_failure_returns_502_and_persists_nothing` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:142` - `BadGateway`; `:143` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)`; `:144-145` - counts 0 | PASS |
| C19 | documents list newest first with 5 fields | `DocumentsTests.List_returns_newest_first` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:162-163` - `Assert.Equal(second, list[0]...)`, `Assert.Equal(first, list[1]...)`; `:164-165` - `TryGetProperty(field)` | PASS |
| C20 | document delete 204, removed from retrieval | `DocumentsTests.Delete_returns_204_and_removes_from_retrieval` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:180` - `NoContent`; `:181` - chunks count 0; `:183` - `Assert.DoesNotContain(...sources..., s => ...documentId == removed)` | PASS |
| C21 | sibling assistant's doc delete 404 | `DocumentsTests.Delete_document_of_other_assistant_returns_404` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:197` - `NotFound`; `:198` - `Assert.Equal(1, await DocumentCountAsync(b))` | PASS |
| C22 | top-5 by cosine in order; 2 chunks -> 2 | `AskTests.Ask_returns_answer_with_top5_sources` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:31` - `Assert.Equal([7, 6, 5, 4, 3], sources.Select(...))`; `:44` - `Assert.Equal(2, smallSources.GetArrayLength())` | PASS |
| C23 | ask to A never retrieves/prompts B | `AskTests.Ask_never_retrieves_from_another_assistant` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:66-67` - `Assert.All(sources, s => Assert.Contains(documentId, docsOfA))`; `:68` - `Assert.DoesNotContain(secretOfB, ...PromptContaining(marker))` | PASS |
| C24 | prompt contains instructions, chunks, question | `AskTests.Ask_prompt_contains_instructions_chunks_and_question` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:86` - `Assert.Contains(instructions, prompt)`; `:87` - question; `:88` - each chunk text | PASS |
| C25 | no documents -> 200, sources [] | `AskTests.Ask_without_documents_returns_empty_sources` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:100` - `OK`; `:102-103` - array, length 0 | PASS |
| C26 | question bounds | `AskTests.Ask_question_bounds` (4) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:120` - `Assert.Equal(expected, response.StatusCode)`; `:122` - `TryGetProperty("question", out _)` | PASS |
| C27 | provider failures 502 problem, no provider message | `AskTests.Ask_provider_failure_returns_502` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:137` - `BadGateway`; `:139` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)` | PASS |
| C28 | 20 ok, 21st 429, other user 200 | `AskTests.Ask_rate_limit_is_20_per_minute_per_user` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:152` - OK x20; `:154` - `TooManyRequests`; `:155` - other user OK | PASS |
| C29 | 401 session -> /login; /register opens | `redirects to login without session` (3 tests) passed | `src/web/src/features/auth/auth.test.tsx:14` - `waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login'))`; `:24-25` - heading 'Criar conta', location '/register' (refreshed; +1 from import) | PASS |
| C30 | empty list text + "Criar IA" | `shows empty state` passed | `src/web/src/features/assistants/assistants.test.tsx:14` - `findByText('Você ainda não criou nenhuma IA')`; `:15` - `getByRole('button', { name: 'Criar IA' })` | PASS |
| C31 | documents, upload field, question box | `assistant page shows documents upload and question` passed | `src/web/src/features/assistants/assistants.test.tsx:67-68` - fileNames; `:69` - `toHaveAttribute('type', 'file')`; `:71` - `getByLabelText('Pergunta')` | PASS |
| C32 | answer and source fileNames | `shows answer and source file names` passed | `src/web/src/features/assistants/assistants.test.tsx:93-94` - `within(answer).getByText('politicas.pdf'/'contrato.md')` | PASS |
| C33 | problem title shown and input kept | `shows problem title and keeps input` (2) passed | `src/web/src/features/assistants/assistants.test.tsx:30-31` - `toHaveTextContent('Nome inválido')`, `toHaveValue('Minha IA')`; `:108-109` - ask | PASS |
| C34 | delete confirms; cancel no DELETE | `delete asks for confirmation` (2) passed | `src/web/src/features/assistants/assistants.test.tsx:52` - `expect(deletes).toEqual([])`; `:56` - `toEqual(['a1'])`; `:129`, `:132` - `[]`, `['d1']` | PASS |
| C35 | upload/ask button disabled "Processando..." | `disables button while processing` (2) passed | `src/web/src/features/assistants/assistants.test.tsx:150-151` - `findByRole('button', { name: 'Processando...' })`, `toBeDisabled()`; `:171-172` | PASS |
| C36 | vector(1536) + hnsw cosine index | `SchemaTests.Chunk_embedding_is_vector1536_with_hnsw_cosine_index` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:31` - `Assert.Equal("vector(1536)", type)`; `:33` - hnsw + `vector_cosine_ops` (above the fix hunk, unchanged) | PASS |
| C37 | concurrent duplicates -> 201 + 409, 1 doc | `DocumentsTests.Concurrent_duplicate_uploads_yield_one_201_one_409` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:216` - `Assert.Equal([Created, Conflict], statuses)`; `:217` - 1 doc | PASS |
| C38 | Features never reference OpenAI | `ArchitectureTests.Features_do_not_reference_OpenAI` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:143` - `Assert.NotEmpty(files)`; `:147` - `Assert.Empty(offenders)` (refreshed, +3) | PASS |
| C39 | 8 error statuses are problem+json | `ProblemDetailsTests.Error_responses_are_problem_json` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:70` - `Assert.Equal(status, (int)response.StatusCode)`; `:71` - `application/problem+json`; `:72` - body status (above the fix hunk, unchanged) | PASS |
| C40 | one Information log with ids/counts; no content in logs | `ObservabilityTests.Ingestion_logs_ids_and_counts_never_content` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:95` - `Assert.Single(ingestion)`; `:96` - `LogLevel.Information`; `:97-98` - ChunkCount/ElapsedMs keys; new `:101-103` - `logged = Entries.SelectMany(e => e.Properties.Values.Select(v => v?.ToString() ?? "").Append(e.Message))`; `:104-106` - `Assert.DoesNotContain(logged, text => text.Contains(documentSecret/questionSecret/answer))` (verified at 0747ee0) | PASS |
| C41 | SPA fallback vs /api 404 problem | `SpaHostingTests.Spa_fallback_serves_index_but_not_for_api` (2) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:121-123` - `OK`, `text/html`, SpaMarker; `:126-128` - `NotFound`, `application/problem+json`, no SpaMarker (refreshed, +3) | PASS |
| C42 | chunker rules | `TextChunkerTests` (5) Passed | `tests/BuildYourOwnAI.Api.Tests/Common/TextChunkerTests.cs:11` - `Assert.Empty(...)`; `:18` - `Assert.Single(...)`; `:26` - whitespace; `:39` - `Assert.Equal(chunks[i - 1][^200..], chunks[i][..200])` | PASS |
| C43 | manage/info 200 with email with session, 401 without | `AuthTests.Manage_info_reflects_session` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:131` - `Unauthorized`; `:140` - `OK`; `:142` - `Assert.Equal(email, body.GetProperty("email").GetString())`; new `:143` - `Assert.False(body.GetProperty("isEmailConfirmed").GetBoolean())` (verified at 0747ee0) | PASS |
| C44 | GET id 200 with 5 fields, documentCount | `AssistantsTests.Get_returns_200_with_document_count` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:156` - `OK`; `:158-161` - id, name, instructions, `TryGetProperty("createdAt")`; `:162` - `Assert.Equal(2, ...documentCount)` (refreshed, +5) | PASS |
| C45 | /login: pending "Processando..." disabled; 401 -> "E-mail ou senha inválidos." keeps e-mail; 200 -> /assistants | `login form states: ...` (2 tests) passed | `src/web/src/features/auth/auth.test.tsx:60` - `expect(await screen.findByRole('button', { name: 'Processando...' })).toBeDisabled()`; `:62` - `expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')`; `:63` - `toHaveValue('ana@test.local')`; `:77-78` - heading 'Minhas IAs', `location` `'/assistants'` (verified at 0747ee0; fault W1 killed) | PASS |
| C46 | /register: pending disabled; problem title keeps e-mail; success -> /assistants | `register form states: ...` (2 tests) passed | `src/web/src/features/auth/auth.test.tsx:94` - `findByRole('button', { name: 'Processando...' })).toBeDisabled()`; `:96` - `findByRole('alert')).toHaveTextContent('E-mail já cadastrado')`; `:97` - `toHaveValue('ana@test.local')`; `:115-116` - heading 'Minhas IAs', location '/assistants' (verified at 0747ee0) | PASS |
| C47 | /assistants shows "Carregando..." while GET /api/assistants is pending, then the list | `assistants list shows loading` passed | `src/web/src/features/assistants/assistants.test.tsx:189` - `expect(await screen.findByText('Carregando...')).toBeInTheDocument()`; `:191` - `findByRole('link', { name: 'Suporte' })`; `:192` - `queryByText('Carregando...')).not.toBeInTheDocument()`. **The assertion does not target the check-defined element.** `RequireAuth` renders the same `Carregando...` while `manage/info` is pending (`src/web/src/features/auth/RequireAuth.tsx`, `if (session.isPending) return <p className="p-6">Carregando...</p>`), and `:189` matches that text. Fault W2 deleted `AssistantsPage.tsx:53` and the test still passed. With `RequireAuth`'s loader also removed, the test failed, which confirms the cause. | FAIL |
| C48 | /assistants/{id} with documents [] shows no document item and shows the upload field | `empty document list shows only upload` passed | `src/web/src/features/assistants/assistants.test.tsx:207` - `waitFor(() => expect(within(section).queryByText('Carregando...')).not.toBeInTheDocument())`; `:208` - `expect(within(section).queryAllByRole('listitem')).toHaveLength(0)`; `:210` - `getByRole('button', { name: 'Enviar' })` (verified at 0747ee0) | PASS |
| C49 | upload problem details -> title shown on assistant page | `upload error shows problem title` passed | `src/web/src/features/assistants/assistants.test.tsx:224` - `expect(await screen.findByRole('alert')).toHaveTextContent('Formato não suportado. Use .pdf, .txt, .md.')` (verified at 0747ee0; fault W3 killed) | PASS |

**Level**: carried from bef8502 for C1-C44. C45-C49 are Testing Library + MSW tests, which is the level the `Test policy` row requires for React components. No level gaps.

**Precision notes (non-failing)** (verified at 0747ee0):

- **C40 does not see log scopes.** Its sink, `tests/BuildYourOwnAI.Api.Tests/Infrastructure/CapturingLoggerProvider.cs`, has `BeginScope` return `null`. Content carried in a logging scope would therefore never be captured. The claim reads "nenhum log", so scope data is still outside what the proof can see.
- **C40 also cannot see a leak at `Debug` level.** Such a leak is filtered by the shipped `appsettings.json` `"Default": "Information"`, so it is equivalent under the shipped configuration and not a finding.
- **C44's `createdAt` is asserted for presence only** (`AssistantsTests.cs:161`, `TryGetProperty`), carried from round 1.
- **The fix's new login mapping also changes `useRegister`.** The mapping in `src/web/src/features/auth/api.ts` also applies to the login step inside `useRegister`. No check covers a register that succeeds followed by a 401 on the automatic login. That case should be unreachable with correct credentials, so this is noted but not a gap.

**Swept existing**: carried from bef8502. No `Swept` row resolves to an existing constraint.

**Startup configuration**: carried from bef8502. The fix did not touch `Program.cs` or `ApiFactory.cs`.

## Coverage

The rows whose authority the fix touched were recomputed at 0747ee0: web screen states, Surface response fields for C9 and C43, and C40's log surface. All other rows are carried from bef8502. They are summarised in the last row of the table and appear in full in the round-1 report at `e579476`.

For screen states the authority is the plan's `Observable` table (`plan.md:232-238`) plus the `Test policy` row "cada estado: vazio, carregando, erro, sucesso". It is not the checks' own "estados por tela do Observable (11)" row. `Observable` also names `/assistants/{id}` unauthorised (AC 29), which the checks' 11-member row omits, so the recomputed set has 12 members. C29 proves that member (`auth.test.tsx:14`, the `/assistants/abc` case), so the omission costs nothing.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| Observable screen states (12) - verified at 0747ee0 | `plan.md:232-238` | `/login` error C45 · `/login` loading C45 · `/register` error C46 · `/register` loading C46 · `/assistants` empty C30 · `/assistants` error C33 · `/assistants` unauthorised C29 · `/assistants/{id}` empty C48 · `/assistants/{id}` error C33, C49 · `/assistants/{id}` loading C35 · `/assistants/{id}` unauthorised C29 · `/assistants` loading C47 (hollow: fault W2 survived) | `/assistants` loading: list `Carregando...` (`AssistantsPage.tsx:53`) is not proven, because C47 passes on `RequireAuth`'s text. The create button's `Processando...` (`AssistantsPage.tsx:48`), named unproven in round 1, still has no test. |
| React component states per Test policy - verified at 0747ee0 | components that branch on query/mutation state: `AuthForm.tsx`, `AssistantsPage.tsx`, `AssistantPage.tsx`, `RequireAuth.tsx` | `AuthForm` loading C45/C46, error C45/C46, success C45/C46 (empty n/a, public form) · `AssistantPage` empty C48, loading C35, error C33/C49, success C31/C32 · `AssistantsPage` empty C30, error C33, success C47:191/C34 · `RequireAuth` unauthorised C29, success (Outlet) implicit in C30-C35 | `AssistantsPage` loading (see above). `RequireAuth` loading (`Carregando...` while session pending) and non-401 error (`role="alert"` with `errorTitle`) were both named in round 1, and neither has a test (`rg "RequireAuth\|status: 500" src/web/src/**/*.test.tsx` returns no hits). |
| UI error display (5) - verified at 0747ee0 | checks row + `role="alert"` sites | create C33 · ask C33 · upload C49 · login C45 · register C46 | - |
| `GET /api/assistants` item fields (5) - verified at 0747ee0 | plan Surface `Out` + `ListAssistants.cs:8` `Item(Id, Name, Instructions, CreatedAt, DocumentCount)` | id C9 `:69-70` · name C9 `:73,75` · instructions C9 `:74,76` · documentCount C9 `:71-72` · createdAt C9 `:77` (ordering only) | `createdAt` value: `:77` asserts only `list[0].createdAt >= list[1].createdAt`. That holds when both are equal, and fault B1 (constant `default(DateTimeOffset)`) survived. |
| `GET /api/auth/manage/info` fields (2) - verified at 0747ee0 | Surface `Out`; Identity `InfoResponse` | email C43 `:142` · isEmailConfirmed C43 `:143` | - |
| log content surfaces (C40) - verified at 0747ee0 | `CapturedLog` record: Message, Properties | rendered message C40 `:104-106` · structured property values C40 `:101-106` (fault B2 killed) | - |
| all other rows (route statuses x12, protected routes 8, `{id}` x {foreign, missing} 12, problem+json 8, extensions, size bounds, missing input, no text, name/instructions/question bounds, top-K, provider failures, rate limit, chunker rules, destructive UI 2, doors, startup config) | carried from bef8502 | as in round 1, with no member unproven | - |

## Test policy rows

The React component row and the rows classifying touched files were re-judged at 0747ee0. The others are carried from bef8502.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Endpoint that decides (validation, ownership, dedup, limits) | `Features/**/*.cs` | HTTP boundary vs real Postgres | yes - carried from bef8502; fix touched only test assertions (C9, C43) that strengthen it |
| Pure decision behind an endpoint (chunker) | `Common/TextChunker.cs` | boundary C12 · own level C42 | yes - carried from bef8502 |
| React component that decides what to show | `AuthForm.tsx` (+ `LoginPage.tsx`, `RegisterPage.tsx`, `auth/api.ts` login mapping), `AssistantsPage.tsx`, `AssistantPage.tsx`, `RequireAuth.tsx` | Testing Library + MSW | not met. `AuthForm` and `AssistantPage` are now met. The row fails on two components. `AssistantsPage` loading is untested in effect: C47 survives removal of the list loader (fault W2), and the create `Processando...` has no test. `RequireAuth` has no test of its loading state or its non-401 error state, both named in round 1. |
| Instrumentation (DI, route mapping, DTO) | `AssistantsEndpoints.cs`, `AuthEndpoints.cs`, AI registration, records | covered by consumer's proof | yes, carried from bef8502. One caveat: the `ListAssistants.Item` DTO `CreatedAt` mapping is not discriminated by its consumer proof (fault B1). That is recorded under Coverage. |

## Faults injected

Verified at 0747ee0. The worktree was `git worktree add <scratchpad>/wt2 HEAD` at `0747ee0`. Each fault was applied alone and reverted with `git checkout -- .` inside the worktree, and the worktree's `git status --porcelain` was empty between faults. Web runs used a directory junction to the real `src/web/node_modules`. The junction was unlinked with `(Get-Item $J).Delete()`, and the target still exists. The worktree was then removed with `git worktree remove --force`.

The real tree's `git status --porcelain` was empty before the faults and empty after, so it matches the baseline. The round-1 faults F1-F5 are carried from bef8502: all were killed, and their surfaces were not touched by the fix.

| Mutation | Location | Killed |
| --- | --- | --- |
| W1: login 401 mapping removed: `if (error instanceof ApiError && error.status === 401) throw new ApiError(401, 'E-mail ou senha inválidos.')` -> comment | `src/web/src/features/auth/api.ts:22` | yes - C45 `login form states: disabled while pending, then invalid credentials keep the e-mail` failed (`Expected element to have text content`) |
| W2: list loading text removed: `{assistants.isPending && <p>Carregando...</p>}` -> comment | `src/web/src/features/assistants/AssistantsPage.tsx:53` | no - survived: C47 `assistants list shows loading` still passed, because `findByText('Carregando...')` matched `RequireAuth`'s session loader. A control run that also blanked `RequireAuth`'s loader made C47 fail. |
| W3: upload error alert removed: `{upload.isError && <p role="alert" ...>}` -> comment | `src/web/src/features/assistants/AssistantPage.tsx:60` | yes - C49 `upload error shows problem title` failed (`Unable to find role="alert"`) |
| B1: list DTO `createdAt` dropped: `new Item(a.Id, a.Name, a.Instructions, a.CreatedAt, ...)` -> `default(DateTimeOffset)` | `src/BuildYourOwnAI.Api/Features/Assistants/ListAssistants.cs:16` | no - survived: C9 `List_returns_only_own_newest_first` Passed, because `:77` compares `>=` and equal constants satisfy it |
| B2: document content logged only in a structured property: `logger.Log(LogLevel.Information, default, new List<KeyValuePair<string, object?>> { new("Preview", chunks[0]) }, null, (_, _) => "ingestion preview captured")` added after the ingestion log | `src/BuildYourOwnAI.Api/Features/Documents/UploadDocument.cs:94` | yes - C40 `Ingestion_logs_ids_and_counts_never_content` failed (`Assert.DoesNotContain() Failure: Filter matched in collection`). A first attempt at `LogLevel.Debug` passed, but the shipped `Default: Information` filter drops it in production too. It is an equivalent mutant and not counted. |

## Gate

- `dotnet test C:\Personal-Projects\BuildYourOwnAI\tests\BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` (DOCKER_HOST npipe) at 0747ee0: 81 passed, 0 failed.
- `npm --prefix C:/Personal-Projects/BuildYourOwnAI/src/web run test -- --reporter=verbose` at 0747ee0: 19 passed, 0 failed.

**Ranked gaps:**

1. **C47 is hollow, a surviving mutant.** The `/assistants` list loading state is not proven. The assertion at `src/web/src/features/assistants/assistants.test.tsx:189` matches `RequireAuth`'s `Carregando...` (`src/web/src/features/auth/RequireAuth.tsx`), and deleting `AssistantsPage.tsx:53` leaves the test green. The test must wait for the session to resolve, for example by asserting a page element first, or scope the query to the list region, before asserting the loader.
2. **The `createdAt` value in the C9 list DTO is unproven, a surviving mutant.** `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:77` asserts only `>=`, and a constant `createdAt` passes.
3. **The `Test policy` React row is unmet for `RequireAuth`.** Its loading and non-401 error states have no test, and round 1 named both. There is no evidence in `src/web/src/features/auth/auth.test.tsx`.
4. **`/assistants` create button `Processando...` (`AssistantsPage.tsx:48`) has no test.** Round 1 named this `Observable` loading member and the fix did not address it.

Lessons (step 7) were not written, because the brief allows only `verification.md` to be modified. The orchestrator should distill gaps 1 and 2 with `scripts/lessons.py`:

- A loading-text assertion must be scoped away from a shared loader.
- An ordering-only comparison does not prove a field's value.
