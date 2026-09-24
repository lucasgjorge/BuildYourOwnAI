# Prévia do trecho e escolha automática verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 427a40c..2d13fc7 (round-1 FAIL at fe854c9; fix fe854c9..2d13fc7, excluding 66e482f which only adds `.specs/features/study-mode/plan.md`)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Round 2 scope: the fix diff `fe854c9..2d13fc7` (`src/web/src/features/chat/SourcePreview.tsx`, `src/web/src/features/chat/sourcePreview.test.tsx`, `tests/.../RoutingChoiceTests.cs`, `tests/.../CrossCuttingTests.cs`) plus every non-PASS item from round 1 (AC 17 for found answers, AC 13 `TimeoutSeconds`, the C18 selector). Proofs re-run in full at `2d13fc7`. Sections say whether they were verified at `2d13fc7` or carried from `fe854c9`. The real tree was not touched.

## Binding sources

Carried from `fe854c9`. No binding sources: the plan marks none. Step 1 did not apply. The fix changed the preview's scroll behaviour, not its interface or screens.

## Checks

Verified at `2d13fc7`. Proof runs, one invocation per target:

- API (PowerShell, `DOCKER_HOST=npipe://./pipe/dockerDesktopLinuxEngine`): `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`. Exit 0, 191 passed, 0 failed. Each named API test below appears individually as `Passed`, including both `Timeout_comes_from_routing_config` cases.
- Web: `npm --prefix src/web run test -- --reporter=verbose`. Exit 0, 8 files, 63 passed. Each named web test below appears individually with `✓`.
- C10 (Git Bash, repo root, exactly as written in checks.md): exit 0. Unfiltered, the grep matches only `jev-latest` lines: `README.md:24,51,174`, `docs/PRD.md:49,112`.

C17, C18 and C19 were re-judged in full. C8's citations were refreshed, because `RoutingChoiceTests.cs` moved by +3 lines. The other checks sit in lines the fix did not move, so their judgment is carried from `fe854c9`.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | `chunks/1?around=1` -> 200, ids, fileName, chunkCount 4, [0,1,2] with stored content; same without `around` | `DocumentChunksTests.Returns_chunk_with_neighbors` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentChunksTests.cs:47` `Assert.Equal(HttpStatusCode.OK, ...)`; `:49-51` documentId, `"rh.txt"`, `4`; `:52` `Assert.Equal([0, 1, 2], Indexes(body))`; `:54` `Assert.Equal(stored[index], content)` (carried from `fe854c9`) | PASS |
| C2 | 0 -> [0,1]; 3 -> [2,3]; around=0 -> [2]; around=2 on 1 -> [0..3] | `DocumentChunksTests.Edges_return_only_existing_neighbors` (4 cases) Passed | `DocumentChunksTests.cs:60-63` InlineData; `:70` `Assert.Equal(expected, Indexes(body))` (carried) | PASS |
| C3 | around -1 and 3 -> 400 with `errors.around` | `DocumentChunksTests.Around_out_of_range_returns_400(-1/3)` Passed | `DocumentChunksTests.cs:83` `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:84` `TryGetProperty("around", ...)` (carried) | PASS |
| C4 | foreign user, missing org, doc of other org, index = chunkCount, index -1 -> 404 problem | `DocumentChunksTests.Foreign_or_missing_returns_404` (5 scenarios) Passed | `DocumentChunksTests.cs:110` `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:111` `Assert.Equal("application/problem+json", ...)` (carried) | PASS |
| C5 | chunks route without cookie -> 401 | `AuthTests.Protected_route_without_session_returns_401` (chunks row) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:108` row; `:131` `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` (carried) | PASS |
| C6 | renamed routing tests pass against `/api/route/ask` and `/api/organizations/{id}/route/ask` | `RoutingTests`, `OrganizationRoutingTests`, `RoutingChoiceTests`, every case Passed | `tests/BuildYourOwnAI.Api.Tests/Features/RoutingTests.cs:61-72`, `:153-154`; `OrganizationRoutingTests.cs:80-83` (carried) | PASS |
| C7 | old `jev` routes -> 404 problem | `RoutingTests.Old_jev_routes_are_gone` Passed | `RoutingTests.cs:255` `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:256` `Assert.Equal("application/problem+json", ...)` (carried) | PASS |
| C8 | `AI:Routing:ConfidenceThreshold=0.95` + 0.9 -> clarify; default -> answered | `RoutingChoiceTests.Threshold_comes_from_routing_config(null/"0.95")` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/RoutingChoiceTests.cs:109-110` InlineData; `:113` `UseSetting("AI:Routing:ConfidenceThreshold", threshold)`; `:130` `Assert.Equal(kind, body.GetProperty("kind").GetString())` (citations refreshed at `2d13fc7`) | PASS |
| C9 | "Escolha automática", "escolha automática", "Escolhendo quem responde…", "Todas as IAs" -> `/all` | `automatic choice wording` ✓ | `src/web/src/features/chat/sourcePreview.test.tsx:179` radio checked; `:181` `toHaveAttribute('href', '/all')`; `:183` `findByText('Escolhendo quem responde…')`; `:186` `getByText('escolha automática')` (carried) | PASS |
| C10 | no "jev" in src/docs/README/AGENTS/.claude except `jev-latest` | shell command exit 0 | output above: only `jev-latest` lines (`README.md:24`, `docs/PRD.md:49`) (re-run at `2d13fc7`) | PASS |
| C11 | 5 sources of 2 docs -> each file name once, 3 + 2 chunk buttons, first chunk opens unclicked | `sources are grouped by document and the first chunk opens` ✓ | `sourcePreview.test.tsx:69-70` `toHaveLength(1)`; `:73-74` button lists; `:75` `expect(requested).toEqual(['d1/2'])`; `:76` `Trecho 3 de 8` (carried) | PASS |
| C12 | click "trecho 5" -> `d2/chunks/4`, panel file, "Trecho 5 de 8", chunk 4 `aria-current`, 3 and 5 unmarked | `preview shows the chunk with its neighbors` ✓ | `sourcePreview.test.tsx:89` `toContain('d2/4')`; `:91-92`; `:93` `toHaveAttribute('aria-current', 'true')`; `:94-95` `.not.toHaveAttribute('aria-current')` (carried) | PASS |
| C13 | found=false -> no "Fontes", no chunk buttons, no preview, no chunk request | `not found answer shows no sources and no preview` ✓ | `sourcePreview.test.tsx:108-110` absent; `:111` `expect(requested).toEqual([])` (carried) | PASS |
| C14 | "Fechar prévia" removes the panel; clicking a chunk reopens it | `preview closes and reopens` ✓ | `sourcePreview.test.tsx:123` `.not.toBeInTheDocument()`; `:126` `Trecho 1 de 8` (carried) | PASS |
| C15 | "Carregando trecho…" while pending; 404 shows the `title` | `preview loading and error states` ✓ | `sourcePreview.test.tsx:145` `findByText('Carregando trecho…')`; `:150` `toHaveTextContent('Trecho não encontrado.')` (carried) | PASS |
| C16 | complementary "Prévia do trecho" with `fixed`, `inset-0`, `lg:sticky`; close button always rendered | `preview overlays on narrow screens` ✓ | `sourcePreview.test.tsx:162` class loop over `['fixed','inset-0','lg:sticky']`; `:163` `Fechar prévia`. Class string now at `src/web/src/features/chat/SourcePreview.tsx:25`, unchanged by the fix (judgment carried from `fe854c9`) | PASS |
| C17 | with a found answer and the preview open: shell has `overflow-x-clip`; `scrollIntoView` never called (thread or preview); `window.scrollTo` gets only `top` | `chat never scrolls sideways` ✓ | `sourcePreview.test.tsx:196` `api()` (found answer, default); `:202` preview open and showing the cited chunk (`findByText('conteúdo do trecho 2')`); `:204` `toContain('overflow-x-clip')`; `:205` `expect(scrollIntoView).not.toHaveBeenCalled()`; `:206` `expect(scrollTo).toHaveBeenCalled()`; `:207` `Object.keys(options)).toEqual(['top'])`. The code now centres by scrolling the panel only: `SourcePreview.tsx:17-20` `panel.current.scrollTop = cited.current.offsetTop - panel.current.clientHeight / 2`. Re-injecting `scrollIntoView` is killed (below). Verified at `2d13fc7` | PASS |
| C18 | existing web tests (chat, organizations, gaps, home, styles, auth) pass with the new wording | the filter's 8 alternatives, each matching a passed test | `chat.test.tsx` (`chat sends to organization routing...`, `asking an alternative appends...`, `pinned assistant is asked directly`) ✓; `src/web/src/features/organizations/organizations.test.tsx:71` `organization page lists documents and assistants` ✓; `src/web/src/features/auth/auth.test.tsx:50,67` `login form states: ...` (2) ✓; `gaps.test.tsx` `nav shows open gap count (0)/(1)` ✓; `home.test.tsx` `home explains how it works in three steps` ✓; `styles.test.ts` `styles keep focus visible...` ✓. Wording assertion e.g. `src/web/src/features/chat/chat.test.tsx:147` `getByText('escolha automática')`. The round-1 precision note is closed: the alternation now reaches organizations and auth. Verified at `2d13fc7` | PASS |
| C19 | no `AI:Routing:TimeoutSeconds` -> routing client timeout 10 s; `3` -> 3 s; base address `https://openrouter.ai/api/v1/` | `RoutingChoiceTests.Timeout_comes_from_routing_config(null,10)` and `("3",3)` Passed | `RoutingChoiceTests.cs:135-136` `[InlineData(null, 10)]`, `[InlineData("3", 3)]`; `:142` `services.AddAi(...)` with in-memory config; `:145` `CreateClient(nameof(OpenRouterRoutingChoice))`; `:147` `Assert.Equal(TimeSpan.FromSeconds(expected), client.Timeout)`; `:148` `Assert.Equal("https://openrouter.ai/api/v1/", client.BaseAddress!.ToString())`. Hardcoding the timeout is killed (below). Verified at `2d13fc7` | PASS |

## Coverage

The rows the fix touched were recomputed at `2d13fc7`: `AI:Routing` keys, startup config `AI:Routing`, and the AC 17 found-answer and sideways-scroll sources. The other rows are carried from `fe854c9`.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `AI:Routing:*` keys (2, AC 13), verified at `2d13fc7` | plan AC 13 (`plan.md:95`); `src/BuildYourOwnAI.Api/Infrastructure/Ai/AiServiceCollectionExtensions.cs:33` (`TimeoutSeconds = 10`), `:46`, `:56`; `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs:67` | `ConfidenceThreshold` C8 · `TimeoutSeconds` C19 (default 10 and configured 3) | - |
| startup config `AI:Routing` (2 assemblies), verified at `2d13fc7` | read directly: `src/BuildYourOwnAI.Api/Program.cs:55` `builder.Services.AddAi(builder.Configuration)` -> `AiServiceCollectionExtensions.cs:46,56`; `ApiFactory` keeps the defaults | Program/AddAi: C8 (through the host), C19 (through `AddAi`, the same extension `Program.cs:55` calls) · ApiFactory default C6 | - |
| AC 17 "thread with answers" (2: found=false, found=true), verified at `2d13fc7` | plan AC 17 (`plan.md:108`) | found=true C17 (`sourcePreview.test.tsx:196,202`) · found=false: carried from round 1 (C13 shows no preview; the Thread path is the same `Thread.tsx:64`) | - |
| sideways-scroll sources in the web app (3), verified at `2d13fc7` | search of `src/web/src` (non-test) for `scrollIntoView`, `scrollTo`, `scrollTop`, `overflow-x`: `src/web/src/features/chat/Thread.tsx:64` `window.scrollTo?.({ top: ... })`; `SourcePreview.tsx:19` panel `scrollTop`; `src/web/src/shared/AppLayout.tsx:23` `overflow-x-clip` | Thread scroll C17 (`:207` only `top`) · preview scroll C17 (`:205` no `scrollIntoView`, with the preview open at `:202`) · shell clip C17 (`:204`) | - |
| chunks route statuses (4), carried | plan Surface; `GetDocumentChunks.cs:22-46` | 200 C1 · 400 C3 · 401 C5 · 404 C4 | - |
| neighbors (4), carried | `GetDocumentChunks.cs:41` | middle C1 · first C2 · last C2 · around 0/2 C2 | - |
| chunk 404 (5), carried | `GetDocumentChunks.cs:29-38` | all five C4 | - |
| `around` edges (4), carried | `GetDocumentChunks.cs:22` | -1 C3 · 0 C2 · 2 C2 · 3 C3 | - |
| preview panel states (6), carried | `SourcePreview.tsx`, `ChatPage.tsx` | auto-open C11 · click C12 · loading C15 · error C15 · closed C14 · narrow C16 | - |
| sources by `found` (2), carried | `Thread.tsx:73,287` | true C11 · false C13 | - |
| renamed routes (4) + Surface statuses, carried | plan Surface; `RouteAsk.cs:45-62` | `/api/route/ask` and `/api/organizations/{id}/route/ask` C6 · old routes 404 C7 | - |
| automatic-choice wording (4), carried | AC 14 | C9 | - |
| places without "Jev" (5), carried (proof re-run) | AC 15/16 | C10 | - |
| doors (2), carried | plan Landing | 1: C1, C4 · 2: C6, C7, C10 | - |

## Test policy rows

Carried from `fe854c9`. `checks.md` adds no new rows. The inherited provider-client row (jev-choice) still holds: `RoutingChoice.cs` is unchanged, and C19 adds a proof at the level of its `Configure`. The fix touched no handler.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| (none new in this checks.md) | - | - | yes - inherited provider-client row met by `OpenRouterRoutingChoiceTests` (`RoutingChoiceTests.cs:152-217`) |

Swept `existing` (dependency failure, routing fallback) is carried from `fe854c9`: `RouteAsk.cs` is unchanged.

## Faults injected

Verified at `2d13fc7`. Isolated in `git worktree add --detach <scratchpad>/wt HEAD`, with `src/web/node_modules` junctioned from the real tree. The junction was removed first (`rmdir`), then the worktree (removed and pruned). The real tree's `git status --porcelain` was empty before and after, and the real `node_modules` is intact. These are the surfaces the fix touched or created. The round-1 faults (around ignored, organization check dropped, sources on found=false, grouping disabled, Thread `scrollIntoView`) are carried from `fe854c9`.

| Mutation | Location | Killed |
| --- | --- | --- |
| re-introduce `cited.current.scrollIntoView?.({ block: 'center', inline: 'nearest' })` in place of the panel `scrollTop` | `src/web/src/features/chat/SourcePreview.tsx:19` | yes - `chat never scrolls sideways`: `expected "vi.fn()" to not be called at all, but actually been called 1 times` |
| hardcode the routing timeout (`client.Timeout = TimeSpan.FromSeconds(10)`) | `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs:67` | yes - `Timeout_comes_from_routing_config("3", 3)`: `Values differ, Expected: 00:00:03, Actual: 00:00:10` at `RoutingChoiceTests.cs:147` |

The same worktree also ran three jev-choice faults (see that report). Five in total, the cap.

## Gate

`dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` - 191 passed, 0 failed; `npm --prefix src/web run test -- --reporter=verbose` - 63 passed, 0 failed; C10 command exit 0 (all at 2d13fc7).

## Ranked gaps

None. Round-1 gap 1 (AC 17 found answer) is closed by `SourcePreview.tsx:17-20` and `sourcePreview.test.tsx:196,202,205`, and the fault was killed. Gap 2 (`TimeoutSeconds`) is closed by C19 (`RoutingChoiceTests.cs:147`), and the fault was killed. Precision gap 3 (the C18 selector) is closed: the alternation matches `organizations.test.tsx:71` and `auth.test.tsx:50,67`.
