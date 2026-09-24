# Organizações, Jev e Lacunas checks

Profile: standard
Plan: `.specs/features/jev-gaps/plan.md`

62 checks in 4 slices · 7 one-way doors · 2 open, of which 0 block (2 block go-live)

Comandos de prova:

- API: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"` (precisa do Docker)
- Web: `npm --prefix src/web run test -- -t "<nome do teste>"` (Vitest)

Checks do rag-mvp afetados por este plano (o contrato mudou por decisão aprovada, as asserções não enfraquecem):

- Re-apontados para as rotas novas, mesmas asserções: C6 (lista de rotas protegidas passa a ser a deste plano), C10 (rotas `{id}` × {outro, inexistente}, agora também em `/api/organizations/{id}*`), C12-C20, C37, C40 (upload em `/api/organizations/{id}/documents`), C22-C28 (ask cria a IA dentro de uma organização)
- Substituídos: C9 (`GET /api/assistants` sai) por C3 deste plano. C11 (apagar IA apagava documentos) por C12 e C49 deste plano: documentos pertencem à organização. C44 (`documentCount` em `GET /api/assistants/{id}`) por C9 deste plano, cujo Surface não tem mais esse campo
- Telas do rag-mvp `/assistants` e `/assistants/{id}`: C30-C35, C47-C49, C51 migram para as telas `Organizações`/`Organização`/`IA` e são reprovadas por C16-C18 e C60 deste plano

## Checks

### S1 - Organizações e documentos compartilhados · ~22 files · ~95 KB · ~24k

**C1** - `POST /api/organizations` com `name` de 1 e de 100 caracteres (com espaços nas pontas) responde `201`, `Location: /api/organizations/{id}` e corpo com `id`, `name` sem espaços nas pontas e `createdAt` (AC 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationsTests.Create_valid_returns_201"`

**C2** - `name` `""`, `"   "` e com 101 caracteres respondem `400` com `errors.name` (AC 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationsTests.Create_invalid_returns_400"`

**C3** - `GET /api/organizations` devolve `[]` para usuário novo; depois de criar A e B e, em A, 2 IAs e 1 documento, devolve `[B, A]` com `assistantCount` 0/2, `documentCount` 0/1, `name` e `createdAt` iguais aos da criação, e nunca as de outro usuário (AC 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationsTests.List_returns_only_own_newest_first_with_counts"`

**C4** - `GET /api/organizations/{id}` do dono responde `200` com `id`, `name`, `createdAt`, `documentCount` e `assistants[{id, name, routingDescription}]` iguais ao que foi criado (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationsTests.Get_returns_assistants_and_document_count"`

**C5** - Nas 5 rotas `/api/organizations/{id}*` (`GET`, `DELETE`, `POST documents`, `GET documents`, `DELETE documents/{documentId}`), um id de outro usuário e um id inexistente respondem `404` problem details, e a organização alheia continua com os mesmos documentos (AC 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationsTests.Foreign_or_missing_organization_returns_404"`

**C6** - `POST /api/assistants` com `organizationId` próprio, `name`, `instructions` e `routingDescription` de 500 caracteres responde `201` com `id`, `organizationId`, `name`, `instructions`, `routingDescription`, `createdAt`; com `routingDescription` ausente responde `201` com `routingDescription = null` (AC 5) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Create_valid_returns_201_with_location"`

**C7** - `POST /api/assistants` sem `organizationId` responde `400` com `errors.organizationId`; `routingDescription` com 501 caracteres responde `400` com `errors.routingDescription`; as validações de `name`/`instructions` do rag-mvp continuam (AC 6, AC 8) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Create_invalid_returns_400_keyed_by_field"`

**C8** - `POST /api/assistants` com `organizationId` de outro usuário e com um id inexistente responde `404`, e o número de IAs no banco não muda (AC 7) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Create_in_foreign_or_missing_organization_returns_404"`

**C9** - `GET /api/assistants/{id}` do dono responde `200` com `id`, `organizationId`, `organizationName`, `name`, `instructions`, `routingDescription` e `createdAt` iguais aos da criação (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Get_returns_organization_and_routing_description"`

**C10** - Um documento enviado uma vez à organização aparece com o mesmo `documentId` nas `sources` de duas IAs diferentes dessa organização (AC 9) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Assistants_of_same_organization_share_documents"`

**C11** - Com duas organizações do mesmo usuário, cada uma com um documento, o ask a uma IA da organização A nunca cita o documento de B, mesmo quando o documento de B é mais parecido com a pergunta (AC 10, door 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests.Retrieves_only_from_own_organization"`

**C12** - `DELETE /api/organizations/{id}` responde `204` e no banco não resta linha de `assistants`, `documents`, `chunks` nem `gaps` dessa organização; as de outra organização continuam (AC 13) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationsTests.Delete_returns_204_and_cascades"`

**C13** - O mesmo conteúdo enviado duas vezes à mesma organização responde `201` e depois `409`; enviado a outra organização responde `201` (AC 11) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Same_content_is_unique_per_organization"`

**C14** - Os testes de upload do rag-mvp (C12-C20, C37: formatos, `413`/`415`/`422`/`400`/`502`, lista, apagar, corrida de duplicata) passam contra `/api/organizations/{id}/documents` sem mudar as asserções (AC 12) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests"`
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Concurrent_duplicate_uploads_yield_one_201_one_409"`

**C15** - `POST /api/assistants/{id}/documents`, `GET /api/assistants/{id}/documents`, `DELETE /api/assistants/{id}/documents/{x}` e `GET /api/assistants` respondem `404` (ou `405` para o método sem rota) com `application/problem+json` (AC 15) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~DocumentsTests.Old_assistant_document_routes_are_gone"`

**C16** - Migração: num banco migrado até `InitialCreate` com dois usuários, três IAs (uma sem documento) e dois documentos com chunks, migrar até o fim cria 3 organizações com o nome e o dono de cada IA, cada IA com `organization_id` da sua, cada documento na organização da IA de origem, com os mesmos `id` de documento e chunks intactos (AC 14, door 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MigrationTests.Backfill_creates_one_organization_per_assistant"`

**C17** - Schema final: índice único `(organization_id, content_sha256)` em `documents`; `documents.assistant_id` e `assistants.owner_id` não existem; `assistants.organization_id` e `documents.organization_id` NOT NULL com `ON DELETE CASCADE`; `assistants.routing_description` `varchar(500)` nula (door 1, door 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MigrationTests.Schema_has_organization_ownership_shape"`

### S2 - Jev · ~8 files · ~40 KB · ~10k

**C18** - Com IAs do usuário em duas organizações, uma sem `routingDescription` e outra com `routingDescription = "   "`, o prompt recebido pelo cliente `router` contém o nome, a organização e a descrição só das IAs com descrição não vazia, e nunca IAs de outro usuário (AC 19) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Router_sees_only_own_assistants_with_description"`

**C19** - Com o roteador devolvendo `{"choice": 2, "confident": true}`, `POST /api/jev/ask` responde `200` com `kind = "answered"`, `assistant.id`/`name`/`organizationName` da 2ª IA oferecida, `answer` e `found` do chat padrão, `sources` com o documento da organização dela, e `alternatives` com as demais elegíveis (até 3, sem a escolhida) (AC 20) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Confident_choice_answers_through_chosen_assistant"`

**C20** - Com 5 IAs elegíveis e escolha confiante, `alternatives` tem exatamente 3 itens (AC 20) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Alternatives_are_capped_at_three"`

**C21** - Com `{"choice": 1, "confident": false}`, responde `200` com `kind = "clarify"`, `candidates` de 1 a 3 IAs elegíveis, sem `answer`, e o chat padrão não recebe nenhuma chamada (AC 21) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Low_confidence_returns_clarify_without_answering"`

**C22** - Com `{"choice": null, "confident": true}`, responde `200` com `kind = "noMatch"` e existe 1 lacuna aberta do usuário com aquela pergunta, `organization_id` e `assistant_id` nulos (AC 22) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.No_match_records_gap_without_organization"`

**C23** - Para cada falha do roteador - exceção do cliente, texto que não é JSON, `choice = 0`, `choice = N+1` - responde `200` com `kind = "clarify"` e `candidates` = as IAs elegíveis ordenadas por nome, no máximo 5 (com 6 elegíveis, 5 itens) (AC 23, door 5) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Router_failure_falls_back_to_clarify"`

**C24** - Sem nenhuma IA elegível (usuário sem IAs, e usuário só com IAs sem descrição), responde `422` problem details e o cliente `router` não recebe chamada (AC 24) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.No_eligible_assistant_returns_422"`

**C25** - `question` vazia, só espaços e com 2001 caracteres responde `400` com `errors.question`; com 2000 caracteres não responde `400` (AC 25) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Question_bounds"`

**C26** - 10 perguntas em `/api/assistants/{id}/ask` + 10 em `/api/jev/ask` passam; a 21ª (em `/api/jev/ask`) responde `429` problem details (AC 26) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Shares_rate_limit_with_ask"`

**C27** - Com instruções e documento contendo marcadores únicos, nenhuma mensagem recebida pelo cliente `router` contém o marcador das instruções nem o do documento (AC 27) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Router_never_receives_instructions_or_documents"`

**C28** - Quando o ask depois do roteamento falha no chat padrão, `POST /api/jev/ask` responde `502` problem details sem a mensagem do provedor (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Answer_failure_after_routing_returns_502"`

**C29** - Sem `AI:OpenRouter:ApiKey`, a app sobe e `POST /api/jev/ask` com IA elegível responde `200` `kind = "clarify"` (door 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UnconfiguredAiTests.Jev_without_router_key_falls_back_to_clarify"`

**C30** - Tela `Jev`, `kind=answered`: mostra "Respondido por Direto · ACME", a resposta, e um botão por alternativa; clicar em "Professor" chama `POST /api/assistants/{professorId}/ask` com a mesma pergunta e passa a mostrar "Respondido por Professor · ACME" e a nova resposta (AC 28)
Proof: `npm --prefix src/web run test -- -t "jev answered shows who answered and switches"`

**C31** - Tela `Jev`, `kind=clarify`: mostra "Qual destas IAs deve responder?" e um botão por candidato; clicar pergunta àquela IA e mostra "Respondido por <nome>" (AC 29)
Proof: `npm --prefix src/web run test -- -t "jev clarify lets the user pick"`

**C32** - Tela `Jev`, `kind=noMatch`: mostra "Nenhuma IA sabe responder isso ainda. A pergunta foi para Lacunas." (AC 30)
Proof: `npm --prefix src/web run test -- -t "jev no match points to gaps"`

**C33** - Tela `Jev`, `422`: mostra "Preencha 'Quando usar esta IA' em pelo menos uma IA para usar o Jev" e um link para `/organizations` (AC 31)
Proof: `npm --prefix src/web run test -- -t "jev without eligible assistants explains how to enable"`

**C34** - Tela `Jev`: enquanto pendente mostra "Jev está escolhendo…" e o botão fica desabilitado; `429` e `502` mostram o `title` do problem details e mantêm a pergunta digitada (AC 32)
Proof: `npm --prefix src/web run test -- -t "jev loading and error states"`

### S3 - Lacunas · ~10 files · ~45 KB · ~11k

**C35** - Com o chat devolvendo `{"answer": "não sei", "found": false}`, `POST /api/assistants/{id}/ask` responde `200` com `answer = "não sei"`, `found = false`, e existe 1 lacuna aberta com `organization_id` e `assistant_id` daquela IA, `ask_count = 1` e a pergunta como enviada; com `found: true` responde `found = true` e não cria lacuna (AC 33, door 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Unanswered_question_opens_gap"`

**C36** - Com o roteador escolhendo uma IA e o chat devolvendo `found: false`, `POST /api/jev/ask` responde `kind = "answered"`, `found = false` e cria a lacuna com a organização e a IA escolhidas (AC 33) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Jev_routed_unanswered_question_opens_gap"`

**C37** - "Qual o horário?" e depois "  QUAL o   horário? " sem resposta na mesma organização resultam em 1 lacuna com `askCount = 2` e `lastAskedAt` maior que `firstAskedAt`; a mesma pergunta numa outra organização cria outra lacuna; duas `noMatch` iguais do Jev resultam em 1 lacuna sem organização com `askCount = 2` (AC 34) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Same_normalized_question_increments_count"`

**C38** - Duas perguntas iguais sem resposta disparadas ao mesmo tempo resultam em exatamente 1 lacuna aberta com `ask_count = 2` (AC 34, door 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Concurrent_same_question_yields_one_gap"`

**C39** - Com o chat devolvendo texto que não é o JSON `{answer, found}`, o ask responde `200` com o texto bruto em `answer`, `found = true`, e nenhuma lacuna é criada (AC 35) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Unstructured_chat_reply_is_answered_without_gap"`

**C40** - `GET /api/gaps` devolve só as lacunas abertas do usuário, com `id`, `question`, `askCount`, `firstAskedAt`, `lastAskedAt`, `organization{id, name}` e `assistant{id, name}` (nulos quando não há), ordenadas por `askCount` decrescente e depois `lastAskedAt` decrescente; respondidas, dispensadas e de outro usuário não aparecem (AC 36) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.List_returns_own_open_gaps_most_asked_first"`

**C41** - `POST /api/gaps/{id}/answer` com `answer` de 1 e de 4000 caracteres responde `200` com `id`, `status = "answered"` e `documentId`; esse documento aparece em `GET /api/organizations/{orgId}/documents` com `fileName` = `Lacuna - ` + os primeiros 60 caracteres da pergunta + `.md`, e a lacuna sai de `GET /api/gaps` (AC 37, door 7) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Answer_creates_document_and_closes_gap"`

**C42** - Depois de responder a lacuna, a mesma pergunta feita a outra IA da mesma organização cita o `documentId` da resposta em `sources`, e o texto do trecho contém a resposta (AC 38) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Answered_gap_becomes_retrievable_knowledge"`

**C43** - Lacuna sem organização: responder sem `organizationId` dá `400` com `errors.organizationId`; com `organizationId` de outro usuário dá `404`; com `organizationId` próprio dá `200` e o documento fica nessa organização (AC 39) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Gap_without_organization_requires_one"`

**C44** - `answer` vazio, só espaços e com 4001 caracteres responde `400` com `errors.answer`, e a lacuna continua aberta (AC 40) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Answer_bounds"`

**C45** - `answer` e `dismiss` numa lacuna já respondida e numa já dispensada respondem `409` problem details (AC 41) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Closed_gap_returns_409"`

**C46** - Com o embedding falhando na resposta, `answer` responde `502` sem a mensagem do provedor, a lacuna continua em `GET /api/gaps` e a organização não ganha documento (AC 42) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Embedding_failure_keeps_gap_open"`

**C47** - `POST /api/gaps/{id}/dismiss` numa lacuna aberta responde `204` e ela sai de `GET /api/gaps`; no banco o `status` é `dismissed` (AC 43) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Dismiss_closes_gap"`

**C48** - `answer` e `dismiss` com o id de uma lacuna de outro usuário e com um id inexistente respondem `404`, e a lacuna alheia continua aberta (AC 44) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Foreign_or_missing_gap_returns_404"`

**C49** - Apagar uma IA com lacuna aberta responde `204` e a lacuna continua em `GET /api/gaps` com `assistant = null` e a mesma `organization` (AC 45) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~GapsTests.Deleting_assistant_keeps_its_gaps"`

**C50** - Depois de um ask com `found=false`, um Jev `answered`, um Jev `noMatch` e uma resposta de lacuna, cada um com marcadores únicos na pergunta, na resposta e na saída do roteador, nenhum log capturado contém qualquer marcador; a criação de lacuna gera um log com `GapId` (AC 46, observability) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~CrossCuttingTests.Jev_and_gaps_never_log_content"`

**C51** - Menu: com `GET /api/gaps` devolvendo 2 lacunas mostra "Lacunas (2)"; com `[]` mostra "Lacunas" sem número (AC 47)
Proof: `npm --prefix src/web run test -- -t "nav shows open gap count"`

**C52** - Tela `Lacunas` com `[]` mostra "Nenhuma lacuna. Suas IAs responderam tudo o que foi perguntado." (AC 48)
Proof: `npm --prefix src/web run test -- -t "gaps empty state"`

**C53** - Tela `Lacunas`: uma lacuna sem organização não envia a resposta enquanto nenhuma organização é escolhida no seletor; escolhida, envia `answer` e `organizationId`, e depois do `200` o item sai da lista. Uma lacuna com organização envia sem seletor (AC 49)
Proof: `npm --prefix src/web run test -- -t "gaps answer requires organization when missing"`

**C54** - Tela `Lacunas`: dispensar abre confirmação com "A pergunta sai da lista e não volta"; cancelar não chama `dismiss`; confirmar chama (AC 50)
Proof: `npm --prefix src/web run test -- -t "gaps dismiss confirms"`

**C55** - Tela `Lacunas`: mostra "Carregando lacunas…" enquanto a lista não responde; problem details na lista e na resposta mostram o `title` (AC 51)
Proof: `npm --prefix src/web run test -- -t "gaps loading and error states"`

### S4 - Telas de organização · ~6 files · ~35 KB · ~9k

**C56** - Tela `/organizations` com `[]` mostra "Crie sua primeira organização" e o formulário de criação; criar navega para `/organizations/{id}` (AC 16)
Proof: `npm --prefix src/web run test -- -t "organizations empty state and create"`

**C57** - Tela `/organizations/{id}` lista os `fileName` dos documentos e os nomes das IAs; tem upload e o formulário de IA com o campo "Quando usar esta IA", que envia `routingDescription` e `organizationId` (AC 17)
Proof: `npm --prefix src/web run test -- -t "organization page lists documents and assistants"`

**C58** - Apagar organização abre confirmação que diz que IAs, documentos e lacunas serão apagados; cancelar não chama `DELETE`; confirmar chama `DELETE /api/organizations/{id}` (AC 18)
Proof: `npm --prefix src/web run test -- -t "organization delete confirms"`

**C59** - Tela `/organizations/{id}` sem IAs mostra "Nenhuma IA nesta organização"; `404` mostra o `title` do problem details; enquanto carrega mostra "Carregando..." (Observable `Organização`)
Proof: `npm --prefix src/web run test -- -t "organization page empty, loading and not found"`

**C60** - Tela `/assistants/{id}` mostra o nome da organização com link para ela, a caixa de pergunta, e não mostra upload de documento; a resposta mostra as fontes (Observable `IA`)
Proof: `npm --prefix src/web run test -- -t "assistant page asks and links to organization"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/organizations` statuses (3) | 201 C1 · 400 C2 · 401 C61 | - |
| `GET /api/organizations` statuses (2) | 200 C3 · 401 C61 | - |
| `GET /api/organizations/{id}` statuses (3) | 200 C4 · 401 C61 · 404 C5 | - |
| `DELETE /api/organizations/{id}` statuses (3) | 204 C12 · 401 C61 · 404 C5 | - |
| `POST /api/organizations/{id}/documents` statuses (9) | 201 C13 · 400 C14 · 401 C61 · 404 C5 · 409 C13 · 413 C14 · 415 C14 · 422 C14 · 502 C14 | - |
| `GET /api/organizations/{id}/documents` statuses (3) | 200 C14 · 401 C61 · 404 C5 | - |
| `DELETE /api/organizations/{id}/documents/{documentId}` statuses (3) | 204 C14 · 401 C61 · 404 C5 | - |
| `POST /api/assistants` statuses (4) | 201 C6 · 400 C7 · 401 C61 · 404 C8 | - |
| `GET /api/assistants/{id}` statuses (3) | 200 C9 · 401 C61 · 404 C62 | - |
| `POST /api/assistants/{id}/ask` statuses (6) | 200 C35 · 400 C62 · 401 C61 · 404 C62 · 429 C26 · 502 C62 | - |
| `POST /api/jev/ask` statuses (6) | 200 C19 · 400 C25 · 401 C61 · 422 C24 · 429 C26 · 502 C28 | - |
| `GET /api/gaps` statuses (2) | 200 C40 · 401 C61 | - |
| `POST /api/gaps/{id}/answer` statuses (6) | 200 C41 · 400 C44 · 401 C61 · 404 C48 · 409 C45 · 502 C46 | - |
| `POST /api/gaps/{id}/dismiss` statuses (4) | 204 C47 · 401 C61 · 404 C48 · 409 C45 | - |
| rotas removidas (4) | `POST .../documents` C15 · `GET .../documents` C15 · `DELETE .../documents/{x}` C15 · `GET /api/assistants` C15 | - |
| `kind` do Jev (3) | `answered` C19 · `clarify` C21 · `noMatch` C22 | - |
| falhas do roteador (5) | exceção C23 · não-JSON C23 · índice 0 C23 · índice N+1 C23 · sem chave C29 | - |
| elegibilidade (4) | com descrição C18 · descrição nula C18 · só espaços C18 · outro usuário C18 | - |
| saída do chat do ask (3) | `found=true` C35 · `found=false` C35 · não-JSON C39 | - |
| origem da lacuna (3) | ask direto C35 · Jev roteado C36 · Jev `noMatch` C22 | - |
| agrupamento da lacuna (4) | mesma pergunta normalizada C37 · outra organização C37 · sem organização C37 · simultânea C38 | - |
| transições de `Gap` (5) | nasce `open` C35 · `open`→`answered` C41 · `open`→`dismissed` C47 · `answered`→× C45 · `dismissed`→× C45 | - |
| `name` da organização (5 bordas) | vazio C2 · só espaços C2 · 1 C1 · 100 C1 · 101 C2 | - |
| `routingDescription` (3 bordas) | ausente C6 · 500 C6 · 501 C7 | - |
| `organizationId` na criação de IA (3) | ausente C7 · alheio C8 · inexistente C8 | - |
| `question` do Jev (4 bordas) | vazio C25 · só espaços C25 · 2000 C25 · 2001 C25 | - |
| `answer` da lacuna (5 bordas) | vazio C44 · só espaços C44 · 1 C41 · 4000 C41 · 4001 C44 | - |
| `organizationId` na resposta da lacuna (3) | ausente C43 · alheio C43 · próprio C43 | - |
| limites de lista (2) | `alternatives` ≤ 3 C20 · fallback ≤ 5 C23 | - |
| rotas `{id}` novas × {outro, inexistente} (14) | org GET outro C5 · org GET inexistente C5 · org DELETE outro C5 · org DELETE inexistente C5 · POST docs outro C5 · POST docs inexistente C5 · GET docs outro C5 · GET docs inexistente C5 · DELETE doc outro C5 · DELETE doc inexistente C5 · answer outro C48 · answer inexistente C48 · dismiss outro C48 · dismiss inexistente C48 | - |
| rotas protegidas sem sessão (14) | C61, table-driven sobre as 14 rotas desta feature | - |
| telas (5) | `Organizações` C56, C58 · `Organização` C57, C59 · `IA` C60 · `Jev` C30-C34 · `Lacunas` C51-C55 | - |
| estados por tela do Observable (13) | `Organizações` vazio C56 · `Organizações` apagar confirma C58 · `Organização` vazio C59 · `Organização` carregando C59 · `Organização` 404 C59 · `Jev` answered C30 · `Jev` clarify C31 · `Jev` noMatch C32 · `Jev` vazio C33 · `Jev` carregando/erro C34 · `Lacunas` vazio C52 · `Lacunas` carregando/erro C55 · `Lacunas` dispensar confirma C54 | - |
| doors (7) | 1 C10, C11, C16, C17 · 2 C5, C48, C18 · 3 C6, C17 · 4 C29, C18, C27 · 5 C19, C23 · 6 C35, C38 · 7 C41, C42 | - |
| entidades (5) | `Organization` C1, C12 · `Assistant` C6, C49 · `Document` C13, C41 · `Chunk` C12, C16 · `Gap` C35, C40 | - |
| startup config: cliente `router` keyed (2 assemblies) | `Program.cs` via `AddAi` C29 · `ApiFactory` substitui o keyed C18 | - |

**C61** - Cada uma das 14 rotas desta feature (`POST/GET /api/organizations`, `GET/DELETE /api/organizations/{id}`, `POST/GET /api/organizations/{id}/documents`, `DELETE /api/organizations/{id}/documents/{d}`, `POST /api/assistants`, `GET /api/assistants/{id}`, `POST /api/assistants/{id}/ask`, `POST /api/jev/ask`, `GET /api/gaps`, `POST /api/gaps/{id}/answer`, `POST /api/gaps/{id}/dismiss`) chamada sem cookie responde `401`, nunca `302` ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Protected_route_without_session_returns_401"`

**C62** - Os testes de ask e de IA do rag-mvp (C10 para `GET /api/assistants/{id}` e `ask`, C22-C28: `200` com top-5, `400` de `question`, `404` alheio/inexistente, `502` sem vazar mensagem, `429` no 21º) passam com a IA criada dentro de uma organização, sem mudar as asserções ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests"`
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AssistantsTests.Foreign_or_missing_assistant_returns_404"`

- Claims que nomeiam status, rota ou shape: todas cruzam a fronteira HTTP via `WebApplicationFactory` contra Postgres real; os de tela via Testing Library + MSW
- C16 e C17 são as únicas provas no nível do schema (migração), porque a migração não tem fronteira HTTP

## Test policy

O rag-mvp já respondeu as duas perguntas em `.specs/features/rag-mvp/checks.md` (`## Test policy`), e esta feature segue as mesmas linhas. Uma linha nova:

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Migração com backfill | uma contra Postgres real, migrando de `InitialCreate` com dados e depois até o fim | cada forma de dado de origem (IA com e sem documento, dois donos) e o schema final |

Evidence:

- `JevAsk`: decide entre 3 `kind`, 4 falhas do roteador, elegibilidade e limites → decide, provado na fronteira (C18-C29)
- upsert de lacuna: 3 origens × agrupamento × concorrência → decide, provado na fronteira (C35-C38)
- migração: sem análogo no repo (só `InitialCreate`)

Cost: 1 classe de teste nova de migração. As linhas **não** vão para as guidelines do repo.

## Swept

- validation: C2, C7, C25, C44, C43
- failure modes: C23, C28, C39, C46
- idempotency: C37 - a mesma pergunta repetida soma no contador em vez de criar outra lacuna; C13 para conteúdo duplicado
- authorization: C5, C8, C18, C48, C61; rate limit C26
- concurrency: C38
- data lifecycle: C12, C49, C16
- dependency failure: C23, C29, C46
- state transitions: C35, C41, C45, C47
- observability: C50

## Handoff

- Leitura: código atual ≈ 122 KB ≈ 30k (Api + testes + web). Escrita: S1 ≈ 24k, S2 ≈ 10k, S3 ≈ 11k, S4 ≈ 9k = 54k. Saída de `dotnet test`/`vitest` ≈ 15k. Total ≈ 99k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
