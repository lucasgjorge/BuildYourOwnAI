# Chat da organização com Jev

## Problem

Para perguntar algo à própria IA, o usuário hoje passa por quatro telas: lista de organizações,
página da organização (documentos e IAs), clique na IA, página de pergunta. A pergunta, que é o
uso principal do produto, fica no fim do caminho, atrás da tela de administração.

Dentro de uma organização com várias IAs (ex.: Nexora Tech com RH, Culture e Tech Team), quem
pergunta precisa saber de antemão qual IA escolher. O Jev que escolhe por ele só existe numa tela
à parte, que mistura as IAs de **todas** as organizações do usuário. Então uma pergunta sobre
férias na Nexora pode ser respondida por uma IA de outra empresa.

A fonte não traz números. A evidência é o relato do usuário em 2026-09-24 ("o fluxo para chegar no
chat está ruim demais"), com o print da página da Nexora Tech.

Depois desta mudança:
- Entrar no produto abre o **chat** de uma organização.
- Cada mensagem vai ao **Jev daquela organização**, que escolhe entre as IAs dela. O usuário também
  pode fixar uma IA específica.
- A conversa aparece como thread enquanto a tela está aberta.
- Documentos e IAs ficam numa aba ao lado do chat.

## Flow

Reusa o `JevAsk` e o `AskPipeline` (jev-gaps) inteiros. A rota nova só restringe as IAs elegíveis a
uma organização. O chat do web reusa `useAsk`/`useJev` e os componentes de resposta.

1. `web` abre `/organizations/{id}` = aba **Conversa** (new, no door - placement). A barra lateral lista as organizações (`GET /api/organizations`, exists).
2. Mensagem com alvo "Jev decide" -> `POST /api/organizations/{id}/jev/ask` (door 1) -> `JevAsk` (exists) resolve a `Organization` pelo filtro de dono (AD-010), limita as elegíveis a ela e segue o mesmo caminho (router -> `AskPipeline`).
3. Mensagem com uma IA fixada -> `POST /api/assistants/{id}/ask` (exists).
4. `noMatch` no Jev da organização -> `GapRecorder` (exists) grava a lacuna **com** a organização.
5. A thread vive no estado da tela (door 2). Aba **Base** = documentos + IAs, o conteúdo atual da página da organização.

## Impact

| Front | What changes |
| --- | --- |
| domain | termo existente: `Jev` era "roteador sobre todas as IAs do usuário". Passa a ter dois escopos: por organização (novo, principal) e global (mantido em `/jev`, escolha do usuário). Quem depende do escopo global: `POST /api/jev/ask`, a tela `/jev` e os testes `JevTests` do jev-gaps, que não mudam |
| domain | termo novo: `Conversa` - a thread de mensagens de uma organização na tela. Não é entidade: fica só no navegador (door 2) |
| contrato | rota nova `POST /api/organizations/{id}/jev/ask`, com a mesma resposta de `POST /api/jev/ask`. Nada existente muda de assinatura |
| web | `/organizations/{id}` deixa de ser a página de administração e vira o chat; a administração vai para `/organizations/{id}/knowledge`. `/assistants/{id}` redireciona para o chat da organização com a IA fixada. A lista `/organizations` vira a barra lateral; `/organizations` sozinho abre a primeira organização, ou a criação quando não há nenhuma. Os testes de tela do jev-gaps (C56-C60) e os do rag-mvp migrados são re-apontados para as telas novas, sem enfraquecer asserções |
| web | identidade visual nova (tokens de cor e tipografia, fontes do Google Fonts) aplicada a todas as telas autenticadas; login e registro recebem os mesmos tokens |
| stored data | nada a migrar |

## Relations

None - no stored-data shape change.

## Surface

| Route | In | Out | Status |
| --- | --- | --- | --- |
| `POST /api/organizations/{id}/jev/ask` | `question` | mesmo shape de `POST /api/jev/ask`: `kind` ∈ {`answered`, `clarify`, `noMatch`} com os mesmos campos | `200`, `400`, `401`, `404`, `422`, `429`, `502` |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. Rota do Jev escopado | `POST /api/organizations/{id}/jev/ask` no grupo de organizações, com `RequireRateLimiting(AskPipeline.RateLimitPolicy)` (mesmo limite de 20/min). Handler único em `JevAsk` com `Guid? organizationId`: nulo = global, preenchido = só IAs com `OrganizationId == id`. Organização alheia ou inexistente responde `404` antes de chamar o roteador | `POST /api/jev/ask` com `organizationId` no corpo: mistura dois escopos numa rota, e um id alheio no corpo viraria "sem IA elegível" (`422`) em vez de `404`, o que revela menos mas quebra o padrão de posse pela rota (AD-010) |
| 2. Conversa só no cliente | a thread é `useState` no componente do chat; não há `conversationId`, nem tabela, nem histórico enviado ao modelo; recarregar limpa. Trocar de organização começa outra thread | Persistir conversas: escolha do usuário, fica para a etapa 2. Fica fechado agora: retomar conversa e multi-turno. Abrir depois exige entidade `Conversation`/`Message` e mudar o prompt do `AskPipeline` |

- Nada mais nesta mudança é difícil de reverter: layout, cores, fontes, textos e rotas do web são código trocável. As rotas do web não são consumidas fora da SPA.

## Criteria

### S1: Jev escopado à organização (P1)

O Jev de uma organização só escolhe IAs dela.

**Acceptance Criteria**

1. WHEN `POST /api/organizations/{id}/jev/ask` recebe uma pergunta THEN the system SHALL oferecer ao roteador só as IAs da organização `{id}` com `routingDescription` não vazia, nunca IAs de outra organização do mesmo usuário
2. WHEN o roteador escolhe uma IA com `confident=true` THEN the system SHALL responder `200` `kind=answered` com a resposta daquela IA pelo mesmo pipeline do ask, e `alternatives` só com IAs da mesma organização
3. WHEN o roteador devolve `choice=null` com `confident=true` THEN the system SHALL responder `200` `kind=noMatch` e registrar a lacuna aberta com `organization_id = {id}` e `assistant_id` nulo
4. IF `{id}` é uma organização de outro usuário ou inexistente THEN the system SHALL responder `404` problem details sem chamar o roteador
5. IF a organização não tem IA elegível (mesmo que outra organização do usuário tenha) THEN the system SHALL responder `422` problem details sem chamar o roteador
6. The system SHALL aplicar em `POST /api/organizations/{id}/jev/ask` as mesmas regras do Jev global: `400` para `question` vazia ou com mais de 2000 caracteres, `clarify` com baixa confiança ou falha do roteador, `502` quando o ask falha depois do roteamento, e o mesmo limite de 20 perguntas/min somado ao ask e ao Jev global (`429`)

**Independent test:** duas organizações com uma IA "RH" cada; perguntar no Jev de uma e ver no prompt do roteador só a IA dela.

### S2: Chat como tela principal (P1)

Entrar no produto e perguntar leva um clique, e a resposta diz quem respondeu.

**Acceptance Criteria**

7. WHEN um usuário logado abre `/organizations` e tem organizações THEN the system SHALL levar para `/organizations/{id}` da primeira organização da lista, com a aba Conversa aberta
8. WHEN um usuário logado sem organizações abre `/organizations` THEN the system SHALL mostrar "Crie sua primeira organização" com o campo de nome, e criar SHALL levar para `/organizations/{id}/knowledge` da nova organização
9. The system SHALL mostrar em toda tela autenticada uma barra lateral com as organizações do usuário (link para o chat de cada uma), "Nova organização", "Jev (todas)", "Lacunas (N)" e "Sair"
10. WHEN o usuário envia uma mensagem com o alvo "Jev decide" THEN the system SHALL chamar `POST /api/organizations/{id}/jev/ask`, mostrar a pergunta na thread e, com `kind=answered`, mostrar a resposta com "Respondido por <IA>" e a marca "via Jev", as fontes e um botão "Perguntar a <IA>" por alternativa
11. WHEN o usuário clica em "Perguntar a <IA>" numa resposta THEN the system SHALL chamar `POST /api/assistants/{iaId}/ask` com a mesma pergunta e **acrescentar** a nova resposta à thread, mantendo a anterior
12. WHEN o usuário fixa uma IA no seletor de alvo e envia THEN the system SHALL chamar `POST /api/assistants/{iaId}/ask` e mostrar a resposta com "Respondido por <IA>" sem a marca "via Jev"
13. WHEN o Jev responde `kind=clarify` THEN the system SHALL mostrar na thread "Qual destas IAs deve responder?" com um botão por candidato, e clicar SHALL acrescentar a resposta daquela IA
14. WHEN o Jev responde `kind=noMatch` THEN the system SHALL mostrar na thread "Nenhuma IA desta organização sabe responder isso ainda. A pergunta foi para Lacunas." com link para `/gaps`
15. WHEN uma resposta tem `found=false` THEN the system SHALL mostrar junto dela "Não encontrado nos documentos - registrado em Lacunas"
16. WHILE uma mensagem está sendo respondida the system SHALL mostrar na thread "Jev está escolhendo…" (alvo Jev) ou "<IA> está respondendo…" (IA fixada) e desabilitar o envio
17. IF uma chamada do chat responde problem details THEN the system SHALL mostrar o `title` na thread como mensagem de erro e devolver o texto ao campo de mensagem
18. IF o Jev da organização responde `422` THEN the system SHALL mostrar na thread "Nenhuma IA desta organização tem 'Quando usar' preenchido" com link para a aba Base
19. WHEN a organização não tem IAs THEN the aba Conversa SHALL mostrar "Esta organização ainda não tem IAs" com link para a aba Base, no lugar do campo de mensagem
20. WHEN a thread está vazia e a organização tem IAs THEN the system SHALL mostrar os nomes das IAs com a `routingDescription` de cada uma, como guia do que perguntar
21. WHEN o usuário abre `/assistants/{id}` THEN the system SHALL levar para `/organizations/{orgId}` com aquela IA fixada no seletor de alvo
22. WHEN o usuário troca de organização na barra lateral THEN the system SHALL mostrar a thread vazia da outra organização

**Independent test:** logar, cair no chat da Nexora, perguntar "como peço férias?" e ver "Respondido por RH · via Jev"; clicar "Perguntar a Culture" e ver as duas respostas na thread.

### S3: Base e telas existentes na identidade nova (P2)

A administração continua completa, fora do caminho do chat.

**Acceptance Criteria**

23. WHEN o usuário abre a aba Base (`/organizations/{id}/knowledge`) THEN the system SHALL mostrar upload e lista de documentos, lista de IAs com a `routingDescription`, criação de IA com "Quando usar esta IA", e os mesmos estados, confirmações e erros das telas do jev-gaps (C57-C59 e os rag-mvp migrados)
24. WHEN o usuário apaga a organização na aba Base THEN the system SHALL pedir confirmação dizendo que IAs, documentos e lacunas serão apagados e, confirmado, levar para `/organizations`
25. The system SHALL manter a tela `/jev` (Jev global) com o mesmo comportamento do jev-gaps (C30-C34) usando o mesmo componente de thread do chat da organização
26. The system SHALL manter a tela `/gaps` com o comportamento do jev-gaps (C51-C55)
27. The system SHALL dar foco visível por teclado a todo controle interativo e respeitar `prefers-reduced-motion` desligando as animações da thread

**Independent test:** abrir a aba Base, subir um documento, criar uma IA com "Quando usar", voltar para Conversa e ver a IA no guia da thread vazia.

## Out of scope

| Excluded | Why |
| --- | --- |
| Histórico persistido, lista de conversas, multi-turno | escolha do usuário: fica na etapa 2 |
| Streaming da resposta | etapa 2 |
| Editar IA ou organização | não existe edição ainda |
| Tema escuro | não pedido; tokens ficam prontos para ele |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Organização aberta em `/organizations` | a primeira da lista (mais recente) | sem "última visitada" persistida; é determinístico | n |
| Onde cai a criação de organização | na aba Base, porque sem documentos e IAs o chat não tem o que fazer | caminho natural do primeiro uso | n |
| Alvo padrão do seletor | "Jev decide" | é a proposta da tela | n |
| Thread ao trocar de organização | começa vazia; voltar para a anterior também começa vazia | door 2: sem estado entre organizações | n |
| Cor por IA na thread | cor derivada da posição da IA na organização, de uma paleta fixa de 6 | identifica quem respondeu sem configuração | n |

**Open questions:** none - all resolved or logged above.

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| screen `Conversa` | empty (sem IAs) | AC 19 |
| screen `Conversa` | empty (thread vazia) | AC 20 |
| screen `Conversa` | loading | AC 16 |
| screen `Conversa` | error | AC 17, 18 |
| screen `Conversa` | unauthorised | existing - `RequireAuth` |
| screen `Conversa` | ordering | thread em ordem de envio (AC 11: acrescenta ao fim) |
| screen `Conversa` | destructive action | n/a - o chat não apaga nada |
| screen `Base` | todos os estados | AC 23, 24 (herdados do jev-gaps) |
| screen `Organizações` (vazia) | empty | AC 8 |
| barra lateral | conteúdo e ordem | AC 9; ordem = `GET /api/organizations` (mais recente primeiro) |
| API `POST /api/organizations/{id}/jev/ask` | error shape and codes | AC 4, 5, 6 |
| API `POST /api/organizations/{id}/jev/ask` | rate limit | AC 6 |
| API `POST /api/organizations/{id}/jev/ask` | versioning | n/a - único consumidor é a SPA na mesma origem |

## Sources

- Conversa de 2026-09-24 com print da página da Nexora Tech: "queria que após um input no chat o Jev decidisse para qual IA direcionar"; manter o Jev global; histórico só na sessão
- `.specs/features/jev-gaps/plan.md` - contrato do Jev reusado
