# RAG MVP verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 0579a65..bef8502
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

All 44 checks are proven with located evidence at `bef8502`, and all 5 injected faults were killed. The verdict is FAIL for two reasons. The `Test policy` row for React components is not met. And the Coverage recompute finds plan-named members with no proof: UI error and loading states on `/login`, `/register` and `/assistants`, the empty document list on `/assistants/{id}`, and some response fields named in `Surface`. None of these is a red test. They are states and fields the plan names that no check or test asserts.

## Binding sources

No binding source: the plan's `Sources` are the user's chat request and decisions, with no design or contract file. The profile is `standard`, so step 1 (`ui` only) does not run.

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| none (chat request / decisions only) | n/a | - | - |

## Checks

Proofs run at `bef8502` (verified at bef8502):

- **API**: one full-project run: `$env:DOCKER_HOST="npipe://./pipe/dockerDesktopLinuxEngine"; dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`. It exited 0 with 81 passed and 0 failed, and every test and theory case appeared individually as `Passed <FullyQualifiedName>`.
- **Web**: one batched run: `npm --prefix src/web run test -- --reporter=verbose -t "<7 check names, regex alternation>"`. It exited 0 with 12 of 12 passed. Each of the 7 names matched at least one test. There were no zero-match filters and no `passWithNoTests`.

All proof tests are in files added in the diff range. None resolves to a pre-existing test.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | register 200, then login 200 | `AuthTests.Register_valid_returns_200_and_enables_login` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:22` - `Assert.Equal(HttpStatusCode.OK, register.StatusCode)`; `:25` - `Assert.Equal(HttpStatusCode.OK, login.StatusCode)` | PASS |
| C2 | duplicate e-mail 400 problem+json with errors.DuplicateUserName | `AuthTests.Register_duplicate_email_returns_400_DuplicateUserName` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:38` - `Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode)`; `:39` - `Assert.Equal("application/problem+json", ...MediaType)`; `:40` - `GetProperty("errors").TryGetProperty("DuplicateUserName", out _)` | PASS |
| C3 | login 200 + session cookie httponly/secure/samesite=strict | `AuthTests.Login_valid_sets_httponly_secure_strict_cookie` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:53` - `Assert.Equal(HttpStatusCode.OK, login.StatusCode)`; `:56-58` - `Assert.Contains("httponly"/"secure"/"samesite=strict", attributes)` | PASS |
| C4 | wrong password and unknown e-mail 401, no session cookie | `AuthTests.Login_invalid_returns_401_without_cookie` (2 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:76` - `Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode)`; `:77` - `Assert.DoesNotContain(SetCookies(login), c => c.StartsWith(SessionCookie))` | PASS |
| C5 | logout 204, cookie expired, next call 401 | `AuthTests.Logout_returns_204_and_ends_session` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:89` - `Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode)`; `:91` - `Assert.Contains("expires=thu, 01 jan 1970", ...)`; `:92` - `Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/assistants")).StatusCode)` | PASS |
| C6 | 8 `/api/assistants*` routes without cookie 401 (never 302) | `AuthTests.Protected_route_without_session_returns_401` (8 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:101-108` (8 routes), `:123` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)`; client has `AllowAutoRedirect = false` (`Infrastructure/ApiFactory.cs:65`) | PASS |
| C7 | create 201, Location, trimmed name 1/100, instructions absent/4000 | `AssistantsTests.Create_valid_returns_201_with_location` (3 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:22` - `Assert.Equal(HttpStatusCode.Created, ...)`; `:25` - `Assert.Equal($"/api/assistants/{id}", response.Headers.Location?.OriginalString)`; `:26` - `Assert.Equal(name, body.GetProperty("name")...)`; `:27` - instructions; `:28` - createdAt | PASS |
| C8 | name ""/"   "/101 -> errors.name; instructions 4001 -> errors.instructions | `AssistantsTests.Create_invalid_returns_400_keyed_by_field` (5 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:46` - `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:47` - `GetProperty("errors").TryGetProperty(field, out _)` | PASS |
| C9 | list [] for new user, then [B, A] with documentCount, only own | `AssistantsTests.List_returns_only_own_newest_first` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:60` - `Assert.Equal(0, ...GetArrayLength())`; `:68` - `Assert.Equal(2, list.GetArrayLength())` (other user has 1); `:69-70` - `Assert.Equal(b, list[0]...)`, `Assert.Equal(a, list[1]...)`; `:71-72` - documentCount 0/1 | PASS |
| C10 | 6 `{id}` routes x {foreign, missing} 404, foreign data unchanged | `AssistantsTests.Foreign_or_missing_assistant_returns_404` (12 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:116` - `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)`; `:117` - `Assert.Equal(1, await DocumentCountAsync(foreignId))` | PASS |
| C11 | owner delete 204, no Document/Chunk rows left | `AssistantsTests.Delete_returns_204_and_cascades` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:134` - `Assert.Equal(HttpStatusCode.NoContent, ...)`; `:136` - `Assert.Equal(0, await DocumentCountAsync(id))`; `:137` - `Assert.Equal(0, await ScalarAsync(chunksOfDocs, ...))` | PASS |
| C12 | .txt/.md/.pdf 201, chunkCount>0, fileName/sizeBytes, chunkCount rows with 1536 dims | `DocumentsTests.Upload_supported_file_returns_201_and_persists_chunks` (3 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:32` - `Created`; `:36` - `Assert.True(chunkCount > 0)`; `:37-38` - fileName, sizeBytes; `:40` - `Assert.Equal(chunkCount, ...count(*) from chunks...)`; `:41-42` - `... vector_dims(embedding) = 1536` | PASS |
| C13 | .exe/.docx/no extension 415; NOTAS.TXT 201 | `DocumentsTests.Upload_extension_is_checked_case_insensitively` (4 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:47-50` (cases), `:58` - `Assert.Equal(expected, response.StatusCode)` | PASS |
| C14 | 10 485 761 -> 413; 10 485 760 -> 201 | `DocumentsTests.Upload_size_limit_is_10485760_bytes` (2 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:63-64` - `[InlineData(MaxBytes + 1, RequestEntityTooLarge)]`, `[InlineData(MaxBytes, Created)]` with `MaxBytes = 10_485_760` (`:10`); `:76` - `Assert.Equal(expected, response.StatusCode)` | PASS |
| C15 | no file part, 0 bytes, non-multipart -> 400 | `DocumentsTests.Upload_without_file_returns_400` (3 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:97` - `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` | PASS |
| C16 | blank .txt and textless PDF -> 422, 0 documents | `DocumentsTests.Upload_without_text_returns_422` (2 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:113` - `Assert.Equal(HttpStatusCode.UnprocessableEntity, ...)`; `:114` - `Assert.Equal(0, await DocumentCountAsync(id))` | PASS |
| C17 | same content 201 then 409 (other name), 1 doc; other assistant 201 | `DocumentsTests.Upload_duplicate_content_returns_409` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:126` - `Created`; `:127` - `Assert.Equal(HttpStatusCode.Conflict, ...)`; `:128` - `Assert.Equal(1, await DocumentCountAsync(id))`; `:129` - other assistant `Created` | PASS |
| C18 | embedding failure 502, no provider message, 0 Document 0 Chunk | `DocumentsTests.Upload_embedding_failure_returns_502_and_persists_nothing` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:142` - `Assert.Equal(HttpStatusCode.BadGateway, ...)`; `:143` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)`; `:144-145` - Document/Chunk counts 0 | PASS |
| C19 | documents list newest first with 5 fields | `DocumentsTests.List_returns_newest_first` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:162-163` - `Assert.Equal(second, list[0]...)`, `Assert.Equal(first, list[1]...)`; `:164-165` - `TryGetProperty(field)` for fileName/sizeBytes/chunkCount/uploadedAt | PASS |
| C20 | document delete 204, chunks gone, not in later ask sources | `DocumentsTests.Delete_returns_204_and_removes_from_retrieval` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:180` - `NoContent`; `:181` - `Assert.Equal(0, ...count(*) from chunks where document_id = @d...)`; `:183` - `Assert.DoesNotContain(...sources..., s => ...documentId == removed)` | PASS |
| C21 | delete doc of sibling assistant 404, doc of B remains | `DocumentsTests.Delete_document_of_other_assistant_returns_404` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:197` - `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:198` - `Assert.Equal(1, await DocumentCountAsync(b))` | PASS |
| C22 | 7 chunks -> exactly top-5 by cosine, in order, with 4 fields; 2 chunks -> 2 | `AskTests.Ask_returns_answer_with_top5_sources` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:27` - `OK`; `:29` - answer not blank; `:31` - `Assert.Equal([7, 6, 5, 4, 3], sources.Select(...))`; `:35-37` - fileName, chunkIndex, excerpt; `:44` - `Assert.Equal(2, smallSources.GetArrayLength())` | PASS |
| C23 | ask to A never returns/prompts B's chunks | `AskTests.Ask_never_retrieves_from_another_assistant` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:66-67` - `Assert.All(sources, s => Assert.Contains(documentId, docsOfA))`; `:68` - `Assert.DoesNotContain(secretOfB, Factory.Chat.PromptContaining(marker))` | PASS |
| C24 | prompt contains instructions, each source chunk text, question | `AskTests.Ask_prompt_contains_instructions_chunks_and_question` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:84` - 2 sources; `:86` - `Assert.Contains(instructions, prompt)`; `:87` - question; `:88` - each chunk text | PASS |
| C25 | no documents -> 200, sources [] | `AskTests.Ask_without_documents_returns_empty_sources` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:100` - `OK`; `:102-103` - `JsonValueKind.Array`, `GetArrayLength() == 0` | PASS |
| C26 | question ""/"   "/2001 -> 400 errors.question; 2000 -> 200 | `AskTests.Ask_question_bounds` (4 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:108-111` (cases), `:120` - `Assert.Equal(expected, response.StatusCode)`; `:122` - `GetProperty("errors").TryGetProperty("question", out _)` | PASS |
| C27 | embed and chat failure -> 502 problem, no provider message | `AskTests.Ask_provider_failure_returns_502` (2 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:137` - `BadGateway`; `:138` - `application/problem+json`; `:139` - `Assert.DoesNotContain(FakeAiTriggers.ProviderSecretMessage, ...)` | PASS |
| C28 | 20 asks 200, 21st 429, other user 200 | `AskTests.Ask_rate_limit_is_20_per_minute_per_user` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AskTests.cs:152` - `Assert.Equal(HttpStatusCode.OK, ...)` for i 1..20; `:154` - `Assert.Equal(HttpStatusCode.TooManyRequests, ...)`; `:155` - other user `OK` | PASS |
| C29 | 401 session -> /assistants and /assistants/abc go to /login; /register opens | `redirects to login without session` (3 tests) passed | `src/web/src/features/auth/auth.test.tsx:13` - `expect(screen.getByTestId('location')).toHaveTextContent('/login')`; `:23-24` - heading 'Criar conta', location '/register' | PASS |
| C30 | empty list shows "Você ainda não criou nenhuma IA" and "Criar IA" | `shows empty state` passed | `src/web/src/features/assistants/assistants.test.tsx:14` - `findByText('Você ainda não criou nenhuma IA')`; `:15` - `getByRole('button', { name: 'Criar IA' })` | PASS |
| C31 | assistant page shows document fileNames, upload field, question box | `assistant page shows documents upload and question` passed | `src/web/src/features/assistants/assistants.test.tsx:67-68` - 'politicas.pdf', 'manual.txt'; `:69` - `toHaveAttribute('type', 'file')`; `:71` - `getByLabelText('Pergunta')` | PASS |
| C32 | after asking, answer and each source fileName shown | `shows answer and source file names` passed | `src/web/src/features/assistants/assistants.test.tsx:93-94` - `within(answer).getByText('politicas.pdf')`, `'contrato.md'` | PASS |
| C33 | problem title shown and input kept (create IA, ask) | `shows problem title and keeps input` (2 tests) passed | `src/web/src/features/assistants/assistants.test.tsx:30-31` - `toHaveTextContent('Nome inválido')`, `toHaveValue('Minha IA')`; `:108-109` - `'O provedor de IA falhou.'`, `toHaveValue('Qual o prazo?')` | PASS |
| C34 | delete (assistant, document) confirms; cancel no DELETE; confirm DELETE | `delete asks for confirmation` (2 tests) passed | `src/web/src/features/assistants/assistants.test.tsx:51-52` - `toHaveBeenCalledTimes(1)`, `expect(deletes).toEqual([])`; `:56` - `toEqual(['a1'])`; `:128-129`, `:132` - `toEqual(['d1'])` | PASS |
| C35 | upload and ask button disabled showing "Processando..." | `disables button while processing` (2 tests) passed | `src/web/src/features/assistants/assistants.test.tsx:150-151` - `findByRole('button', { name: 'Processando...' })`, `toBeDisabled()`; `:171-172` - same for ask | PASS |
| C36 | chunks.embedding vector(1536) + hnsw vector_cosine_ops index | `SchemaTests.Chunk_embedding_is_vector1536_with_hnsw_cosine_index` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:31` - `Assert.Equal("vector(1536)", type)`; `:33` - `def.Contains("USING hnsw") && def.Contains("(embedding vector_cosine_ops)")` | PASS |
| C37 | concurrent duplicate uploads -> one 201 one 409, 1 doc | `DocumentsTests.Concurrent_duplicate_uploads_yield_one_201_one_409` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentsTests.cs:216` - `Assert.Equal([Created, Conflict], statuses)`; `:217` - `Assert.Equal(1, await DocumentCountAsync(id))` | PASS |
| C38 | no file in Features references OpenAI | `ArchitectureTests.Features_do_not_reference_OpenAI` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:140` - `Assert.NotEmpty(files)`; `:144` - `Assert.Empty(offenders)` | PASS |
| C39 | 400/404/409/413/415/422/429/502 are problem+json with matching status | `ProblemDetailsTests.Error_responses_are_problem_json` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:52-59,66` (all 8 statuses), `:70` - `Assert.Equal(status, (int)response.StatusCode)`; `:71` - `application/problem+json`; `:72` - `GetProperty("status").GetInt32() == status` | PASS |
| C40 | one Information log with DocumentId/ChunkCount/ElapsedMs; no content in logs | `ObservabilityTests.Ingestion_logs_ids_and_counts_never_content` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:95` - `Assert.Single(ingestion)`; `:96` - `LogLevel.Information`; `:97-98` - ChunkCount, ElapsedMs keys; `:101-103` - `Assert.DoesNotContain(all, e => e.Message.Contains(documentSecret/questionSecret/answer))` | PASS |
| C41 | / and /assistants/x -> index.html 200 text/html; /api/nao-existe -> 404 problem | `SpaHostingTests.Spa_fallback_serves_index_but_not_for_api` (2 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:118-120` - `OK`, `text/html`, SpaMarker; `:123-125` - `NotFound`, `application/problem+json`, no SpaMarker | PASS |
| C42 | chunker: blank->0, 1000->1, 2500->chunks <=1000 with 200 overlap, whitespace collapse | `TextChunkerTests` (5 cases) Passed | `tests/BuildYourOwnAI.Api.Tests/Common/TextChunkerTests.cs:11` - `Assert.Empty(TextChunker.Split(text))`; `:18` - `Assert.Single(...)`; `:26` - `Assert.Equal(["um dois tres"], ...)`; `:37` - `Assert.InRange(c.Length, 1, 1000)`; `:39` - `Assert.Equal(chunks[i - 1][^200..], chunks[i][..200])` | PASS |
| C43 | manage/info 200 with email with session, 401 without | `AuthTests.Manage_info_reflects_session` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:131` - `Unauthorized`; `:140` - `OK`; `:141` - `Assert.Equal(email, ...GetProperty("email")...)` | PASS |
| C44 | GET id 200 with 5 fields, documentCount = uploads | `AssistantsTests.Get_returns_200_with_document_count` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:151` - `OK`; `:153-156` - id, name, instructions, createdAt; `:157` - `Assert.Equal(2, body.GetProperty("documentCount").GetInt32())` | PASS |

**Level**: every claim that names a status, route or response shape (C1-C28, C36-C41, C43, C44) crosses the HTTP boundary through `WebApplicationFactory<Program>` against a real `pgvector/pgvector:pg17` container. C42 is the only unit-level proof, as the Test policy allows, and C12 covers the chunker at the boundary. No level gaps.

**Precision notes (non-failing):**

- C40 checks for leaked content in the formatted `e.Message` only. A structured property carrying content that is not rendered into the template would not be caught. The claim says "nenhum log ... contém o texto", which is imprecise about properties.
- C42's claim reads "começa até 200 caracteres antes do fim". The test asserts an overlap of exactly 200, which is stricter and consistent with the claim.
- C22 relies on the deterministic bag-of-words fake embedding (`Infrastructure/FakeAi.cs`) for the cosine ordering. The expected order `[7, 6, 5, 4, 3]` is readable at the assertion.

**Swept existing**: no `Swept` row resolves to an existing constraint. All rows point to checks, and `state transitions` is `n/a`, which is approved policy. There is nothing to re-read.

**Startup configuration**: one assembly. `src/BuildYourOwnAI.Api/Program.cs` configures:

- the cookie at `:24-33`
- the rate limiter (`PermitLimit = 20`, 60 s window, per `NameIdentifier`) at `:35-46`
- problem details at `:13` and `:58-59`
- the `/api/{**rest}` 404 and the SPA fallback at `:70-71`

`tests/BuildYourOwnAI.Api.Tests/Infrastructure/ApiFactory.cs:44-60` overrides only the AI services, the connection string, the webroot and logging. The proofs exercise the shipped configuration.

## Coverage

This section was recomputed at bef8502. Route statuses come from the plan's `Surface`, checked against the endpoint code: `Features/*` and the `MapIdentityApi` / `RequireAuthorization` group. Response fields also come from `Surface`. UI states come from the plan's `Observable` table and the `Test policy` row for React components.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/auth/register` statuses (2) | Surface; Identity `MapIdentityApi` (`Features/Auth/AuthEndpoints.cs:13`) | 200 C1 · 400 C2 | - |
| `POST /api/auth/login` statuses (2) | Surface; Identity | 200 C3 · 401 C4 | - |
| `POST /api/auth/logout` statuses (1) | Surface; `AuthEndpoints.cs:16-20` (always 204) | 204 C5 | - |
| `GET /api/auth/manage/info` statuses (2) | Surface; Identity | 200 C43 · 401 C43 | - |
| `POST /api/assistants` statuses (3) | Surface; `CreateAssistant.cs` (Created / ValidationProblem) + group auth | 201 C7 · 400 C8 · 401 C6 | - |
| `GET /api/assistants` statuses (2) | Surface; `ListAssistants.cs` | 200 C9 · 401 C6 | - |
| `GET /api/assistants/{id}` statuses (3) | Surface; `GetAssistant.cs` | 200 C44 · 401 C6 · 404 C10 | - |
| `DELETE /api/assistants/{id}` statuses (3) | Surface; `DeleteAssistant.cs` | 204 C11 · 401 C6 · 404 C10 | - |
| `POST /api/assistants/{id}/documents` statuses (9) | Surface; `UploadDocument.cs:32-97` (404, 400 x2, 415, 413, 409 x2, 422, 502, 201) | 201 C12 · 400 C15 · 401 C6 · 404 C10 · 409 C17, C37 · 413 C14 · 415 C13 · 422 C16 · 502 C18 | - |
| `GET /api/assistants/{id}/documents` statuses (3) | Surface; `ListDocuments.cs` | 200 C19 · 401 C6 · 404 C10 | - |
| `DELETE /api/assistants/{id}/documents/{documentId}` statuses (3) | Surface; `DeleteDocument.cs` (assistant 404, document 404) | 204 C20 · 401 C6 · 404 assistant C10 · 404 document C21 | - |
| `POST /api/assistants/{id}/ask` statuses (6) | Surface; `AskAssistant.cs` + rate limiter `Program.cs:35-46` | 200 C22 · 400 C26 · 401 C6 · 404 C10 · 429 C28 · 502 C27 | - |
| protected routes without session (8) | endpoint group `AssistantsEndpoints.cs:10-21` (8 maps) | C6, one case per route | - |
| `{id}` routes x {foreign, missing} (12) | the 6 `{id}` maps x 2 | C10, 12 theory cases | - |
| error statuses are problem+json (8) | Surface / door 7 | C39 covers all 8 | - |
| accepted extensions (3) + case | `TextExtractor.cs:9` | .txt .md .pdf C12 · .TXT C13 | - |
| upload size bounds (2) | `UploadDocument.cs:13` `MaxBytes = 10_485_760` | 10 485 760 C14 · 10 485 761 C14 | - |
| upload missing input (3) | `UploadDocument.cs:35-40` | no part, 0 bytes, non-multipart C15 | - |
| no extractable text (2) | `UploadDocument.cs:61-65` | blank .txt, textless PDF C16 | - |
| `name` bounds (5) | `CreateAssistant.cs` | empty, spaces, 101 C8 · 1, 100 C7 | - |
| `instructions` bounds (3) | `CreateAssistant.cs` | absent, 4000 C7 · 4001 C8 | - |
| `question` bounds (4) | `AskAssistant.cs:37-41` | empty, spaces, 2001, 2000 C26 | - |
| top-K (2) | `AskAssistant.cs` `TopK = 5` | more than 5 C22 · fewer than 5 C22 | - |
| provider failures (3) | `AiProviderCall` call sites | upload embed C18 · ask embed C27 · ask chat C27 | - |
| rate limit (3) | `Program.cs:39` | 20th C28 · 21st C28 · other user C28 | - |
| chunker rules (4) | `TextChunker.cs` | blank, fits-in-one, overlap, whitespace C42 | - |
| Surface response fields (all routes) | Surface `Out` column | POST assistants 4 fields C7 · GET id 5 fields C44 · upload 5 fields C12 · list documents 5 fields C19 · ask fields C22 · manage/info `email` C43 | `GET /api/assistants` items: `name`, `instructions`, `createdAt` (C9 asserts only `id`, `documentCount`); `GET /api/auth/manage/info`: `isEmailConfirmed` |
| UI screen states per plan `Observable` | plan `Observable` rows + Test policy row 3 (empty / loading / error / success) | `/assistants` empty C30, error on create C33, success list implicit in C34 · `/assistants/{id}` success C31, C32, loading on upload/ask C35, error on ask C33 · unauthorised C29 | `/login` and `/register` error state (Observable: "error AC 33") and loading state (Observable: "loading AC 35") · `/assistants` loading (`Carregando...`, create button `Processando...`; Observable: "loading AC 35") · `/assistants/{id}` empty document list (Observable: "a lista vazia mostra só o campo de upload") · upload error (`AssistantPage.tsx` `upload.isError`) |
| destructive actions in UI (2) | `AssistantsPage.tsx:25`, `AssistantPage.tsx:45` | assistant C34 · document C34 | - |
| doors (9 + 5b) | Landing | 1 C12, C36 · 2 C36, C22 · 3 C11, C17, C37 · 4 C3, C5 · 5 C10, C23 · 5b C10 (DELETE-document case survives fault F2 through the Document filter) · 6 C38 · 7 C39 · 8 C18 · 9 C41 | - |
| startup config (1 assembly) | `Program.cs` read directly | cookie C3, rate limiter C28, problem details C39, SPA C41 | - |

The checks' own Coverage table narrows "erro exibido na UI" and "ações em andamento na UI" to two members each. The plan's `Observable` table, which is the authority for screen states, assigns error and loading to `/login`, `/register` and `/assistants` as well. Those members were in the author's own artifact and got no row. There is also a plan-level precision issue: `Observable` maps `/login` and `/assistants` loading to AC 35, but AC 35's text covers only upload and ask.

## Test policy rows

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| Endpoint that decides (validation, ownership, dedup, limits) | `Features/Assistants/*.cs`, `Features/Documents/*.cs`, `Features/Ask/AskAssistant.cs` | HTTP boundary vs real Postgres: C6-C28, C37, C39, C43, C44 | yes - every accepted input, rejected input and error path has a member in Coverage above and a boundary proof |
| Pure decision behind an endpoint (chunker) | `Common/TextChunker.cs` | boundary C12 · own level C42 | yes - all 4 rules (blank, fits in one, overlap, normalization) asserted in `TextChunkerTests.cs:11,18,26,39` |
| React component that decides what to show | `AssistantsPage.tsx`, `AssistantPage.tsx`, `AuthForm.tsx` (+ `LoginPage.tsx`, `RegisterPage.tsx`), `RequireAuth.tsx` | Testing Library + MSW | no - "each state: empty, loading, error, success" is not met. `AuthForm` (login/register) has no test for error, loading or success; no web test submits the login or register form. `AssistantsPage` loading (`Carregando...`, create `Processando...`) is untested. `AssistantPage` empty document list and upload error are untested. `RequireAuth` loading and non-401 error are untested. |
| Instrumentation (DI registration, route mapping, DTO) | `AssistantsEndpoints.cs`, `AuthEndpoints.cs` mapping, `AiServiceCollectionExtensions.cs`, records | none of its own; covered by the consumer's proof | yes - route mapping exercised by C6/C10; AI registration by `UnconfiguredAiTests` and the fakes swap |

## Faults injected

Each fault was applied in a `git worktree add <scratchpad>/wt HEAD` at `bef8502`, one at a time, then reverted with `git checkout -- .` inside the worktree. The web fault used a junction to the real `src/web/node_modules`; the junction was removed with `rmdir` before `git worktree remove --force`. Real-tree `git status --porcelain` was empty before and empty after, so it matches the baseline.

| Mutation | Location | Killed |
| --- | --- | --- |
| F1: retrieval assistant filter `.Where(c => c.Document.AssistantId == assistantId)` -> `.Where(c => true)` | `src/BuildYourOwnAI.Api/Features/Ask/AskAssistant.cs:81` | yes - C23 `Ask_never_retrieves_from_another_assistant` failed (`Assert.All() Failure: 5 out of 5 items ... Item not found`) |
| F2: owner query filter `a.OwnerId == CurrentUserId` -> `a.OwnerId == CurrentUserId` OR `a.OwnerId != null` (always true) | `src/BuildYourOwnAI.Api/Infrastructure/Data/AppDbContext.cs:29` | yes - C10 `Foreign_or_missing_assistant_returns_404` failed 5 of 12 cases (all foreign cases except DELETE document, which the Document filter from door 5b still guards) |
| F3: unique-violation catch `return AlreadyAttached();` -> `throw;` | `src/BuildYourOwnAI.Api/Features/Documents/UploadDocument.cs:88` | yes - C37 `Concurrent_duplicate_uploads_yield_one_201_one_409` failed (`Expected [Created, Conflict]`, `Actual [Created, InternalServerError]`) |
| F4: rate limit `PermitLimit = 20` -> `21` | `src/BuildYourOwnAI.Api/Program.cs:39` | yes - C28 `Ask_rate_limit_is_20_per_minute_per_user` failed (`Expected TooManyRequests`, `Actual OK`) |
| F5: document delete ignores confirm result: `if (window.confirm(...)) remove.mutate(id)` -> `window.confirm(...); remove.mutate(id)` | `src/web/src/features/assistants/AssistantPage.tsx:45` | yes - C34 `delete asks for confirmation (document)` failed (`expected [ 'd1' ] to deeply equal []`) |

## Gate

- `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` (DOCKER_HOST npipe): 81 passed, 0 failed.
- `npm --prefix src/web run test -- --reporter=verbose`: 12 passed, 0 failed.

**Ranked gaps:**

1. Test policy row "React component that decides what to show" is unmet. `AuthForm` (login/register) has no error, loading or success test, and no web test submits the login or register form. There is no evidence in `src/web/src/features/auth/auth.test.tsx` beyond the redirect.
2. Coverage members from the plan's `Observable` are unproven: `/login` and `/register` error and loading; `/assistants` loading; `/assistants/{id}` empty document list; upload error display. There is no evidence in `src/web/src/features/assistants/assistants.test.tsx`.
3. Surface response fields are unproven: `GET /api/assistants` items `name`, `instructions` and `createdAt` (C9 at `tests/BuildYourOwnAI.Api.Tests/Features/AssistantsTests.cs:69-72` asserts only `id` and `documentCount`), and `GET /api/auth/manage/info` `isEmailConfirmed` (C43 at `AuthTests.cs:141` asserts only `email`).
4. Precision gap in C40: the leak check covers only the formatted message (`tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:101-103`), not structured properties.

Lessons (step 7) were not written. The Verifier's brief allows only `verification.md` to be modified, so the orchestrator should distill gaps 1-4 with `scripts/lessons.py`.
