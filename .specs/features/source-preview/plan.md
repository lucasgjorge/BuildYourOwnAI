# Prévia do trecho e escolha automática sem nome próprio

## Problem

Depois de uma resposta, o usuário vê só o nome do arquivo de cada fonte. Para conferir o que a IA
leu, ele precisa abrir o documento fora do produto e procurar o trecho. Quando os 5 trechos vêm do
mesmo arquivo, a lista mostra o mesmo nome 5 vezes ("01_rh (1).txt" repetido, print de 2026-09-24).
Quando a resposta não foi encontrada, a lista "Fontes" aparece mesmo assim, citando trechos que não
sustentam nada.

O nome "Jev" aparece na interface, na documentação, nas rotas e no código como o nome de quem escolhe
a IA. "Jev" vai ser o nome de um negócio (pedido do usuário, 2026-09-24), então o produto não pode
usá-lo como nome próprio de uma função sua.

Há também um defeito de layout: com uma resposta na thread, a página rola na horizontal e a barra
lateral aparece cortada à esquerda (print de 2026-09-24).

Depois desta mudança:
- Clicar numa fonte abre, à direita do chat, o trecho exato em destaque, com os trechos vizinhos para contexto.
- As fontes vêm agrupadas por documento.
- A escolha da IA se chama "escolha automática" em todo lugar.

## Flow

Reusa `RetrieveAsync`/`Source` do `AskPipeline` (a resposta já traz `documentId` e `chunkIndex`) e o
filtro de dono por organização (AD-010). O backend só ganha uma leitura de trechos.

1. Resposta com `found=true` no `Thread` (exists) -> fontes agrupadas por `documentId`; o primeiro trecho abre sozinho no painel de prévia (new, no door - placement)
2. Painel -> `GET /api/organizations/{id}/documents/{documentId}/chunks/{index}` (door 1) -> handler novo em `Features/Documents` resolve a `Organization` pelo filtro de dono, lê o trecho e os vizinhos
3. out: `fileName` · `chunkCount` · `chunks[{index, content}]`, e o painel destaca `index`
4. Renomeação: `Features/Jev/JevAsk` (exists) -> `Features/Routing/RouteAsk`; `IJevChoice` (exists) -> `IRoutingChoice`; rotas `/api/jev/ask` -> `/api/route/ask` e `/api/organizations/{id}/jev/ask` -> `/api/organizations/{id}/route/ask` (door 2)

## Impact

| Front | What changes |
| --- | --- |
| domain | termo existente: "Jev" era o nome de quem escolhe a IA. Passa a ser "escolha automática" (UI) e `Routing` (código). Quem depende hoje: `JevAsk`, `IJevChoice`/`JevChoice`/`OpenRouterJevChoice`, `AiOptions.JevSection`, `Program.cs`, as telas `Thread`, `JevPage`, `HomePage`, `AppLayout`, `OrganizationPage`, os tokens `--color-jev*`, os testes `JevTests`, `OrganizationJevTests`, `JevChoiceTests`, `UnconfiguredAiTests`, `CrossCuttingTests`, e README, PRD, AGENTS, architecture-guardian |
| contrato | rotas `POST /api/jev/ask` e `POST /api/organizations/{id}/jev/ask` saem, entram `POST /api/route/ask` e `POST /api/organizations/{id}/route/ask` com o mesmo corpo e resposta. Único consumidor: a SPA deste repo |
| contrato | rota nova de leitura de trechos (Surface) |
| config | `AI:Jev:*` -> `AI:Routing:*` (`ConfidenceThreshold`, `TimeoutSeconds`). `AI:OpenRouter:Model` continua; o valor (`jev-latest`) é o id do modelo do provedor e fica só na configuração |
| web | rota `/jev` -> `/all` ("Todas as IAs"). A aba Conversa ganha uma segunda coluna em telas largas |
| histórico | `.specs/features/*` e o nome da migration `OrganizationsJevGaps` não mudam: são registro do que foi feito, e renomear uma migration aplicada quebra o histórico do EF |
| stored data | nada a migrar |

## Relations

None - no stored-data shape change.

## Surface

| Route | In | Out | Status |
| --- | --- | --- | --- |
| `GET /api/organizations/{id}/documents/{documentId}/chunks/{index}` | query `around` (0 a 2, padrão 1) | `documentId` · `fileName` · `chunkCount` · `chunks[{index, content}]` (o trecho pedido e até `around` vizinhos de cada lado, em ordem) | `200`, `400`, `401`, `404` |
| `POST /api/route/ask` (era `/api/jev/ask`) | `question` | igual ao anterior | `200`, `400`, `401`, `422`, `429`, `502` |
| `POST /api/organizations/{id}/route/ask` (era `.../jev/ask`) | `question` | igual ao anterior | `200`, `400`, `401`, `404`, `422`, `429`, `502` |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. Leitura de trechos | `GET /api/organizations/{id}/documents/{documentId}/chunks/{index}?around=1`, no grupo de organizações; resolve a organização pelo filtro de dono e o documento dentro dela; devolve `chunks` ordenados por `index`, de `index-around` a `index+around` limitados a `0..chunkCount-1` | Texto completo do trecho dentro de cada `source` da resposta do ask: aumenta toda resposta em até 5 KB e ainda não traz os vizinhos. Rota por `chunkId`: o cliente só conhece `documentId`+`chunkIndex`, e o par é o que a resposta já cita |
| 2. Nome neutro no contrato e no código | rotas `/api/route/ask` e `/api/organizations/{id}/route/ask`; `Features/Routing/RouteAsk.cs`; `IRoutingChoice`, `RoutingChoice`, `OpenRouterRoutingChoice`; `AiOptions.RoutingSection` em `AI:Routing`; tokens `--color-route`/`--color-route-soft`; página `/all`. As rotas antigas saem sem alias | Manter `/api/jev/*` com alias: deixa o nome do negócio no contrato. Mudar só a UI: escolha do usuário foi trocar também código e docs |

- Nada mais nesta mudança é difícil de reverter: layout do painel, textos e agrupamento das fontes são código trocável.

## Criteria

### S1: Prévia do trecho (P1)

Conferir o que a IA leu sem sair do chat.

**Acceptance Criteria**

1. WHEN `GET /api/organizations/{id}/documents/{documentId}/chunks/{index}` é chamado com `around=1` para um trecho do meio THEN the system SHALL responder `200` com `fileName`, `chunkCount` e `chunks` = os trechos `index-1`, `index` e `index+1` com o `content` gravado, em ordem
2. WHEN o trecho pedido é o primeiro ou o último THEN the system SHALL devolver só os vizinhos que existem
3. IF `around` é menor que 0 ou maior que 2 THEN the system SHALL responder `400` com `errors.around`
4. IF a organização é de outro usuário ou inexistente, o documento não é daquela organização, ou `index` está fora de `0..chunkCount-1` THEN the system SHALL responder `404` problem details
5. WHEN uma resposta tem `found=true` e fontes THEN the chat SHALL listar as fontes agrupadas por documento (um nome de arquivo por documento, com um botão por trecho, ex.: "trecho 3") e abrir sozinho, à direita, a prévia do primeiro trecho
6. WHEN o usuário clica num trecho THEN the painel de prévia SHALL mostrar o nome do arquivo, "Trecho N de M" e o trecho em destaque, com os vizinhos antes e depois sem destaque
7. WHEN uma resposta tem `found=false` THEN the chat SHALL não mostrar a lista de fontes e não abrir a prévia
8. WHEN o usuário fecha a prévia THEN the painel SHALL sumir e o chat ocupar a largura toda até o próximo clique numa fonte
9. WHILE a prévia carrega the painel SHALL mostrar "Carregando trecho…". IF a leitura falha THEN SHALL mostrar o `title` do problem details
10. WHILE a tela é estreita (abaixo de 1024 px) the prévia SHALL abrir sobre o chat, com o botão de fechar visível

**Independent test:** perguntar sobre férias na Nexora e ver, à direita, o trecho do `01_rh.txt` que sustenta a resposta.

### S2: Escolha automática sem nome próprio (P1)

O nome do negócio sai do produto.

**Acceptance Criteria**

11. The system SHALL responder em `POST /api/route/ask` e `POST /api/organizations/{id}/route/ask` com o mesmo contrato e os mesmos status que as rotas `jev` tinham
12. IF uma requisição chega a `POST /api/jev/ask` ou `POST /api/organizations/{id}/jev/ask` THEN the system SHALL responder `404` problem details
13. The system SHALL ler o limiar de confiança e o timeout de `AI:Routing:ConfidenceThreshold` e `AI:Routing:TimeoutSeconds`
14. The interface SHALL mostrar "Escolha automática" no seletor "Para", "escolha automática" na resposta roteada, "Escolhendo quem responde…" enquanto a escolha acontece, e "Todas as IAs" (rota `/all`) na barra lateral
15. The interface, o README, o PRD, o AGENTS.md e o architecture-guardian SHALL não conter a palavra "Jev" fora do id do modelo do provedor na configuração
16. The código-fonte da Api e do web SHALL não conter "Jev" em nomes de tipo, rota, arquivo ou chave de configuração (exceto a migration `OrganizationsJevGaps`)

**Independent test:** buscar "jev" em `src/` e nos docs e achar só a migration e o exemplo de valor do modelo.

### S3: Layout sem rolagem lateral (P2)

A barra lateral não some mais pela esquerda.

**Acceptance Criteria**

17. WHEN a thread tem respostas THEN the página SHALL não ter rolagem horizontal: a barra lateral e a área do chat cabem na largura da janela

**Independent test:** abrir o chat numa janela larga, perguntar, e a barra lateral continua inteira.

## Out of scope

| Excluded | Why |
| --- | --- |
| Abrir o documento inteiro | pedido foi o trecho; o documento inteiro é outro visualizador |
| Destacar a frase exata usada na resposta dentro do trecho | exigiria citação por frase no prompt |
| Renomear specs antigos e a migration | histórico; ver Impact |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Vizinhos na prévia | 1 de cada lado | contexto suficiente sem virar o documento inteiro | n |
| Prévia abre sozinha | sim, no primeiro trecho da resposta mais recente com `found=true` | o pedido foi ver o trecho "quando é encontrado" | n |
| Fontes de resposta não encontrada | escondidas | trechos que não sustentam a resposta confundem | n |
| Nome da rota nova | `/api/route/ask` e `/all` | escolha do usuário pelo nome neutro; o termo no código é `Routing` | y |

**Open questions:** none - all resolved or logged above.

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| screen `Conversa` - painel de prévia | loading, error | AC 9 |
| screen `Conversa` - painel de prévia | empty (nada escolhido) | AC 8: sem painel |
| screen `Conversa` - painel de prévia | fechar | AC 8 |
| screen `Conversa` - painel de prévia | tela estreita | AC 10 |
| screen `Conversa` - fontes | agrupamento e duplicatas | AC 5 |
| screen `Conversa` - fontes | resposta não encontrada | AC 7 |
| API `GET .../chunks/{index}` | error shape and codes | AC 3, 4 |
| API `GET .../chunks/{index}` | rate limit | n/a - leitura barata sem IA |
| API `GET .../chunks/{index}` | versioning | n/a - único consumidor é a SPA |

## Sources

- Conversa de 2026-09-24: "parar de referenciar o nome JEV porque vai ser um negócio"; "pré-visualizar o trecho exato do documento no lado direito quando é encontrado"; respostas: sem nome próprio, trocar interface, docs e código
- Print de 2026-09-24 da Nexora Tech: fontes repetidas, fontes em resposta não encontrada, barra lateral cortada
