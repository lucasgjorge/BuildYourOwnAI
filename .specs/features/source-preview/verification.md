# Prévia do trecho e escolha automática verification

**Verdict**: FAIL
**Profile**: standard
**Diff range**: 427a40c..fe854c9
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Verified at `fe854c9`. The real tree was not touched.

## Binding sources

No binding sources: the plan marks none (`Sources` lists the 2026-09-24 conversation and a screenshot, neither marked binding). Step 1 did not apply.

## Checks

Proof runs, one invocation per target at HEAD:

- API (PowerShell, `DOCKER_HOST=npipe://./pipe/dockerDesktopLinuxEngine`): `dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"`. Exit 0, 189 passed, 0 failed. Each named API test below appears individually as `Passed`.
- Web: `npm --prefix src/web run test -- --reporter=verbose`. Exit 0, 8 files, 63 passed. Each named web test below appears individually with `✓`.
- C10 (Git Bash, repo root, exactly as written in checks.md): exit 0. Unfiltered, the grep matches only `jev-latest` lines: `README.md:24,51,174` and `docs/PRD.md:49,112`.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | `chunks/1?around=1` -> 200, ids, fileName, chunkCount 4, [0,1,2] with stored content; same without `around` | `DocumentChunksTests.Returns_chunk_with_neighbors` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DocumentChunksTests.cs:43` loops `"?around=1"` and `""`; `:47` `Assert.Equal(HttpStatusCode.OK, ...)`; `:49-51` documentId, `"rh.txt"`, `4`; `:52` `Assert.Equal([0, 1, 2], Indexes(body))`; `:54` `Assert.Equal(stored[index], content)` | PASS |
| C2 | 0 -> [0,1]; 3 -> [2,3]; around=0 -> [2]; around=2 on 1 -> [0..3] | `DocumentChunksTests.Edges_return_only_existing_neighbors` (4 cases) Passed | `DocumentChunksTests.cs:60-63` InlineData; `:70` `Assert.Equal(expected, Indexes(body))` | PASS |
| C3 | around -1 and 3 -> 400 with `errors.around` | `DocumentChunksTests.Around_out_of_range_returns_400(-1/3)` Passed | `DocumentChunksTests.cs:83` `Assert.Equal(HttpStatusCode.BadRequest, ...)`; `:84` `TryGetProperty("around", ...)` | PASS |
| C4 | foreign user, missing org, doc of other org, index = chunkCount, index -1 -> 404 problem | `DocumentChunksTests.Foreign_or_missing_returns_404` (5 scenarios) Passed | `DocumentChunksTests.cs:89-93` scenarios; `:110` `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:111` `Assert.Equal("application/problem+json", ...)` | PASS |
| C5 | chunks route without cookie -> 401 | `AuthTests.Protected_route_without_session_returns_401` (row `GET .../documents/{doc}/chunks/0`) Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:108` row `{ "GET", $"/api/organizations/{id}/documents/{doc}/chunks/0" }`; `:131` `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C6 | renamed routing tests pass against `/api/route/ask` and `/api/organizations/{id}/route/ask` | `RoutingTests`, `OrganizationRoutingTests`, `RoutingChoiceTests` (and `OpenRouterRoutingChoiceTests`), every case Passed | e.g. `tests/BuildYourOwnAI.Api.Tests/Features/RoutingTests.cs:61-72` (answered through the chosen assistant), `:153-154` (fallback), `:189-191` (400), `:207` (429); `OrganizationRoutingTests.cs:80-83` (noMatch in org). Assertion diff from `427a40c`: unchanged apart from the helper renames (`JevAsync` -> `RouteAsync`) | PASS |
| C7 | old `jev` routes -> 404 problem | `RoutingTests.Old_jev_routes_are_gone` Passed | `RoutingTests.cs:252` both paths; `:255` `Assert.Equal(HttpStatusCode.NotFound, ...)`; `:256` `Assert.Equal("application/problem+json", ...)` | PASS |
| C8 | `AI:Routing:ConfidenceThreshold=0.95` + 0.9 -> clarify; default -> answered | `RoutingChoiceTests.Threshold_comes_from_routing_config(null/"0.95")` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/RoutingChoiceTests.cs:106-107` InlineData; `:110` `UseSetting("AI:Routing:ConfidenceThreshold", threshold)`; `:127` `Assert.Equal(kind, ...kind)` | PASS |
| C9 | "Escolha automática", "escolha automática", "Escolhendo quem responde…", "Todas as IAs" -> `/all` | `automatic choice wording` ✓ | `src/web/src/features/chat/sourcePreview.test.tsx:179` radio `Escolha automática` checked; `:181` link `Todas as IAs` `toHaveAttribute('href', '/all')`; `:183` `findByText('Escolhendo quem responde…')`; `:186` `getByText('escolha automática')` | PASS |
| C10 | no "jev" in src/docs/README/AGENTS/.claude except `jev-latest` | shell command exit 0 | output above: the only matches are `jev-latest` lines (`README.md:24`, `docs/PRD.md:49`). `README.md:24` and `docs/PRD.md:49,112` name the model id in prose, not in configuration. Read as "the provider's model id", which the AC 16 independent test allows | PASS |
| C11 | 5 sources of 2 docs -> each file name once, 3 + 2 chunk buttons, first chunk opens unclicked | `sources are grouped by document and the first chunk opens` ✓ | `sourcePreview.test.tsx:69-70` `getAllByText('01_rh.txt'/'02_cultura.txt')).toHaveLength(1)`; `:73-74` buttons `['trecho 3','trecho 1','trecho 6']` / `['trecho 5','trecho 2']`; `:75` `expect(requested).toEqual(['d1/2'])`; `:76` `Trecho 3 de 8` | PASS |
| C12 | click "trecho 5" -> `d2/chunks/4`, panel shows file, "Trecho 5 de 8", chunk 4 `aria-current`, 3 and 5 unmarked | `preview shows the chunk with its neighbors` ✓ | `sourcePreview.test.tsx:89` `requested).toContain('d2/4')`; `:91` `Trecho 5 de 8`; `:92` heading `02_cultura.txt`; `:93` `toHaveAttribute('aria-current', 'true')`; `:94-95` `.not.toHaveAttribute('aria-current')` | PASS |
| C13 | found=false -> no "Fontes", no chunk buttons, no preview, no chunk request | `not found answer shows no sources and no preview` ✓ | `sourcePreview.test.tsx:108` `queryByText('Fontes')).not.toBeInTheDocument()`; `:109` no `/trecho/` button; `:110` no complementary; `:111` `expect(requested).toEqual([])` | PASS |
| C14 | "Fechar prévia" removes the panel; clicking a chunk reopens it | `preview closes and reopens` ✓ | `sourcePreview.test.tsx:123` `queryByRole('complementary', ...)).not.toBeInTheDocument()`; `:126` `Trecho 1 de 8` after reclick | PASS |
| C15 | "Carregando trecho…" while pending; 404 shows the `title` | `preview loading and error states` ✓ | `sourcePreview.test.tsx:145` `findByText('Carregando trecho…')`; `:150` `findByRole('alert')).toHaveTextContent('Trecho não encontrado.')` | PASS |
| C16 | complementary "Prévia do trecho" with `fixed`, `inset-0`, `lg:sticky`; close button always rendered | `preview overlays on narrow screens` ✓ | `sourcePreview.test.tsx:162` `for (cls of ['fixed','inset-0','lg:sticky']) expect(panel.className.split(/\s+/)).toContain(cls)`; `:163` `getByRole('button', { name: 'Fechar prévia' })` | PASS |
| C17 | shell has `overflow-x-clip`; no `scrollIntoView`; `scrollTo` gets only `top` | `chat never scrolls sideways` ✓ | `sourcePreview.test.tsx:202` `toContain('overflow-x-clip')`; `:203` `expect(scrollIntoView).not.toHaveBeenCalled()`; `:205` `Object.keys(options)).toEqual(['top'])`. Green only on the not-found answer (`:195` `api({ found: false })`), see Coverage | PASS |
| C18 | existing web tests pass with the new wording | the 6 names in the filter ✓ (`chat.test.tsx:133,154,182`, `gaps.test.tsx:48`, `home.test.tsx:8`, `styles.test.ts:11`) | e.g. `src/web/src/features/chat/chat.test.tsx:147` `getByText('escolha automática')`. Precision note: the claim names organizations and auth, but its filter matches no organizations or auth test. They ran and passed in the full invocation (`organizations.test.tsx`, `auth.test.tsx` all ✓) | PASS |

**C16 correction (`lg:static` -> `lg:sticky`)** kept the obligation and did not weaken it. At `lg`, the obligation is "no longer an overlay; a column beside the chat". Either class overrides `position: fixed` at `lg`, so either one, as a class token, proves the panel is not fixed at `lg`. `lg:sticky` together with `lg:inset-auto lg:top-0 lg:h-screen lg:w-[28rem]` (`src/web/src/features/chat/SourcePreview.tsx:20`) also keeps the column in view while the page scrolls, and `lg:static` would not. No proof covers the column arrangement itself, before or after the correction: jsdom does no layout, so this is only a class-level proof. The correction did not change that.

## Coverage

Recomputed from the code (`GetDocumentChunks.cs`, `RouteAsk.cs`, `AiServiceCollectionExtensions.cs`, `RoutingChoice.cs`, `Thread.tsx`, `SourcePreview.tsx`, `AppLayout.tsx`) and from the plan's own enumerations (Surface, Landing, AC 13, AC 17).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| chunks route statuses (4) | plan Surface; `src/BuildYourOwnAI.Api/Features/Documents/GetDocumentChunks.cs:22-46` | 200 C1 · 400 C3 · 401 C5 · 404 C4 | - |
| neighbors (4) | `GetDocumentChunks.cs:41` | middle C1 · first C2 · last C2 · around 0/2 C2 | - |
| chunk 404 (5) | `GetDocumentChunks.cs:29-38` | other user, missing org, doc of other org (`:32` `d.OrganizationId == id`), index = chunkCount, index -1: all C4 | - |
| `around` edges (4) | `GetDocumentChunks.cs:22` | -1 C3 · 0 C2 · 2 C2 · 3 C3 | - |
| preview panel states (6) | `SourcePreview.tsx`, `ChatPage.tsx` | auto-open C11 · click C12 · loading C15 · error C15 · closed C14 · narrow C16 | - |
| sources by `found` (2) | `src/web/src/features/chat/Thread.tsx:73,287` | true C11 · false C13 | - |
| renamed routes (4) + their Surface statuses | plan Surface; `RouteAsk.cs:45-62` | `/api/route/ask` 200/400/401/422/429/502 (`RoutingTests.cs:61,189,170,207,239`, `AuthTests.cs:112`) · `/api/organizations/{id}/route/ask` 200/400/401/404/422/429/502 (`OrganizationRoutingTests`, `AuthTests.cs:113`) · old routes 404 C7 | - |
| automatic-choice wording (4) | AC 14 | selector, answer badge, pending, sidebar: C9 | - |
| places without "Jev" (5) | AC 15/16 | `src/`, `docs/`, `README.md`, `AGENTS.md`, `.claude/`: C10 | - |
| doors (2) | plan Landing | 1: C1, C4 · 2: C6, C7, C10 | - |
| `AI:Routing:*` keys read from config (2, AC 13) | AC 13; `src/BuildYourOwnAI.Api/Infrastructure/Ai/AiServiceCollectionExtensions.cs:29-34,56`; `RoutingChoice.cs:67` | `ConfidenceThreshold` C8 | `TimeoutSeconds`: no test sets or reads it. `rg TimeoutSeconds\|Timeout tests/` finds nothing; `OpenRouterRoutingChoiceTests` passes `new AiOptions.RoutingSection()` (`RoutingChoiceTests.cs:153`) and never asserts `client.Timeout`. Reading `AI:Jev:TimeoutSeconds` or ignoring the key would pass |
| startup config `AI:Routing` (2 assemblies) | read directly: `src/BuildYourOwnAI.Api/Program.cs:55` `AddAi(builder.Configuration)` -> `AiServiceCollectionExtensions.cs:46` `services.Configure<AiOptions>(configuration.GetSection("AI"))`; `ApiFactory` keeps the default | Program/AddAi C8 · ApiFactory default C6 | - |
| AC 17 "thread with answers" (2: found=false, found=true) | AC 17; `Thread.tsx:64`, `SourcePreview.tsx:15` | found=false C17 | found=true, the common case and the one in the screenshot, where the preview opens by itself. `SourcePreview.tsx:15` calls `cited.current?.scrollIntoView?.({ block: 'center', inline: 'nearest' })`. Probe in the scratch worktree: C17 with `api()` (found=true) instead of `api({ found: false })` fails with `expected "vi.fn()" to not be called at all, but actually been called 1 times`. C17's literal claim ("scrollIntoView não é chamado") is false for a found answer, and the proof avoids that path |

## Test policy rows

`checks.md` adds no new rows ("Os specs anteriores já respondem. Sem linhas novas"). The inherited row that applies to files this diff touches is jev-choice's provider HTTP client row, now covering `OpenRouterRoutingChoice`. That row is still met at HEAD: the rename changed no assertion (see jev-choice/verification.md). Rows from earlier specs for handlers (proof at the HTTP boundary) are met by C1-C8, all at the HTTP boundary.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| (none new in this checks.md) | - | - | yes - no new rows; the inherited provider-client row is met by `OpenRouterRoutingChoiceTests` (`RoutingChoiceTests.cs:161-196`) |

Swept `existing` re-read: dependency failure, the routing fallback. Still at `src/BuildYourOwnAI.Api/Features/Routing/RouteAsk.cs:149-153` (catch -> null -> fallback clarify at `:101-102`). Present.

Scope note, not a finding: the Handoff limits the preview to the organization chat. On "Todas as IAs", sources are grouped with no buttons (`Thread.tsx:296-309`). The plan's Observable table scopes the preview to "screen `Conversa`", so this does not contradict the plan.

## Faults injected

Isolated in `git worktree add --detach <scratchpad>/wt2 HEAD`, with `src/web/node_modules` junctioned from the real tree. The junction was removed first (`rmdir`), then the worktree. The real tree's `git status --porcelain` was empty before and after, and the real `node_modules` is intact.

| Mutation | Location | Killed |
| --- | --- | --- |
| ignore `around` (neighbor window fixed at ±1) | `src/BuildYourOwnAI.Api/Features/Documents/GetDocumentChunks.cs:41` | yes - `Edges_return_only_existing_neighbors(2,"?around=0")` and `(1,"?around=2")`, `Collections differ` |
| drop the organization check on the document (`&& d.OrganizationId == id` removed) | `GetDocumentChunks.cs:32` | yes - `Foreign_or_missing_returns_404("other-organization")`, `Values differ` |
| show sources on found=false (`answer.found &&` removed) | `src/web/src/features/chat/Thread.tsx:287` | yes - `not found answer shows no sources and no preview`: `expected document not to contain element, found <h3` |
| grouping disabled (every source its own group) | `Thread.tsx:335` | yes - `sources are grouped by document and the first chunk opens`: `to have a length of 1 but got 3` |
| re-introduce `scrollIntoView` in the Thread's scroll effect | `Thread.tsx:64` | yes - `chat never scrolls sideways`: `expected "vi.fn()" to not be called at all, but actually been called 3 times` |

Not injected (cap of 5 reached): preview auto-open (C11, the same proof as grouping), C9, C12, C14-C16. The C17 found=true probe above was a test-scenario probe, not a code fault.

## Gate

`dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` - 189 passed, 0 failed; `npm --prefix src/web run test -- --reporter=verbose` - 63 passed, 0 failed; C10 command exit 0 (all at fe854c9).

## Ranked gaps

1. AC 17 is unproven for a found answer, and C17's claim is false there. With a found answer, `src/web/src/features/chat/SourcePreview.tsx:15` calls `scrollIntoView({ block: 'center', inline: 'nearest' })`. C17's proof uses only a not-found answer (`src/web/src/features/chat/sourcePreview.test.tsx:195`), and switching it to found=true makes it fail. Either scroll the preview's own container (`scrollTop`) instead of `scrollIntoView`, or narrow C17's claim to the Thread and add a found=true case that asserts no horizontal page scroll.
2. AC 13 `AI:Routing:TimeoutSeconds` has no proof. It is used at `src/BuildYourOwnAI.Api/Infrastructure/Ai/RoutingChoice.cs:67`, but no test sets it or asserts `client.Timeout` (`RoutingChoiceTests.cs:151-153` uses the default section). Add an assertion that the configured value reaches the typed client, or that `Configure` sets `Timeout` from `RoutingSection`.
3. Precision (not failing): C18's claim lists organizations and auth tests, but its `-t` filter matches neither. The full-suite run covers them.
