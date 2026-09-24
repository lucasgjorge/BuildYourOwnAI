# Nome completo do usuário checks

Profile: standard
Plan: `.specs/features/user-name/plan.md`

21 checks in 3 slices · 3 one-way doors · 0 open

Comandos de prova:

- API (PowerShell, com `DOCKER_HOST`, ver AGENTS.md): `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"`
- Web: `npm --prefix src/web run test -- -t "<nome do teste>"`

Checks anteriores afetados pela door 2: todo teste que registra (`ApiTestBase.NewUserClientAsync`, `AuthTests`, `RoutingChoiceTests`, `UnconfiguredAiTests`) passa a enviar `fullName`, com as mesmas asserções. Os handlers de sessão da web (`loggedIn`, `anonymous`, `sessionAfterLogin`, e os de `organizations.test.tsx`) passam de `/api/auth/manage/info` para `/api/auth/me`.

## Checks

### S1 - Cadastro guarda o nome · ~6 files · ~40 KB · ~10k

**C1** - `POST /api/auth/register` com e-mail novo, `Passw0rd!` e `fullName` `"Maria da Silva"` responde `200`; `AspNetUsers.full_name` da conta é `"Maria da Silva"`; o login `?useCookies=true` da conta responde `200` (AC 1, AC 7) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_with_full_name_saves_it_and_enables_login"`

**C2** - `fullName` `"  Maria da Silva  "` é gravado como `"Maria da Silva"` (AC 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_trims_full_name"`

**C3** - `fullName` ausente, `""` e `"   "` respondem `400` `application/problem+json` com `errors.fullName`, e nenhuma linha com aquele e-mail existe em `AspNetUsers` (AC 3; prova também que a rota própria, não a do Identity, atende `/register` - door 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_without_full_name_returns_400"`

**C4** - `fullName` com 101 caracteres responde `400` com `errors.fullName` e não cria conta; com 100 caracteres responde `200` e grava os 100 (AC 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_full_name_length_bound"`

**C5** - Registrar o mesmo e-mail duas vezes: a segunda responde `400` `application/problem+json` com `errors.DuplicateUserName` (AC 5) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_duplicate_email_returns_400_DuplicateUserName"`

**C6** - Senha `"abc"` responde `400` `application/problem+json` com `errors.PasswordTooShort` e nenhuma conta com aquele e-mail (AC 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_weak_password_returns_400_with_identity_code"`

**C7** - E-mail `"nao-e-email"` responde `400` `application/problem+json` com `errors.InvalidEmail` e nenhuma conta, como o `/register` do Identity fazia (Surface `400`, door 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_invalid_email_returns_400_InvalidEmail"`

**C8** - Depois de um cadastro com `fullName` único, nenhuma mensagem ou propriedade de log contém esse nome (AC 8) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~ObservabilityTests.Register_never_logs_full_name"`

**C9** - O esquema tem `AspNetUsers.full_name` anulável, `character varying(100)` (door 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MigrationTests.Schema_has_user_full_name"`

### S2 - A sessão devolve quem está logado · ~4 files · ~20 KB · ~5k

**C10** - Logado, `GET /api/auth/me` responde `200` com `email` igual ao do cadastro e `fullName` `"Maria da Silva"` (AC 9, door 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Me_returns_email_and_full_name"`

**C11** - `GET /api/auth/me` sem cookie responde `401` (AC 10) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Protected_route_without_session_returns_401"`

**C12** - Conta criada pelo `UserManager` sem nome: `GET /api/auth/me` responde `200` com `fullName` `null` (AC 11) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Me_returns_null_full_name_for_account_without_name"`

**C13** - A conta semeada tem `full_name` `"Administrador"` (AC 12) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DevAdminSeedTests.Seeded_account_is_named_Administrador"`

**C14** - A suíte da API passa com os helpers de cadastro enviando `fullName` (regressão da door 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests`

### S3 - O topo da tela mostra quem está logado · ~8 files · ~40 KB · ~10k

**C15** - Em `/register`, o primeiro campo do formulário é "Nome completo", `required`, `maxLength` 100, seguido de "E-mail" (AC 13)
Proof: `npm --prefix src/web run test -- -t "register form asks the full name first"`

**C16** - Enviar o cadastro com "Maria da Silva", `ana@test.local` e `Passw0rd!` faz `POST /api/auth/register` com corpo `{ fullName: "Maria da Silva", email: "ana@test.local", password: "Passw0rd!" }` (AC 14)
Proof: `npm --prefix src/web run test -- -t "register sends the full name"`

**C17** - Cadastro que responde problem `400` "E-mail já cadastrado": o alerta mostra o título e os campos continuam com "Maria da Silva" e `ana@test.local` (AC 15)
Proof: `npm --prefix src/web run test -- -t "register form states: disabled while pending, then problem title keeps the name and e-mail"`

**C18** - Com sessão `{ email: "ana@test.local", fullName: "Ana Souza" }`, a barra `banner` do topo da área de conteúdo mostra "Ana Souza" em `/organizations` e em `/gaps` (AC 16)
Proof: `npm --prefix src/web run test -- -t "top bar shows the logged user name"`

**C19** - Com sessão `fullName: null`, a barra do topo mostra `ana@test.local` (AC 17)
Proof: `npm --prefix src/web run test -- -t "top bar falls back to the e-mail"`

**C20** - `/login` não tem campo "Nome completo" (AC 18)
Proof: `npm --prefix src/web run test -- -t "login form has no full name"`

**C21** - A suíte da web passa com a sessão vinda de `/api/auth/me` (regressão da door 3: guarda de sessão, login, organizações)
Proof: `npm --prefix src/web run test`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/auth/register` statuses (2) | 200 C1 · 400 C3 | - |
| motivos do 400 do cadastro (5) | nome vazio/ausente C3 · nome longo C4 · e-mail duplicado C5 · senha fraca C6 · e-mail inválido C7 | - |
| `fullName` vazio (3) | ausente C3 · `""` C3 · `"   "` C3 | - |
| `fullName` tamanho (2 bordas) | 100 C4 · 101 C4 | - |
| `GET /api/auth/me` statuses (2) | 200 C10 · 401 C11 | - |
| `fullName` na sessão (2) | preenchido C10 · null C12 | - |
| conteúdo da barra do topo (2) | nome C18 · e-mail C19 | - |
| telas autenticadas provadas (2) | `/organizations` C18 · `/gaps` C18 | - |
| origens da conta (3) | cadastro C1 · seed C13 · antiga sem nome C12 | - |
| doors (3) | 1 C9, C1 · 2 C3, C7, C14 · 3 C10, C11, C21 | - |

- Claims naming a status code, route or response shape: C1, C3-C7, C10-C12 - todas cruzam o HTTP
- Nenhum outro check afirma mais do que o caso que sua prova exercita

## Test policy

Os specs anteriores já respondem: rota Minimal API provada no HTTP com Postgres real; tela provada com Testing Library + MSW. Sem linhas novas.

## Swept

- validation: C3, C4, C7
- failure modes: C3, C6 (nada é criado quando o cadastro falha; `UserManager.CreateAsync` é uma escrita só)
- idempotency: C5 (o segundo cadastro com o mesmo e-mail é rejeitado)
- authorization: C11; C10 lê só a conta do cookie
- concurrency: existing - índice único `UserNameIndex` do Identity rejeita o segundo de dois cadastros simultâneos com o mesmo e-mail
- data lifecycle: C12 (contas antigas sem backfill); a coluna some com a conta
- dependency failure: n/a - nenhuma dependência externa nova
- state transitions: n/a - o nome não tem estados
- observability: C8

## Handoff

- Leitura ≈ 60 KB (Auth, AppDbContext, seed, testes de auth, web auth, AppLayout, helpers de teste) ≈ 15k. Escrita: S1 ≈ 10k, S2 ≈ 5k, S3 ≈ 10k, migration ≈ 5k. Total ≈ 45k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
