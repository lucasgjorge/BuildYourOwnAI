# Study Mode checks

Profile: standard
Plan: `.specs/features/study-mode/plan.md`

30 checks in 3 slices · 3 one-way doors · 0 open

Comandos de prova:

- API (PowerShell, com `DOCKER_HOST`, ver AGENTS.md): `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"`
- Web: `npm --prefix src/web run test -- -t "<nome do teste>"`

O fake de chat dos testes gera, para o prompt de estudo, uma pergunta válida por trecho `[n]` (alternativa certa = "certa n" na posição 0), e aceita uma resposta sobrescrita por teste para os casos inválidos.

## Checks

### S1 - Gerar uma sessão · ~10 files · ~45 KB · ~11k

**C1** - `POST /api/organizations/{id}/study-sessions` com `questionCount = 5` e dois `documentIds` (documentos com 8 trechos no total) responde `201`, `Location: /api/study-sessions/{id}`, `id`, `createdAt` e 5 `questions` com `id`, `position` = 1..5 em ordem, `prompt` não vazio e exatamente 4 `options` (AC 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Create_returns_questions_with_four_options"`

**C2** - Com `documentIds = [A]` numa organização com A e B, o prompt enviado ao modelo contém só trechos de A (marcador de B ausente) e nenhum trecho aparece duas vezes; sem `documentIds`, trechos de A e B podem aparecer (AC 2, AC 11) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Samples_only_chosen_documents_without_repeating"`

**C3** - Com 3 trechos na organização e `questionCount = 10`, a sessão tem no máximo 3 perguntas, uma por trecho (AC 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Fewer_chunks_than_requested_caps_questions"`

**C4** - O corpo da criação não contém as propriedades `correctOption`, `correct`, `explanation`, `source`, `documentId` nem `chunkIndex` em nenhuma pergunta (a ordem das alternativas não revela a certa: C10) (AC 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Creation_never_reveals_the_answer"`

**C5** - `questionCount` 0, 3, 7 e 21 respondem `400` com `errors.questionCount`; `documentIds = []` responde `400` com `errors.documentIds`; 5, 10 e 20 são aceitos (AC 5) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Invalid_input_returns_400"`

**C6** - Organização de outro usuário, organização inexistente e `documentId` de outra organização do mesmo usuário respondem `404`, e o modelo não recebe chamada (AC 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Foreign_or_missing_returns_404"`

**C7** - Organização sem documentos responde `422` e o modelo não recebe chamada (AC 7) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.No_chunks_returns_422"`

**C8** - Para cada item inválido (trecho inexistente, trecho repetido, 3 alternativas, 5 alternativas, alternativa vazia, alternativas repetidas, `correct` = 4, `correct` = -1, `prompt` vazio), misturado a um item válido, a sessão grava só a pergunta válida (AC 8) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Invalid_model_items_are_discarded"`

**C9** - Falha do modelo, resposta que não é JSON e JSON sem nenhum item válido respondem `502` sem a mensagem do provedor, e `study_sessions` não ganha linha (AC 9) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Model_failure_returns_502_without_saving"`

**C10** - Com 20 perguntas cuja certa vem sempre na posição 0 do modelo, cada `correct_option` gravado aponta para o texto certo ("certa n") e as posições gravadas não são todas iguais (AC 10) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Options_are_shuffled_keeping_the_answer"`

**C11** - 10 perguntas no ask + 10 criações de sessão passam; a 21ª criação responde `429` (AC 11) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Shares_rate_limit_with_ask"`

**C18** - Schema: `study_sessions.organization_id` e `study_questions.session_id`/`document_id` com `ON DELETE CASCADE`; checks de `correct_option` e `chosen_option` entre 0 e 3 e de 4 alternativas; índice único `(session_id, position)` (door 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Schema_has_study_tables"`

**C19** - Apagar um documento apaga as perguntas geradas dele (as de outro documento ficam); apagar a organização apaga as sessões (Relations) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Deletes_cascade_to_study_data"`

**C20** - As duas rotas novas sem cookie respondem `401` (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Protected_route_without_session_returns_401"`

**C21** - Nenhum log da criação e da resposta contém o texto do trecho, da pergunta, das alternativas ou da explicação (observability, AGENTS) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudySessionsTests.Study_never_logs_content"`

### S2 - Responder e corrigir · ~3 files · ~15 KB · ~4k

**C12** - Responder com a `correctOption` gravada responde `200` com `correct = true`, `chosenOption`, `correctOption`, `explanation` = "explicação n" e `source` = `documentId`, `fileName` e `chunkIndex` do trecho de origem; no banco `chosen_option` e `answered_at` ficam preenchidos (AC 12) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudyAnswerTests.Right_answer_is_graded_correct"`

**C13** - Responder outra alternativa responde `200` com `correct = false`, `chosenOption` = a enviada e `correctOption` = a gravada, com `explanation` e `source` (AC 13) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudyAnswerTests.Wrong_answer_returns_the_right_one"`

**C14** - Responder de novo a mesma pergunta responde `409` e o `chosen_option` gravado continua o primeiro (AC 14) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudyAnswerTests.Second_answer_returns_409"`

**C15** - `option` -1 e 4 respondem `400` com `errors.option` (AC 15) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudyAnswerTests.Option_out_of_range_returns_400"`

**C16** - Sessão de outro usuário, sessão inexistente e pergunta de outra sessão respondem `404` e nada é gravado (AC 16) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~StudyAnswerTests.Foreign_or_missing_returns_404"`

### S3 - Aba Estudar · ~6 files · ~35 KB · ~9k

**C22** - A organização mostra as abas Conversa, Estudar e Base nessa ordem; Estudar leva a `/organizations/o1/study` (AC 17)
Proof: `npm --prefix src/web run test -- -t "organization has conversa, estudar and base tabs"`

**C23** - A aba Estudar lista os documentos com caixas marcadas, "10" escolhido entre 5, 10 e 20, e o botão "Começar"; desmarcar um documento e escolher 5 envia `documentIds` só com os marcados e `questionCount = 5` (AC 18)
Proof: `npm --prefix src/web run test -- -t "study setup sends chosen documents and count"`

**C24** - Organização sem documentos mostra "Suba documentos na Base para estudar" com link para a Base e sem "Começar" (AC 19)
Proof: `npm --prefix src/web run test -- -t "study without documents points to base"`

**C25** - Enquanto a criação não responde, mostra "Gerando perguntas…" e "Começar" fica desabilitado (AC 20)
Proof: `npm --prefix src/web run test -- -t "study shows generating state"`

**C26** - A sessão mostra "Pergunta 1 de 3", as 4 alternativas e "Responder" desabilitado até escolher uma (AC 21)
Proof: `npm --prefix src/web run test -- -t "study shows one question at a time"`

**C27** - Resposta certa: a alternativa escolhida fica com `data-state="correct"`, aparece "Certo!" e a explicação, a prévia não abre sozinha, e "Ver no documento" abre a prévia do trecho de origem (AC 22)
Proof: `npm --prefix src/web run test -- -t "right answer shows certo and optional preview"`

**C28** - Resposta errada: a escolhida fica `data-state="wrong"`, a certa `data-state="correct"`, aparece "A resposta certa é: <texto>" e a explicação, e a prévia "Prévia do trecho" abre sozinha com o trecho de origem (`chunks/{chunkIndex}` do documento da fonte) (AC 23)
Proof: `npm --prefix src/web run test -- -t "wrong answer shows the right one with the preview"`

**C29** - "Próxima pergunta" fecha a prévia e mostra "Pergunta 2 de 3"; depois da última, mostra "Você acertou 1 de 3" e "Estudar de novo" volta à escolha de documentos (AC 24, AC 25)
Proof: `npm --prefix src/web run test -- -t "study moves through questions to the result"`

**C30** - Um `502` na criação mostra o `title` e mantém os documentos e a quantidade escolhidos; um `409` na resposta mostra o `title` (AC 26)
Proof: `npm --prefix src/web run test -- -t "study errors show title and keep choices"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/organizations/{id}/study-sessions` statuses (7) | 201 C1 · 400 C5 · 401 C20 · 404 C6 · 422 C7 · 429 C11 · 502 C9 | - |
| `POST /api/study-sessions/{sessionId}/questions/{questionId}/answer` statuses (5) | 200 C12 · 400 C15 · 401 C20 · 404 C16 · 409 C14 | - |
| `questionCount` (7 valores) | 0 C5 · 3 C5 · 5 C5 · 7 C5 · 10 C5 · 20 C5 · 21 C5 | - |
| `documentIds` (4 formas) | ausente C2 · vazio C5 · da organização C1 · de outra organização C6 | - |
| itens inválidos do modelo (9) | trecho inexistente C8 · trecho repetido C8 · 3 alternativas C8 · 5 alternativas C8 · alternativa vazia C8 · alternativas repetidas C8 · correct 4 C8 · correct -1 C8 · prompt vazio C8 | - |
| falhas do modelo (3) | exceção C9 · não-JSON C9 · nenhum válido C9 | - |
| `option` (4 bordas) | -1 C15 · 0..3 certa C12 · 0..3 errada C13 · 4 C15 | - |
| 404 da resposta (3) | outro usuário C16 · inexistente C16 · pergunta de outra sessão C16 | - |
| estados da aba (8) | sem documentos C24 · escolha C23 · gerando C25 · pergunta C26 · certo C27 · errado C28 · fim C29 · erro C30 | - |
| doors (3) | 1 C18, C19, C14 · 2 C1, C12 · 3 C8, C10 | - |
| entidades (2) | `StudySession` C1, C19 · `StudyQuestion` C12, C19 | - |
| startup config: rate limit do ask (1 assembly) | `Program.cs` compartilhado com `WebApplicationFactory` C11 | - |

- Claims de status/rota/shape cruzam a fronteira HTTP contra Postgres real; os de tela via Testing Library + MSW

## Test policy

Os specs anteriores já respondem. Sem linhas novas.

## Swept

- validation: C5, C15
- failure modes: C9
- idempotency: C14 - responder de novo não troca a resposta
- authorization: C6, C16, C20; rate limit C11
- concurrency: C14 - a gravação da resposta só acontece se `chosen_option` ainda é nulo (UPDATE condicional), então duas respostas simultâneas não sobrescrevem
- data lifecycle: C19
- dependency failure: C9
- state transitions: C12, C14 (não respondida -> respondida, sem volta)
- observability: C21

## Handoff

- Leitura ≈ 40 KB (entidades, DbContext, rotas, fakes, Thread/SourcePreview) ≈ 10k. Escrita: S1 ≈ 11k, S2 ≈ 4k, S3 ≈ 9k. Testes ≈ 10k. Total ≈ 44k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta). Build num worktree separado (`../BuildYourOwnAI-study`, branch `study-mode`) porque o Verifier da feature anterior está lendo a árvore principal
