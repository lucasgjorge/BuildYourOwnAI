# Admin usage checks

Profile: standard
Plan: `.specs/features/admin-usage/plan.md`

37 checks in 4 slices · 4 one-way doors · 2 open (preços e `Admin:Emails` de produção), ambos block go-live, nenhum bloqueia o build

Comandos de prova:

- API (PowerShell, com `DOCKER_HOST`, ver AGENTS.md): `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"`
- Web: `npm --prefix src/web run test -- -t "<nome do teste>"`

Os fakes de IA passam a informar uso: o chat informa 120 tokens de entrada e 30 de saída e o modelo `gpt-4.1-mini`
(metadado do cliente); o gerador de embeddings informa 7 tokens por texto e o modelo `text-embedding-3-small`; o
roteador informa 50 e 5 e o modelo `jev-latest`. A `ApiFactory` configura `AI:Pricing` para `gpt-4.1-mini`
(0,40 / 1,60 por milhão) e `text-embedding-3-small` (10 / 0, alto de propósito para o custo não arredondar a zero), e deixa `jev-latest` sem preço. Os testes do
relatório gravam linhas em datas de 2001, fora do período de qualquer outro teste, e pedem esse período.

## Checks

### S1 - Registrar o uso · ~16 files · ~60 KB · ~15k

**C1** - `POST /api/assistants/{id}/ask` `200` grava exatamente 2 linhas para o usuário com o mesmo `correlation_id`, modo `ask`: `embedding` (`text-embedding-3-small`, entrada 7, saída 0) e `chat` (`gpt-4.1-mini`, entrada 120, saída 30) (AC 1)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Ask_records_embedding_and_chat"`

**C2** - `POST /api/route/ask` e `POST /api/organizations/{id}/route/ask` respondendo `answered` gravam 3 linhas com modo `routing` e um único `correlation_id`: `choice` (`jev-latest`, 50, 5), `embedding` e `chat` (AC 2)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Routing_records_choice_embedding_and_chat"`

**C3** - Um upload `201` de um texto com 150 trechos grava 2 linhas `embedding` modo `upload` (lotes de 100 e 50), com entrada 700 e 350 (AC 3)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Upload_records_one_embedding_per_batch"`

**C4** - Responder uma lacuna (`200`) grava 1 linha `embedding` modo `gap` (AC 4)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Gap_answer_records_embedding"`

**C5** - Criar uma sessão de estudo (`201`) grava 1 linha `chat` modo `study` (AC 5)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Study_records_chat"`

**C6** - `cost_usd` da linha `chat` do ask = 120 × 0,40/1e6 + 30 × 1,60/1e6 = 0,000096; da linha `embedding` = 7 × 10/1e6 = 0,000070; da linha `choice` (`jev-latest` sem preço) é nulo (AC 6)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Cost_uses_pricing_or_is_null"`

**C7** - Um ask cujo chat falha (`502`) grava só a linha `embedding` e nenhuma `chat`; um upload cujo embedding falha grava 0 linhas; um roteamento cuja escolha falha (`clarify`) não grava `choice` (AC 7)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Failed_call_records_nothing"`

**C8** - Com a gravação em `ai_usage` rejeitada pelo banco para o usuário, o ask responde `200` com a resposta normal, 0 linhas são gravadas e um log `Warning` do registrador é escrito (AC 8)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Recording_failure_does_not_fail_request"`

**C9** - Depois de ask, roteamento, upload, lacuna e estudo com marcadores únicos no texto, nenhum log e nenhuma coluna de texto de `ai_usage` (`correlation_id`, `mode`, `operation`, `model`) contém um marcador (AC 9)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Usage_never_stores_or_logs_content"`

**C10** - Schema: `ai_usage` com `user_id` FK para `AspNetUsers` `ON DELETE CASCADE`, `correlation_id varchar(64)`, `mode varchar(16)`, `operation varchar(16)`, `model varchar(100)`, `cost_usd numeric(12,6)` nulo, checks de `mode` e `operation` rejeitando `'outro'`, índices `(occurred_at)` e `(user_id, occurred_at)` (door 1)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageSchemaTests.Schema_has_ai_usage"`

**C11** - Apagar o usuário apaga as linhas de uso dele; as de outro usuário ficam (Relations)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageSchemaTests.Deleting_user_deletes_usage"`

**C12** - Correlação: sem `X-Correlation-ID` a resposta traz um GUID nesse header e as linhas gravam esse valor; com `X-Correlation-ID: acao-teste-<guid>` a resposta devolve o mesmo valor e as linhas o gravam; um valor com 65 caracteres é trocado por um GUID (door 1)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Correlation_id_from_header_or_generated"`

### S2 - Papel Admin · ~5 files · ~15 KB · ~4k

**C13** - Depois do startup o papel `Admin` existe em `AspNetRoles` (door 3)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminRoleTests.Startup_creates_admin_role"`

**C14** - Uma aplicação que sobe com `Admin:Emails` contendo o e-mail de uma conta já existente dá o papel a essa conta, e o `GET /api/auth/me` dela (depois de entrar) traz `isAdmin = true` (AC 10)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminRoleTests.Startup_grants_role_to_admin_emails"`

**C15** - O seed com `includeDevAccount = true` dá o papel à conta `DevSeed:AdminEmail`; com `false`, não dá; rodar duas vezes não falha nem duplica (AC 10)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminRoleTests.Dev_account_gets_role_only_in_development"`

**C16** - `GET /api/auth/me` de uma conta comum responde `200` com `email`, `fullName` e `isAdmin = false`; de uma conta com o papel, `isAdmin = true` (AC 11)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminRoleTests.Me_reports_is_admin"`

**C17** - `GET /api/admin/usage` de uma conta logada sem o papel responde `403` `application/problem+json` com `status = 403` (AC 12)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.Non_admin_gets_403_problem"`

**C18** - `GET /api/admin/usage` sem sessão responde `401` (AC 13)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Protected_route_without_session_returns_401"`

### S3 - Relatório · ~2 files · ~12 KB · ~3k

**C19** - Com linhas em 2001-03-09 23:59:59, 2001-03-10 00:00:00, 2001-03-12 23:59:59 e 2001-03-13 00:00:00 (UTC), `from=2001-03-10&to=2001-03-12` responde `200` com `from = "2001-03-10"`, `to = "2001-03-12"` e totais só das duas do meio: `calls`, `actions` (`correlation_id` distintos), `inputTokens`, `outputTokens`, `costUsd` somados (AC 14)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.Totals_count_only_the_period"`

**C20** - `byUser` traz cada usuário com uso no período com `userId`, `email`, `calls`, `actions`, `inputTokens`, `outputTokens`, `costUsd`; a ordem é custo decrescente, e entre usuários sem custo, tokens (entrada + saída) decrescentes (AC 15)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.By_user_sorted_by_cost_then_tokens"`

**C21** - `byMode` traz os 5 modos (`ask`, `routing`, `study`, `upload`, `gap`), os sem uso com zeros, ordenados por `actions` decrescentes: com 3 ações `routing` (9 chamadas) e 4 ações `ask` (8 chamadas), `ask` vem antes de `routing` (AC 16)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.By_mode_lists_all_modes_by_actions"`

**C22** - Sem `from` e `to`, `to` = hoje (UTC) e `from` = hoje − 29 dias; `since` = o menor `occurred_at` de `ai_usage` (AC 17)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.Defaults_to_last_30_days_and_since"`

**C23** - Numa base sem nenhum registro de uso, `since` é `null` e os totais são zero (AC 17)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.Since_is_null_without_usage"`

**C24** - `from=2001-03-12&to=2001-03-10` → `400` com `errors.from`; `from=abc` → `400` `errors.from`; `to=2001-02-30` → `400` `errors.to`; `from=2001-01-01&to=2002-01-02` (367 dias) → `400` `errors.to`; `from=2001-01-01&to=2002-01-01` (366 dias) → `200` (AC 18)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.Invalid_period_returns_400"`

**C25** - Com 2 linhas com preço (0,5 e 0,25) e 3 sem preço no período, `totals.costUsd = 0.75` e `totals.unpricedCalls = 3` (AC 19)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.Unpriced_calls_are_counted_not_summed"`

### S4 - Tela Uso e custo · ~6 files · ~25 KB · ~6k

**C26** - Com `isAdmin = true` a barra lateral mostra o link "Uso e custo" para `/admin/usage`; com `isAdmin = false` não mostra (AC 20)
Proof: `npm --prefix src/web run test -- -t "sidebar shows usage link only to admins"`

**C27** - Um admin em `/admin/usage` vê os períodos "7 dias", "30 dias" e "90 dias" com "30 dias" marcado (`aria-pressed`), e o pedido leva `from` = hoje − 29 e `to` = hoje (AC 21)
Proof: `npm --prefix src/web run test -- -t "usage page defaults to 30 days"`

**C28** - A tela mostra os totais (chamadas, ações, tokens de entrada e saída, custo "US$ 1,23"), a tabela por usuário com e-mail, chamadas, ações, tokens e custo em cada linha na ordem recebida, e o uso por modo com rótulos em português, o primeiro marcado como mais usado (`data-top="true"`) e só ele (AC 21)
Proof: `npm --prefix src/web run test -- -t "usage page shows totals, users and modes"`

**C29** - Clicar em "7 dias" pede o relatório com `from` = hoje − 6 e mostra os números da nova resposta (AC 22)
Proof: `npm --prefix src/web run test -- -t "changing period requests new report"`

**C30** - Com `totals.calls = 0` a tela mostra "Nenhum uso de IA neste período" e nenhuma tabela (AC 23)
Proof: `npm --prefix src/web run test -- -t "usage page empty period"`

**C31** - Enquanto o relatório não responde mostra "Carregando uso…" (AC 24)
Proof: `npm --prefix src/web run test -- -t "usage page loading"`

**C32** - Um `500` do relatório mostra o `title` do problem details (AC 24)
Proof: `npm --prefix src/web run test -- -t "usage page error shows title"`

**C33** - Um não-admin em `/admin/usage` vê "Esta página é só para administradores" e o relatório não é pedido (AC 25)
Proof: `npm --prefix src/web run test -- -t "usage page is admin only"`

**C34** - Com `totals.unpricedCalls = 3` a tela mostra "3 chamadas sem preço configurado não entram no custo"; com 0, não mostra (AC 26)
Proof: `npm --prefix src/web run test -- -t "usage page warns about unpriced calls"`

### Transversais

**C35** - A resposta de `GET /api/admin/usage` a um admin traz em `totals` exatamente `calls`, `actions`, `inputTokens`, `outputTokens`, `costUsd`, `unpricedCalls`, e na raiz `from`, `to`, `since`, `totals`, `byUser`, `byMode` (Surface)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminUsageTests.Response_has_surface_fields"`

**C36** - Uma conta que recebe o papel só o vê em `isAdmin` e ganha acesso a `/api/admin/usage` depois de entrar de novo: antes, `false` e `403`; depois, `true` e `200` (Impact auth)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AdminRoleTests.Role_applies_after_login_again"`

**C37** - Um upload cujo segundo lote de embeddings falha (`502`) grava a linha do primeiro lote e nenhuma do segundo (AC 7, sobre lotes)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UsageRecordingTests.Partial_upload_records_only_successful_batches"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `GET /api/auth/me` statuses (2) | 200 C16 · 401 existing (user-name `AuthTests.Protected_route_without_session_returns_401`, rota já na lista) C18 | - |
| `GET /api/admin/usage` statuses (4) | 200 C19 · 400 C24 · 401 C18 · 403 C17 | - |
| modos (5) | `ask` C1 · `routing` C2 · `upload` C3 · `gap` C4 · `study` C5 | - |
| operações (3) | `chat` C1 · `embedding` C1 · `choice` C2 | - |
| rotas de escolha automática (2) | `/api/route/ask` C2 · `/api/organizations/{id}/route/ask` C2 | - |
| custo (3) | com preço de entrada e saída C6 · só entrada C6 · sem preço C6 | - |
| chamada que falha (4) | chat do ask C7 · embedding do upload C7 · escolha C7 · segundo lote C37 | - |
| correlação (3) | ausente C12 · recebida C12 · longa demais C12 | - |
| bordas do período (4 instantes) | 2001-03-09 23:59:59 fora C19 · 03-10 00:00 dentro C19 · 03-12 23:59:59 dentro C19 · 03-13 00:00 fora C19 | - |
| período inválido (5) | from > to C24 · from inválido C24 · to inválido C24 · 367 dias C24 · 366 dias aceito C24 | - |
| `since` (2) | com uso C22 · sem uso C23 | - |
| ordem de `byUser` (2) | custo C20 · tokens sem custo C20 | - |
| objetos de Surface do relatório (4) | root C35 · totals C35 · byUser C20 · byMode C21 | - |
| configuração (4 chaves) | `AI:Pricing:<m>:InputPerMillion` C6 · `AI:Pricing:<m>:OutputPerMillion` C6 · `Admin:Emails` C14 · `DevSeed:AdminEmail` C15 | - |
| estados da tela (9) | link admin/não-admin C26 · período padrão C27 · dados C28 · troca de período C29 · vazio C30 · carregando C31 · erro C32 · não-admin C33 · sem preço C34 | - |
| doors (4) | 1 C10, C12 · 2 C1, C8 · 3 C13, C14, C15 · 4 C16, C17 | - |
| entidades (2) | `AiUsage` C10, C11 · papel `Admin` C13, C36 | - |
| startup config: seed do papel (1 assembly) | `Program.cs` compartilhado com `WebApplicationFactory` C13, C14 | - |
| startup config: middleware de correlação (1 assembly) | `Program.cs` compartilhado com `WebApplicationFactory` C12 | - |

- Claims de status/rota/shape cruzam a fronteira HTTP contra Postgres real; os de tela via Testing Library + MSW
- C15 prova o ramo de Development no `AdminRoleSeed` chamado direto (o `Program.cs` só repassa `IsDevelopment()`), porque subir o host em Development carregaria o `appsettings.Development.json` sobre a conexão de teste

## Test policy

Os specs anteriores já respondem. Sem linhas novas.

## Swept

- validation: C24
- failure modes: C7, C8, C37
- idempotency: C15 - o seed roda a cada startup sem duplicar papel
- authorization: C17, C18, C36; o relatório não passa pelo filtro de dono (é de todos os usuários), só pela política de papel
- concurrency: n/a - cada chamada grava uma linha nova, sem leitura-e-escrita; a agregação é só leitura
- data lifecycle: C11; sem expurgo (Assumptions do plano)
- dependency failure: C7, C8
- state transitions: C36 (sem papel -> com papel, visível após novo login)
- observability: C9, C8 (o aviso existe)

## Handoff

- Leitura ≈ 180 KB (handlers de IA, pipeline, ingestão, DbContext, testes base, fakes, web auth/layout/test) ≈ 45k, já feita. Escrita: S1 ≈ 15k, S2 ≈ 4k, S3 ≈ 3k, S4 ≈ 6k. Testes ≈ 15k. Total ≈ 88k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
