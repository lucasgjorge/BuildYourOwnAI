# RAG MVP checks

Profile: standard
Plan: `.specs/features/rag-mvp/plan.md`

49 checks in 7 slices · 9 one-way doors · 1 open, of which 0 block (1 blocks go-live)

Comandos de prova:

- API: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"` (precisa do Docker - Testcontainers `pgvector/pgvector:pg17`)
- Web: `npm --prefix src/web run test -- -t "<nome do teste>"` (Vitest)

## Checks

### S1 - Conta e login · 3 files · ~20 KB · ~5k

**C1** - `POST /api/auth/register` com e-mail válido e senha `Passw0rd!` responde `200`, e o login seguinte com as mesmas credenciais responde `200` (AC 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_valid_returns_200_and_enables_login"`

**C2** - Registrar um e-mail já cadastrado responde `400`, `application/problem+json`, com `errors.DuplicateUserName` presente (AC 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Register_duplicate_email_returns_400_DuplicateUserName"`

**C3** - `POST /api/auth/login?useCookies=true` com credenciais corretas responde `200` e um `Set-Cookie` de sessão contendo `httponly`, `secure` e `samesite=strict` (AC 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Login_valid_sets_httponly_secure_strict_cookie"`

**C4** - Login com senha errada e login com e-mail inexistente respondem `401` e nenhum dos dois traz `Set-Cookie` de sessão (AC 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Login_invalid_returns_401_without_cookie"`

**C5** - `POST /api/auth/logout` com sessão responde `204` com `Set-Cookie` que expira o cookie de sessão, e o mesmo cliente (que respeita o `Set-Cookie`) recebe `401` em `GET /api/assistants` logo depois (AC 5) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Logout_returns_204_and_ends_session"`

**C6** - Cada uma das 8 rotas `/api/assistants*` chamada sem cookie responde `401`, nunca `302` (AC 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Protected_route_without_session_returns_401"`

### S2 - Criar e gerenciar a própria IA · 4 files · ~25 KB · ~6k

**C7** - `POST /api/assistants` com `name` de 1 e de 100 caracteres (com espaços nas pontas removidos), com `instructions` ausente e com 4000 caracteres, responde `201`, `Location: /api/assistants/{id}` e corpo com `id`, `name` sem espaços nas pontas, `instructions`, `createdAt` (AC 7) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Create_valid_returns_201_with_location"`

**C8** - `name` `""`, `"   "` ou com 101 caracteres responde `400` com `errors.name`; `instructions` com 4001 caracteres responde `400` com `errors.instructions` (AC 8) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Create_invalid_returns_400_keyed_by_field"`

**C9** - `GET /api/assistants` devolve `[]` para um usuário novo; depois de criar A e B, devolve `[B, A]` (por `createdAt` decrescente) com `documentCount` correto, e nunca os assistentes de outro usuário (AC 9) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.List_returns_only_own_newest_first"`

**C10** - Nas 6 rotas `/api/assistants/{id}*`, um `{id}` de outro usuário e um `{id}` inexistente respondem `404`, e o assistente do outro usuário continua com os mesmos documentos (AC 10) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Foreign_or_missing_assistant_returns_404"`

**C11** - `DELETE /api/assistants/{id}` do dono responde `204`, e no banco não resta nenhuma linha de `Document` nem de `Chunk` desse assistente (AC 11) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Delete_returns_204_and_cascades"`

### S3 - Anexar documentos · 6 files · ~45 KB · ~11k

**C12** - Upload de `.txt`, `.md` e `.pdf` (PDF gerado com texto) com texto extraível responde `201` com `chunkCount` > 0, `fileName`, `sizeBytes` iguais ao arquivo; no banco existem exatamente `chunkCount` chunks, cada um com embedding de 1536 dimensões (AC 12) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Upload_supported_file_returns_201_and_persists_chunks"`

**C13** - Upload de `.exe`, `.docx` e de arquivo sem extensão responde `415`; `NOTAS.TXT` (extensão maiúscula) responde `201` (AC 13) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Upload_extension_is_checked_case_insensitively"`

**C14** - Um `.txt` de 10 485 761 bytes responde `413`; um `.txt` de exatamente 10 485 760 bytes responde `201` (AC 14) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Upload_size_limit_is_10485760_bytes"`

**C15** - Requisição multipart sem a parte `file`, arquivo de 0 bytes e corpo não-multipart respondem `400` (AC 15) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Upload_without_file_returns_400"`

**C16** - `.txt` só com espaços/quebras de linha e PDF sem texto respondem `422`, e o assistente continua com 0 documentos (AC 16) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Upload_without_text_returns_422"`

**C17** - O mesmo conteúdo enviado duas vezes ao mesmo assistente (mesmo com outro nome de arquivo) responde `201` e depois `409`, com 1 documento no assistente; o mesmo conteúdo em outro assistente responde `201` (AC 17) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Upload_duplicate_content_returns_409"`

**C18** - Quando o gerador de embeddings falha, o upload responde `502`, o corpo não contém a mensagem da exceção do provedor, e no banco há 0 `Document` e 0 `Chunk` do assistente (AC 18) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Upload_embedding_failure_returns_502_and_persists_nothing"`

**C19** - `GET /api/assistants/{id}/documents` devolve os documentos do assistente em ordem de `uploadedAt` decrescente, com `id`, `fileName`, `sizeBytes`, `chunkCount`, `uploadedAt` (AC 19) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.List_returns_newest_first"`

**C20** - `DELETE` de um documento responde `204`; seus chunks somem do banco e um ask seguinte não traz esse `documentId` em `sources` (AC 20) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Delete_returns_204_and_removes_from_retrieval"`

**C21** - `DELETE /api/assistants/{A}/documents/{docDeB}` (B do mesmo usuário) responde `404` e o documento de B continua existindo (AC 21) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Delete_document_of_other_assistant_returns_404"`

### S4 - Perguntar à própria IA · 4 files · ~30 KB · ~8k

**C22** - Com 7 chunks no assistente, ask responde `200`, `answer` não vazio e `sources` com exatamente os 5 chunks de menor distância de cosseno ao embedding da pergunta, em ordem, cada um com `documentId`, `fileName`, `chunkIndex`, `excerpt`; com 2 chunks, `sources` tem os 2 (AC 22) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Ask_returns_answer_with_top5_sources"`

**C23** - Dois assistentes do mesmo usuário com documentos distintos: o ask a um deles não traz em `sources` nenhum documento do outro, e o texto enviado ao modelo não contém nenhum trecho do outro (AC 23) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Ask_never_retrieves_from_another_assistant"`

**C24** - As mensagens enviadas ao `IChatClient` contêm as `instructions` do assistente, o texto de cada chunk de `sources` e a pergunta (AC 24) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Ask_prompt_contains_instructions_chunks_and_question"`

**C25** - Ask a um assistente sem documentos responde `200` com `sources: []` (AC 25) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Ask_without_documents_returns_empty_sources"`

**C26** - `question` `""`, `"   "` e com 2001 caracteres responde `400` com `errors.question`; com 2000 caracteres responde `200` (AC 26) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Ask_question_bounds"`

**C27** - Falha do gerador de embeddings e falha do cliente de chat respondem `502` com problem details cujo corpo não contém a mensagem da exceção (AC 27) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Ask_provider_failure_returns_502"`

**C28** - Um usuário faz 20 asks seguidos (todos `200`) e o 21º dentro de 60 s responde `429`; outro usuário no mesmo instante recebe `200` (AC 28) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Ask_rate_limit_is_20_per_minute_per_user"`

### S5 - UI web · ~20 files · ~60 KB · ~15k

**C29** - Com `GET /api/auth/manage/info` respondendo `401`, abrir `/assistants` e `/assistants/abc` leva à tela de `/login`; `/register` abre sem redirecionar (AC 29) ✓
Proof: `npm --prefix src/web run test -- -t "redirects to login without session"`

**C30** - Logado e com `GET /api/assistants` = `[]`, `/assistants` mostra "Você ainda não criou nenhuma IA" e o botão "Criar IA" (AC 30) ✓
Proof: `npm --prefix src/web run test -- -t "shows empty state"`

**C31** - `/assistants/{id}` mostra a lista de documentos (com o `fileName` de cada um), o campo de upload e a caixa de pergunta (AC 31) ✓
Proof: `npm --prefix src/web run test -- -t "assistant page shows documents upload and question"`

**C32** - Depois de perguntar, a página mostra o `answer` e o `fileName` de cada fonte (AC 32) ✓
Proof: `npm --prefix src/web run test -- -t "shows answer and source file names"`

**C33** - Quando a Api responde problem details, a página mostra o `title` e mantém o texto digitado: no formulário de criar IA e na caixa de pergunta (AC 33) ✓
Proof: `npm --prefix src/web run test -- -t "shows problem title and keeps input"`

**C34** - Clicar em apagar (assistente e documento) abre confirmação; cancelar não chama `DELETE`; confirmar chama `DELETE` (AC 34) ✓
Proof: `npm --prefix src/web run test -- -t "delete asks for confirmation"`

**C35** - Durante o upload e durante a pergunta, o botão que disparou a ação fica desabilitado e mostra "Processando..." (AC 35) ✓
Proof: `npm --prefix src/web run test -- -t "disables button while processing"`

### S6 - Portas e transversais · 5 files · ~20 KB · ~5k

**C36** - Depois das migrations, `chunks.embedding` é `vector(1536)` e existe um índice `hnsw` com `vector_cosine_ops` sobre ele (door 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~SchemaTests.Chunk_embedding_is_vector1536_with_hnsw_cosine_index"`

**C37** - Dois uploads simultâneos do mesmo conteúdo ao mesmo assistente resultam em exatamente um `201` e um `409`, e 1 documento (door 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Concurrent_duplicate_uploads_yield_one_201_one_409"`

**C38** - Nenhum arquivo em `src/BuildYourOwnAI.Api/Features` referencia `OpenAI` (door 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~ArchitectureTests.Features_do_not_reference_OpenAI"`

**C39** - As respostas `400`, `404`, `409`, `413`, `415`, `422`, `429` e `502` da Api têm `Content-Type: application/problem+json` e `status` no corpo igual ao status HTTP (door 7) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~ProblemDetailsTests.Error_responses_are_problem_json"`

**C40** - Um upload bem-sucedido gera exatamente um log `Information` com as propriedades `DocumentId`, `ChunkCount` e `ElapsedMs`, e nenhum log emitido durante upload e ask contém o texto do documento, da pergunta ou da resposta (observability) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~ObservabilityTests.Ingestion_logs_ids_and_counts_never_content"`

**C41** - `GET /` e `GET /assistants/x` devolvem o `index.html` do SPA (`200`, `text/html`); `GET /api/nao-existe` devolve `404` problem details, não HTML (door 9) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~SpaHostingTests.Spa_fallback_serves_index_but_not_for_api"`

**C42** - O chunker devolve 0 chunks para texto vazio/espaços, 1 chunk para 1000 caracteres, e para 2500 caracteres devolve chunks de até 1000 caracteres em que cada chunk seguinte começa até 200 caracteres antes do fim do anterior; sequências de espaço viram um espaço só (test policy - decision) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~TextChunkerTests"`

**C43** - `GET /api/auth/manage/info` responde `200` com o `email` do usuário quando há sessão e `401` sem sessão (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Manage_info_reflects_session"`

**C44** - `GET /api/assistants/{id}` do dono responde `200` com `id`, `name`, `instructions`, `createdAt` e `documentCount` igual ao número de documentos enviados (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Get_returns_200_with_document_count"`

### S7 - Estados de tela que faltavam (rodada 2 do Verifier) · 2 files · ~8 KB · ~2k

**C45** - Em `/login`: enquanto o login está pendente, o botão fica desabilitado com o texto "Processando..."; um `401` mostra "E-mail ou senha inválidos." e mantém o e-mail digitado; um login `200` leva a `/assistants` (Observable `/login` error, loading) ✓
Proof: `npm --prefix src/web run test -- -t "login form states"`

**C46** - Em `/register`: enquanto pendente, o botão fica desabilitado com "Processando..."; problem details da Api mostram o `title` e mantêm o e-mail digitado; sucesso leva a `/assistants` (Observable `/register` error, loading) ✓
Proof: `npm --prefix src/web run test -- -t "register form states"`

**C47** - `/assistants` mostra "Carregando..." enquanto `GET /api/assistants` não responde, e a lista depois que responde (Observable `/assistants` loading) ✓
Proof: `npm --prefix src/web run test -- -t "assistants list shows loading"`

**C48** - `/assistants/{id}` com `GET documents` = `[]` não mostra nenhum item de documento e mostra o campo de upload (Observable `/assistants/{id}` empty state) ✓
Proof: `npm --prefix src/web run test -- -t "empty document list shows only upload"`

**C49** - Um upload respondido com problem details mostra o `title` na página de assistente (AC 33, Observable `/assistants/{id}` error) ✓
Proof: `npm --prefix src/web run test -- -t "upload error shows problem title"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/auth/register` statuses (2) | 200 C1 · 400 C2 | - |
| `POST /api/auth/login` statuses (2) | 200 C3 · 401 C4 | - |
| `POST /api/auth/logout` statuses (1) | 204 C5 | - |
| `GET /api/auth/manage/info` statuses (2) | 200 C43 · 401 C43 | - |
| `POST /api/assistants` statuses (3) | 201 C7 · 400 C8 · 401 C6 | - |
| `GET /api/assistants` statuses (2) | 200 C9 · 401 C6 | - |
| `GET /api/assistants/{id}` statuses (3) | 200 C44 · 401 C6 · 404 C10 | - |
| `DELETE /api/assistants/{id}` statuses (3) | 204 C11 · 401 C6 · 404 C10 | - |
| `POST /api/assistants/{id}/documents` statuses (9) | 201 C12 · 400 C15 · 401 C6 · 404 C10 · 409 C17 · 413 C14 · 415 C13 · 422 C16 · 502 C18 | - |
| `GET /api/assistants/{id}/documents` statuses (3) | 200 C19 · 401 C6 · 404 C10 | - |
| `DELETE /api/assistants/{id}/documents/{documentId}` statuses (3) | 204 C20 · 401 C6 · 404 C10 | - |
| `POST /api/assistants/{id}/ask` statuses (6) | 200 C22 · 400 C26 · 401 C6 · 404 C10 · 429 C28 · 502 C27 | - |
| rotas protegidas sem sessão (8) | C6, table-driven sobre as 8 | - |
| rotas `{id}` × {outro usuário, inexistente} (12) | GET id outro C10 · GET id inexistente C10 · DELETE id outro C10 · DELETE id inexistente C10 · POST documents outro C10 · POST documents inexistente C10 · GET documents outro C10 · GET documents inexistente C10 · DELETE document outro C10 · DELETE document inexistente C10 · POST ask outro C10 · POST ask inexistente C10 | - |
| extensões aceitas (3) | `.pdf` C12 · `.txt` C12 · `.md` C12 | - |
| extensões rejeitadas (3 amostras) + caixa | `.exe` C13 · `.docx` C13 · sem extensão C13 · `.TXT` aceita C13 | - |
| limite de tamanho (2 bordas) | 10 485 760 C14 · 10 485 761 C14 | - |
| entrada ausente no upload (3) | sem parte `file` C15 · 0 bytes C15 · não-multipart C15 | - |
| sem texto extraível (2) | `.txt` só espaços C16 · PDF sem texto C16 | - |
| `name` (5 bordas) | vazio C8 · só espaços C8 · 1 C7 · 100 C7 · 101 C8 | - |
| `instructions` (3 bordas) | ausente C7 · 4000 C7 · 4001 C8 | - |
| `question` (4 bordas) | vazio C26 · só espaços C26 · 2000 C26 · 2001 C26 | - |
| top-K (2 casos) | mais de 5 chunks C22 · menos de 5 chunks C22 | - |
| falhas de provedor (3) | embedding no upload C18 · embedding no ask C27 · chat no ask C27 | - |
| rate limit (3) | 20º C28 · 21º C28 · outro usuário C28 | - |
| telas (4) | `/login` C29, C45 · `/register` C29, C46 · `/assistants` C30, C34, C47 · `/assistants/{id}` C31, C32, C33, C34, C35, C48, C49 | - |
| estados por tela do Observable (11) | `/login` erro C45 · `/login` carregando C45 · `/register` erro C46 · `/register` carregando C46 · `/assistants` vazio C30 · `/assistants` carregando C47 · `/assistants` erro C33 · `/assistants` sem sessão C29 · `/assistants/{id}` vazio C48 · `/assistants/{id}` erro C33, C49 · `/assistants/{id}` carregando C35 | - |
| ações destrutivas na UI (2) | apagar assistente C34 · apagar documento C34 | - |
| ações em andamento na UI (2) | upload C35 · pergunta C35 | - |
| erro exibido na UI (5) | criar IA C33 · pergunta C33 · upload C49 · login C45 · registro C46 | - |
| doors (9) | 1 C12, C36 · 2 C36, C22 · 3 C11, C17, C37 · 4 C3, C5 · 5 C10, C23 · 6 C38 · 7 C39 · 8 C18 · 9 C41 | - |
| entidades (3) | `Assistant` C7, C11 · `Document` C12, C20 · `Chunk` C12, C36 | - |
| startup config: cookie, rate limiter, problem details, SPA (1 assembly) | `Program.cs` compartilhado com `WebApplicationFactory` C3, C28, C39, C41 | - |

- Claims que nomeiam status, rota ou shape de resposta: todas cruzam a fronteira HTTP via `WebApplicationFactory` contra Postgres real
- C42 é a única prova em nível de unidade, pelo test policy abaixo

## Test policy

O repositório não responde às duas perguntas (é novo), então:

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Endpoint que decide (validação, posse, dedup, limites) | um na fronteira HTTP contra Postgres real | cada entrada aceita, cada entrada rejeitada, cada caminho de erro - cada borda como membro em Coverage |
| Decisão pura atrás de um endpoint (chunker) | um na fronteira (via C12) **e** um no próprio nível | cada regra do chunker (vazio, cabe em um, overlap, normalização) |
| Componente React que decide o que mostrar | um com Testing Library + MSW | cada estado: vazio, carregando, erro, sucesso |
| Instrumentação (registro de DI, mapeamento de rota, DTO) | nenhuma própria | coberta pela prova do consumidor |

Evidence:

- `TextChunker`: janela com overlap e normalização de espaço, 4 regras → decide
- handlers de `Features/*`: validação + posse + status → decidem, provados na fronteira
- registro de IA/DI: encaminha, sem condicional → instrumentação
- análogo no repo: nenhum (repositório novo)

Cost: 1 classe de teste de unidade (chunker); o resto já estava na fronteira. As linhas **não** vão para as guidelines do repo nesta feature.

## Swept

- validation: C8, C13, C14, C15, C26
- failure modes: C16, C18, C27
- idempotency: C17 - reenviar o mesmo arquivo é rejeitado com 409, sem segundo documento
- authorization: C6, C10, C21, C23; rate limit C28
- concurrency: C37
- data lifecycle: C11, C20
- dependency failure: C18, C27
- state transitions: n/a - `Document` não tem status (door 8) e `Assistant` só existe ou foi apagado (C11)
- observability: C40

## Handoff

- Repositório novo: o custo são arquivos escritos, não lidos. S1-S4 + S6 ≈ 24 arquivos ≈ 140 KB ≈ 35k; S5 entra no web com ≈ 20 arquivos ≈ 60 KB ≈ 15k; total ≈ 50k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
- **Boundary:** C1-C28, C36-C44 fechados em `3d64515`; C29-C35 fechados no commit do web (este)
- **Settled mid-build:** nenhum esclarecimento do usuário durante o build. Door 5b adicionada ao `Landing` antes do código (filtro de dono também em `Document`/`Chunk`)
- **Abandoned:** ambiente jsdom nos testes do web - o `FormData` do jsdom não é aceito pelo `fetch` do Node, então o upload nunca chegava ao MSW; trocado por happy-dom. `vi.spyOn(window, 'confirm')` - happy-dom não define `confirm`; o teste atribui um `vi.fn()`
- **Round 2 fix (after Verifier FAIL at `bef8502`):** C45-C49 adicionados para os estados de tela do `Observable` sem prova; asserções de C9 (`name`, `instructions`, `createdAt`), C43 (`isEmailConfirmed`) e C40 (propriedades estruturadas do log) reforçadas; login `401` passa a mostrar "E-mail ou senha inválidos."
