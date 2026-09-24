# Prévia do trecho e escolha automática checks

Profile: standard
Plan: `.specs/features/source-preview/plan.md`

18 checks in 3 slices · 2 one-way doors · 0 open

Comandos de prova:

- API (PowerShell, com `DOCKER_HOST`, ver AGENTS.md): `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"`
- Web: `npm --prefix src/web run test -- -t "<nome do teste>"`

Checks anteriores afetados pela renomeação (door 2): os testes `JevTests`, `OrganizationJevTests` e `JevChoiceTests` passam a se chamar `RoutingTests`, `OrganizationRoutingTests` e `RoutingChoiceTests` e a chamar as rotas novas, com as mesmas asserções. Os textos de tela "Jev decide", "via Jev", "Jev está escolhendo…" e "Jev (todas)" dos testes de web passam a ser os de AC 14. As provas do jev-gaps, org-chat e jev-choice que citam os nomes antigos ficam no histórico; C6 deste arquivo é a prova atual daquele contrato.

## Checks

### S1 - Prévia do trecho · ~8 files · ~50 KB · ~12k

**C1** - Num documento de 4 trechos, `GET .../chunks/1?around=1` responde `200` com `documentId`, `fileName`, `chunkCount = 4` e `chunks` = índices [0, 1, 2] com `content` igual ao gravado; sem `around`, o mesmo resultado (AC 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentChunksTests.Returns_chunk_with_neighbors"`

**C2** - `.../chunks/0` devolve [0, 1]; `.../chunks/3` devolve [2, 3]; `around=0` devolve só o pedido; `around=2` em `.../chunks/1` devolve [0, 1, 2, 3] (AC 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentChunksTests.Edges_return_only_existing_neighbors"`

**C3** - `around=-1` e `around=3` respondem `400` com `errors.around` (AC 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentChunksTests.Around_out_of_range_returns_400"`

**C4** - Organização de outro usuário, organização inexistente, documento de outra organização do mesmo usuário, `index = chunkCount` e `index = -1` respondem `404` problem details (AC 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentChunksTests.Foreign_or_missing_returns_404"`

**C5** - `GET .../chunks/{index}` sem cookie responde `401` (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Protected_route_without_session_returns_401"`

**C11** - Resposta `found=true` com 5 fontes de 2 documentos (3 de `01_rh.txt`, 2 de `02_cultura.txt`) mostra cada nome de arquivo uma vez, 3 botões de trecho sob o primeiro e 2 sob o segundo, e abre a prévia do primeiro trecho sem clique (AC 5) ✓
Proof: `npm --prefix src/web run test -- -t "sources are grouped by document and the first chunk opens"`

**C12** - Clicar em "trecho 5" do `02_cultura.txt` pede `.../documents/d2/chunks/4`; o painel "Prévia do trecho" mostra `02_cultura.txt`, "Trecho 5 de 8", o trecho 4 marcado com `aria-current` e os vizinhos 3 e 5 sem marca (AC 6) ✓
Proof: `npm --prefix src/web run test -- -t "preview shows the chunk with its neighbors"`

**C13** - Resposta `found=false` com fontes não mostra "Fontes", nenhum botão de trecho e nenhum painel de prévia, e não chama a leitura de trechos (AC 7) ✓
Proof: `npm --prefix src/web run test -- -t "not found answer shows no sources and no preview"`

**C14** - "Fechar prévia" remove o painel; clicar de novo num trecho reabre (AC 8) ✓
Proof: `npm --prefix src/web run test -- -t "preview closes and reopens"`

**C15** - Enquanto a leitura não responde, o painel mostra "Carregando trecho…"; um `404` mostra o `title` (AC 9) ✓
Proof: `npm --prefix src/web run test -- -t "preview loading and error states"`

**C16** - O painel é `role=complementary` "Prévia do trecho" com classes de sobreposição abaixo de `lg` (`fixed`, `inset-0`) e coluna fixa ao lado do chat a partir de `lg` (`lg:sticky`), e o botão "Fechar prévia" sempre renderizado (AC 10) ✓
Proof: `npm --prefix src/web run test -- -t "preview overlays on narrow screens"`

### S2 - Escolha automática sem nome próprio · ~25 files · ~120 KB · ~30k

**C6** - Os testes de roteamento renomeados (`RoutingTests`, `OrganizationRoutingTests`, `RoutingChoiceTests`) passam contra `POST /api/route/ask` e `POST /api/organizations/{id}/route/ask` (AC 11) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~RoutingTests|FullyQualifiedName~OrganizationRoutingTests|FullyQualifiedName~RoutingChoiceTests"`

**C7** - `POST /api/jev/ask` e `POST /api/organizations/{id}/jev/ask` respondem `404` problem details (AC 12) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~RoutingTests.Old_jev_routes_are_gone"`

**C8** - Com `AI:Routing:ConfidenceThreshold = 0.95` e uma escolha com confiança 0.9, a rota responde `clarify`; com o padrão (0.6), a mesma escolha responde `answered` (AC 13) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~RoutingChoiceTests.Threshold_comes_from_routing_config"`

**C9** - No chat: o seletor "Para" tem "Escolha automática"; a resposta roteada mostra "escolha automática"; enquanto escolhe, "Escolhendo quem responde…"; a barra lateral tem "Todas as IAs" -> `/all` (AC 14) ✓
Proof: `npm --prefix src/web run test -- -t "automatic choice wording"`

**C10** - A busca por "jev" (sem diferenciar caixa) em `src/`, `docs/`, `README.md`, `AGENTS.md` e `.claude/`, excluindo `Migrations/` e `package-lock.json`, só encontra linhas com `jev-latest` (AC 15, AC 16) ✓
Proof: `bash -c "! (git grep -n -i jev -- src docs README.md AGENTS.md .claude ':(exclude)src/BuildYourOwnAI.Api/Infrastructure/Data/Migrations' ':(exclude)src/web/package-lock.json' | grep -v -i 'jev-latest')"`

### S3 - Layout sem rolagem lateral · ~2 files · ~10 KB · ~3k

**C17** - O contêiner raiz da área logada tem `overflow-x-clip`, e a thread rola só na vertical: `scrollIntoView` não é chamado e `window.scrollTo` recebe só `top` (AC 17) ✓
Proof: `npm --prefix src/web run test -- -t "chat never scrolls sideways"`

**C18** - Os testes de web existentes (chat, organizações, lacunas, página inicial, estilos, auth) passam com os textos novos (regressão de S2) ✓
Proof: `npm --prefix src/web run test -- -t "chat sends to organization routing|asking an alternative appends|pinned assistant is asked directly|nav shows open gap count|home explains how it works|styles keep focus visible"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `GET /api/organizations/{id}/documents/{documentId}/chunks/{index}` statuses (4) | 200 C1 · 400 C3 · 401 C5 · 404 C4 | - |
| vizinhos (4 casos) | meio C1 · primeiro C2 · último C2 · `around` 0 e 2 C2 | - |
| 404 da leitura (5) | outro usuário C4 · organização inexistente C4 · documento de outra organização C4 · índice = chunkCount C4 · índice -1 C4 | - |
| `around` (4 bordas) | -1 C3 · 0 C2 · 2 C2 · 3 C3 | - |
| estados do painel (6) | aberto sozinho C11 · por clique C12 · carregando C15 · erro C15 · fechado C14 · tela estreita C16 | - |
| fontes por `found` (2) | true C11 · false C13 | - |
| rotas renomeadas (4) | `/api/route/ask` C6 · `/api/organizations/{id}/route/ask` C6 · `/api/jev/ask` 404 C7 · `/api/organizations/{id}/jev/ask` 404 C7 | - |
| textos da escolha automática (4) | seletor C9 · marca na resposta C9 · carregando C9 · barra lateral C9 | - |
| lugares sem "Jev" (5) | `src/` C10 · `docs/` C10 · `README.md` C10 · `AGENTS.md` C10 · `.claude/` C10 | - |
| doors (2) | 1 C1, C4 · 2 C6, C7, C10 | - |
| startup config: `AI:Routing` (2 assemblies) | `Program.cs`/`AddAi` C8 · `ApiFactory` (padrão) C6 | - |

## Test policy

Os specs anteriores já respondem. Sem linhas novas.

## Swept

- validation: C3
- failure modes: C15
- idempotency: n/a - leitura; a renomeação não grava nada
- authorization: C4, C5
- concurrency: n/a - nenhuma escrita nova
- data lifecycle: n/a - nada persistido
- dependency failure: existing - fallback do roteamento (jev-choice), provado em C6 pelos mesmos testes
- state transitions: n/a - nenhuma entidade com estado
- observability: n/a - a rota nova não loga; a leitura não carrega nada sensível para log

## Handoff

- Leitura ≈ 70 KB (Thread, rotas, testes do roteamento, docs) ≈ 18k. Escrita: S1 ≈ 12k, S2 ≈ 30k (renomeação em ~25 arquivos), S3 ≈ 3k. Testes ≈ 10k. Total ≈ 73k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
- **Boundary:** C1-C18 fechados no commit da feature (este)
- **Settled mid-build:** o usuário confirmou "à direita, como uma prévia". C16 corrigido antes de a prova passar: o check pedia `lg:static`, mas uma coluna ao lado de uma página que rola precisa de `lg:sticky` para continuar visível; a obrigação (sobrepõe no estreito, coluna no largo) é a mesma, só a classe estava errada. A prévia existe só no chat da organização: na tela "Todas as IAs" a resposta não traz o id da organização, e a leitura de trechos é por organização (as fontes aparecem agrupadas, sem botão)
- **Abandoned:** substituições encadeadas no script de renomeação da Api (uma troca anterior mudava o alvo da seguinte); o script ficou idempotente e as trocas sem espaços à esquerda. O teste C9 declarava o handler com atraso depois dos padrões e o MSW usava o padrão; o handler com atraso vai primeiro
