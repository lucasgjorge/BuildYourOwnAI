# Nome completo do usuário verification

**Verdict**: PASS
**Profile**: standard
**Diff range**: 273dd9c..6912d8e
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Proofs ran at `6912d8e` (both suites finished at 15:21, before the unrelated commits `7e6c269`,
`15bbdf0` and `c3e6833` landed on `main` from another session; `git diff --name-only 6912d8e..HEAD`
touches no file of this feature).

## Binding sources

| Source | Opened | Contradiction | Uncovered |
| --- | --- | --- | --- |
| user request 2026-09-24 (quoted in `plan.md` Sources; no design file) | yes - quoted text | none | - |

The request fixes two things: save the complete name at user creation (C1-C4, C9) and show the
logged user at the top of the screen (C18, C19). There is no design source, so arrangement is
bounded by the plan's Assumption (bar at the top of the content column, name right-aligned),
which C18 reaches through the `banner` named "Usuário logado".

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | register with name -> 200, `full_name` saved, login 200 | API suite, `AuthTests.Register_with_full_name_saves_it_and_enables_login` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:193` - `Assert.Equal(HttpStatusCode.OK, register.StatusCode)`; `:194` - `Assert.Equal("Maria da Silva", await FullNameAsync(email))` (reads `full_name`, `:171`); `:196` - `Assert.Equal(HttpStatusCode.OK, login.StatusCode)` | PASS |
| C2 | trimmed name is stored | `AuthTests.Register_trims_full_name` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:208` - `Assert.Equal("Maria da Silva", await FullNameAsync(email))` after posting `"  Maria da Silva  "` (`:206`) | PASS |
| C3 | absent / `""` / `"   "` -> 400 problem `errors.fullName`, no row | `AuthTests.Register_without_full_name_returns_400` Passed x3 (null, "", "   ") | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:224` - `AssertValidationProblemAsync(register, "fullName")` (`:179-181`: 400, `application/problem+json`, `errors.<key>`); `:225` - `Assert.Equal(0, await UsersWithEmailAsync(email))` | PASS |
| C4 | 101 -> 400 `errors.fullName`, no row; 100 -> 200, stored | `AuthTests.Register_full_name_length_bound` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:239-240` - `AssertValidationProblemAsync(rejected, "fullName")`, `Assert.Equal(0, await UsersWithEmailAsync(tooLong))`; `:241-242` - `Assert.Equal(HttpStatusCode.OK, accepted.StatusCode)`, `Assert.Equal(new string('b', 100), await FullNameAsync(longest))` | PASS |
| C5 | duplicate e-mail -> 400 problem `errors.DuplicateUserName` | `AuthTests.Register_duplicate_email_returns_400_DuplicateUserName` Passed (touched in diff: sends `fullName`) | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:43-45` - `Assert.Equal(HttpStatusCode.BadRequest, ...)`, `Assert.Equal("application/problem+json", ...)`, `TryGetProperty("DuplicateUserName", out _)` | PASS |
| C6 | password `"abc"` -> 400 `errors.PasswordTooShort`, no row | `AuthTests.Register_weak_password_returns_400_with_identity_code` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:254-255` - `AssertValidationProblemAsync(register, "PasswordTooShort")`; `Assert.Equal(0, await UsersWithEmailAsync(email))` | PASS |
| C7 | `"nao-e-email"` -> 400 `errors.InvalidEmail`, no row | `AuthTests.Register_invalid_email_returns_400_InvalidEmail` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:266-267` - `AssertValidationProblemAsync(register, "InvalidEmail")`; `Assert.Equal(0, await UsersWithEmailAsync("nao-e-email"))` | PASS |
| C8 | name never in log messages/properties | `ObservabilityTests.Register_never_logs_full_name` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/CrossCuttingTests.cs:160` - `Assert.DoesNotContain(logged, text => text.Contains(fullName))` over messages and property values (`:157-159`), success and duplicate paths | PASS |
| C9 | `AspNetUsers.full_name` nullable `character varying(100)` | `MigrationTests.Schema_has_user_full_name` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/MigrationTests.cs:102-105` - `Assert.Equal(1, ... is_nullable = 'YES' and data_type = 'character varying' and character_maximum_length = 100)` | PASS |
| C10 | `/me` logged -> 200, `email`, `fullName` "Maria da Silva" | `AuthTests.Me_returns_email_and_full_name` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:281` - `Assert.Equal(HttpStatusCode.OK, me.StatusCode)`; `:283-284` - `Assert.Equal(email, ...GetProperty("email"))`, `Assert.Equal("Maria da Silva", body.GetProperty("fullName").GetString())` | PASS |
| C11 | `/me` without cookie -> 401 | `AuthTests.Protected_route_without_session_returns_401(method: "GET", path: "/api/auth/me")` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:121` - row `{ "GET", "/api/auth/me" }` (added in diff); `:139` - `Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)` | PASS |
| C12 | account without name -> `/me` 200, `fullName` null | `AuthTests.Me_returns_null_full_name_for_account_without_name` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/AuthTests.cs:302` - `Assert.Equal(HttpStatusCode.OK, me.StatusCode)`; `:305` - `Assert.Equal(JsonValueKind.Null, body.GetProperty("fullName").ValueKind)` | PASS |
| C13 | seeded account `full_name` "Administrador" | `DevAdminSeedTests.Seeded_account_is_named_Administrador` Passed | `tests/BuildYourOwnAI.Api.Tests/Features/DevAdminSeedTests.cs:57-58` - `Assert.Equal(1, ... full_name = 'Administrador')` | PASS |
| C14 | API suite passes with helpers sending `fullName` | `dotnet test tests/BuildYourOwnAI.Api.Tests` exit 0 - 250 passed, 0 failed | `tests/BuildYourOwnAI.Api.Tests/Infrastructure/ApiTestBase.cs:26` - helper posts `fullName = "Usuário de Teste"` then `EnsureSuccessStatusCode()`; same in `RoutingChoiceTests.cs:120`, `UnconfiguredAiTests.cs:27`, `:46` | PASS |
| C15 | `/register` first field "Nome completo", required, maxLength 100, then "E-mail" | vitest `full name > register form asks the full name first` passed | `src/web/src/features/auth/auth.test.tsx:133-134` - `expect(name).toBeRequired()`, `toHaveAttribute('maxLength', '100')`; `:136-137` - `expect(fields[0]).toBe(name)`, `expect(fields[1]).toBe(screen.getByLabelText('E-mail'))` | PASS |
| C16 | register posts `{ fullName, email, password }` | vitest `full name > register sends the full name` passed | `src/web/src/features/auth/auth.test.tsx:151` - `expect(bodies).toEqual([{ fullName: 'Maria da Silva', email: 'ana@test.local', password: 'Passw0rd!' }])` | PASS |
| C17 | 400 problem shows title, keeps name and e-mail | vitest `register form states: disabled while pending, then problem title keeps the name and e-mail` passed | `src/web/src/features/auth/auth.test.tsx:101-103` - `toHaveTextContent('E-mail já cadastrado')`, `getByLabelText('Nome completo')).toHaveValue('Maria da Silva')`, `getByLabelText('E-mail')).toHaveValue('ana@test.local')` | PASS |
| C18 | banner shows "Ana Souza" on `/organizations` and `/gaps` | vitest `top bar shows the logged user name (/organizations)` and `(/gaps)` passed | `src/web/src/features/auth/auth.test.tsx:168` - `expect(await screen.findByRole('banner', { name: 'Usuário logado' })).toHaveTextContent('Ana Souza')` | PASS |
| C19 | `fullName: null` -> banner shows e-mail | vitest `top bar falls back to the e-mail` passed | `src/web/src/features/auth/auth.test.tsx:176` - `...findByRole('banner', { name: 'Usuário logado' })).toHaveTextContent('ana@test.local')` | PASS |
| C20 | `/login` has no "Nome completo" | vitest `login form has no full name` passed | `src/web/src/features/auth/auth.test.tsx:160` - `expect(screen.queryByLabelText('Nome completo')).not.toBeInTheDocument()` | PASS |
| C21 | web suite passes with session from `/api/auth/me` | `npx vitest run --reporter=verbose` exit 0 - 78 passed, 0 failed | `src/web/src/test/server.ts:14` - `loggedIn` handler on `*/api/auth/me`; session-guard tests in `src/web/src/features/organizations/organizations.test.tsx:248` moved to `*/api/auth/me` and passed | PASS |

## Coverage

Recomputed from the plan's Surface/Landing/Criteria and from the code, not from the author's table.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| `POST /api/auth/register` statuses (2) | plan Surface; `Register.cs:21` `Results<Ok, ValidationProblem>` | 200 C1, C4 · 400 C3-C7 | - |
| register 400 branches (4 code paths) | `Register.cs:24`, `:31`, `:35-36` | fullName empty C3 / too long C4 (`:24`) · invalid e-mail C7 (`:31`) · Identity `CreateAsync` failures DuplicateUserName C5, PasswordTooShort C6 (`:36` -> `:39-42`) | - |
| `fullName` empty forms (3) | AC 3 | null C3 · `""` C3 · `"   "` C3 (theory rows `AuthTests.cs:213-215`) | - |
| `fullName` length edges (2) | AC 4, `Register.cs:24` | 100 C4 · 101 C4 | - |
| `GET /api/auth/me` statuses (2) | plan Surface; `GetMe.cs:58` `Results<Ok<Response>, ProblemHttpResult>` | 200 C10, C12 · 401 C11 | - |
| `fullName` in session (2) | AC 9, AC 11 | value C10 · null C12 | - |
| response fields of `/me` (2) | `GetMe.cs:54` `Response(Email, FullName)` | email C10, C12 · fullName C10, C12 | - |
| register request fields (3) | plan Surface In; `api.ts` `useRegister` | fullName, email, password C16 (exact `toEqual`) | - |
| top bar content (2) | `AppLayout.tsx:71` `fullName ?? email` | name C18 · e-mail C19 | - |
| authenticated screens (7 routes, 1 shared layout) | `App.tsx:21-31` - every authenticated route is a child of the single `<AppLayout />` at `App.tsx:22` | layout header proven on `/organizations`, `/gaps` C18; the other 5 routes render inside the same element | - |
| account origins (3) | Impact stored data | register C1 · dev seed C13 · pre-existing without name C12 | - |
| register form fields (web, 2 text fields) | AC 13, `AuthForm.tsx` | Nome completo first, E-mail second C15; login without name C20 | - |
| one-way doors (3) | plan Landing | 1 column C9 · 2 route shadowing C3 (fault F2 killed) · 3 `/me` C10, C11 | - |
| Observable states (RegisterPage error/loading; shell loading/error/unauth) | plan Observable | error C17 · loading C17 (`auth.test.tsx:99`, existing) · shell loading/error `organizations.test.tsx` session guard (migrated, passed) · unauth redirect `auth.test.tsx:9-16` | - |

Level: every claim naming a status, route or response shape (C1, C3-C7, C10-C12) is proven over HTTP
with `WebApplicationFactory` and real Postgres; no level gap.

Swept existing re-read: concurrency cites Identity's unique `UserNameIndex` - present at
`src/BuildYourOwnAI.Api/Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs:106-108`
(`HasIndex("NormalizedUserName").IsUnique().HasDatabaseName("UserNameIndex")`).

Observations (not coverage members of any enumerated set, non-blocking):
- `GetMe.cs:66-67` returns 401 when a valid cookie outlives its account; the 401 status is proven (C11) but this second path to it has no test.
- `Register.cs:31` null/empty e-mail shares the `InvalidEmail` return with the malformed-e-mail case; only the malformed disjunct is exercised (C7).

## Test policy rows

`checks.md` Test policy says prior specs answer it and adds no rows; the repo convention (AGENTS.md:
Minimal API proven over HTTP with real Postgres, screens with Testing Library + MSW, no OpenAI calls)
decides.

| Row | Files it classifies | Required proof | Expectation met |
| --- | --- | --- | --- |
| repo convention - Minimal API route over HTTP, real Postgres | `Features/Auth/Register.cs`, `Features/Auth/GetMe.cs`, `AppDbContext.cs`, migration, `DevAdminSeed.cs` | HTTP C1-C7, C10-C12 · DB C9, C13 | yes |
| repo convention - screen via Testing Library + MSW | `AuthForm.tsx`, `RegisterPage.tsx`, `LoginPage.tsx`, `api.ts`, `AppLayout.tsx` | C15-C20, C21 | yes |

## Faults injected

Scratch `git worktree add --detach <scratchpad>/wt HEAD` at `6912d8e`; `src/web/node_modules` junctioned
from the real tree and removed (junction only) before `git worktree remove --force`. Baseline porcelain
of the real tree: `?? .specs/features/study-mode/verification.md`. After: empty - that file was
committed by another session in `7e6c269` during this run, not by the Verifier; the four mutated files
show no diff against `6912d8e` in the real tree (`git diff --stat 6912d8e -- <files>` empty), and the
worktree list holds only the main tree.

| Mutation | Location | Killed |
| --- | --- | --- |
| F1 length bound `> FullNameMaxLength` -> `> FullNameMaxLength + 1` | `src/BuildYourOwnAI.Api/Features/Auth/Register.cs:24` | yes - `Register_full_name_length_bound` failed (`Actual: InternalServerError`, expected 400) |
| F2 drop `.WithOrder(-1)` (door 2 shadowing) | `src/BuildYourOwnAI.Api/Features/Auth/Register.cs:19` | yes - `Register_without_full_name_returns_400` failed x3 (`AmbiguousMatchException`) |
| F3 `/me` projects `FullName` -> `null` | `src/BuildYourOwnAI.Api/Features/Auth/GetMe.cs:19` | yes - `Me_returns_email_and_full_name` failed (`Expected: "Maria da Silva"`, `Actual: null`) |
| F4 top bar fallback `?? session.data?.email` -> `?? ''` | `src/web/src/shared/AppLayout.tsx:71` | yes - `top bar falls back to the e-mail` failed |
| F5 register payload drops `fullName` | `src/web/src/features/auth/api.ts:41` | yes - `register sends the full name` failed |

Cap of five reached; the trim (`Register.cs:23`, C2) was not mutated - its assertion is an exact
string equality at `AuthTests.cs:208`.

## Gate

`dotnet test tests/BuildYourOwnAI.Api.Tests --logger "console;verbosity=normal"` (DOCKER_HOST set) - 250 passed, 0 failed
`npx vitest run --reporter=verbose` (src/web) - 78 passed, 0 failed
