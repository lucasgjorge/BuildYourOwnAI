# Jev via primitiva de escolha (jev-latest)

## Problem

O Jev nunca escolhe uma IA. Toda pergunta no chat da Nexora Tech ("como peço férias", "quando posso
tirar férias") volta com "Qual destas IAs deve responder?" e as IAs em ordem alfabética (Culture, RH,
Tech Team). É o fallback de falha do roteador: o log mostra `Jev router call failed with
ClientResultException` em todas as chamadas.

A causa é o contrato. O jev-gaps (door 4) chama o roteador como um chat (`/chat/completions` via
`IChatClient`), mas `jev-latest` na OpenRouter não é um modelo de chat. É a primitiva "choice" da
TypeSafe: `POST {base}/systemone` com `state` (o texto) e `questions.<chave>.criteria` (opção ->
descrição), que responde com `choice`, `confidence` e `probabilities`. Evidência: o app
`C:\reserve\aria` (`JevRankingClient`) usa esse contrato, e uma chamada real com as três IAs da
Nexora e "quando posso tirar ferias" devolveu `choice = rh`, `confidence = 1`.

Depois desta mudança, a pergunta sobre férias vai direto para o RH, e o Jev só pergunta ao usuário
quando a confiança fica abaixo do limiar.

## Flow

Reusa `JevAsk` inteiro: elegibilidade, escopo por organização, fallback, lacunas e `AskPipeline`. Só
troca a chamada ao roteador e a leitura da decisão.

1. `JevAsk` (exists) monta `criteria`: `"1".."N"` -> "<nome> (organização: <org>): <quando usar>", mais `"nenhuma"` (door 2)
2. `IJevChoice` (door 1) -> `OpenRouterJevChoice` (door 1) faz `POST systemone` e devolve `choice`, `confidence`, `probabilities`
3. `JevAsk` decide (door 2): `choice` válido com `confidence >= limiar` -> `answered` (ou `noMatch` se `nenhuma`); abaixo do limiar -> `clarify` por probabilidade; erro ou chave desconhecida -> fallback `clarify` (exists)

## Impact

| Front | What changes |
| --- | --- |
| domain | termo existente: "roteador do Jev" era um `IChatClient` keyed `router` que devolvia JSON `{choice, confident}`. Passa a ser `IJevChoice`, com confiança numérica. Quem depende disso hoje: `JevAsk`, `AiServiceCollectionExtensions`, `ApiFactory`/`FakeRouterClient` dos testes, `UnconfiguredAiTests` |
| decisões | AD-011 (IChatClient keyed `router`) é substituída por AD-012. AD-004 continua valendo para chat e embeddings |
| config | `AI:OpenRouter:ApiKey` e `AI:OpenRouter:Model` continuam. Novos: `AI:OpenRouter:BaseAddress` (padrão `https://openrouter.ai/api/v1/`), `AI:Jev:ConfidenceThreshold` (padrão `0.6`), `AI:Jev:TimeoutSeconds` (padrão `10`) |
| contrato | nenhuma rota muda. `clarify` passa a listar os candidatos por probabilidade em vez de nome quando a resposta veio do Jev |
| stored data | nada a migrar |

## Relations

None - no stored-data shape change.

## Surface

None - nothing consumed outside. As rotas `POST /api/jev/ask` e `POST /api/organizations/{id}/jev/ask` mantêm assinatura e status.

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. Abstração do roteador (substitui a door 4 do jev-gaps e AD-011) | `interface IJevChoice { Task<JevChoice> ChooseAsync(string state, string instructions, IReadOnlyDictionary<string, string> criteria, CancellationToken ct); }` com `record JevChoice(string Choice, double Confidence, IReadOnlyDictionary<string, double> Probabilities)`. Implementação `OpenRouterJevChoice` com `HttpClient` tipado: `POST systemone`, corpo `{model, state, questions: {principal: {type: "choice", instructions, criteria}}}`, `Authorization: Bearer`, lê `answers.principal`. Sem chave ou modelo: `UnconfiguredJevChoice`, que lança exceção (vira fallback) | Manter `IChatClient`: o endpoint não é de chat, a chamada sempre falha. `HttpClient` direto no handler: viola AD-004 (handler sem SDK/HTTP de IA) e não dá para simular nos testes |
| 2. Chaves das opções e regra de decisão (substitui a door 5 do jev-gaps) | `criteria` com chaves `"1".."N"` (posição na lista ordenada por nome, nunca o id) e `"nenhuma"` = "Nenhuma dessas IAs: a pergunta é sobre outro assunto". `confidence >= AI:Jev:ConfidenceThreshold` (0.6) com `choice` em `1..N` -> `answered`; com `nenhuma` -> `noMatch`. Abaixo do limiar -> `clarify` com até 3 IAs em ordem de probabilidade. `choice` fora das chaves -> fallback | GUID da IA como chave: um id alucinado ou de outro usuário teria que ser revalidado; a posição elimina isso. Sem opção `nenhuma`: a primitiva sempre escolhe alguém, e nenhuma pergunta chegaria a Lacunas pelo Jev |

- Nada mais nesta mudança é difícil de reverter: textos das descrições, limiar e timeout são configuração.

## Criteria

### S1: Jev escolhe de verdade (P1)

A pergunta vai para a IA que o `jev-latest` escolheu, sem o usuário escolher.

**Acceptance Criteria**

1. WHEN o Jev é chamado THEN the system SHALL enviar ao `IJevChoice` o texto da pergunta como `state`, uma opção `"1".."N"` por IA elegível (em ordem de nome) cuja descrição contém o nome, a organização e a `routingDescription` dela, e a opção `"nenhuma"`
2. WHEN a escolha é `"k"` (1 <= k <= N) com `confidence >= 0.6` THEN the system SHALL responder `kind=answered` com a k-ésima IA, e `alternatives` com as demais IAs elegíveis em ordem decrescente de probabilidade (até 3)
3. WHEN a escolha é `"nenhuma"` com `confidence >= 0.6` THEN the system SHALL responder `kind=noMatch` e registrar a lacuna como hoje
4. WHEN a `confidence` fica abaixo de `0.6` THEN the system SHALL responder `kind=clarify` com até 3 IAs elegíveis em ordem decrescente de probabilidade, sem chamar o chat de resposta
5. IF o `IJevChoice` lança exceção, ou a escolha não é uma das chaves enviadas THEN the system SHALL responder o fallback `clarify` de hoje (até 5 IAs por nome)
6. WHEN `OpenRouterJevChoice` é chamado THEN the system SHALL fazer `POST {BaseAddress}systemone` com `Authorization: Bearer <chave>` e corpo `{model, state, questions: {principal: {type: "choice", instructions, criteria}}}`, e devolver `choice`, `confidence` e `probabilities` de `answers.principal`
7. IF a resposta HTTP não é 2xx ou não traz `answers.principal` THEN `OpenRouterJevChoice` SHALL lançar exceção sem incluir o corpo da resposta nem a chave na mensagem
8. The system SHALL nunca enviar ao `IJevChoice` instruções de IA nem conteúdo de documento (mantém o AC 27 do jev-gaps)
9. The system SHALL nunca escrever em log a pergunta, as descrições enviadas nem a resposta do Jev (mantém o AC 46 do jev-gaps)

**Independent test:** no chat da Nexora, "quando posso tirar férias" responde "Respondido por RH · via Jev".

## Out of scope

| Excluded | Why |
| --- | --- |
| Calibrar o limiar com exemplos reais | 0.6 é o chute inicial do aria; ajustável em config |
| Mostrar as probabilidades na tela | não pedido |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Limiar de confiança | 0.6 | o valor usado no aria para a mesma primitiva | n |
| Timeout da chamada | 10 s | a chamada real levou menos de 1 s; 10 s cobre variação sem prender o request | n |
| Texto da opção `nenhuma` | "Nenhuma dessas IAs: a pergunta é sobre outro assunto" | dá ao modelo uma saída explícita para Lacunas | n |

**Open questions:** none - all resolved or logged above.

## Observable

None - no user-facing surface. As telas do Jev não mudam; só a decisão que chega a elas.

## Sources

- `C:\reserve\aria\Rag\Services\Jev\JevRankingClient.cs` e `Rag/Models/Jev/*` - contrato da primitiva "choice" via OpenRouter
- Chamada real em 2026-09-24: `POST https://openrouter.ai/api/v1/systemone`, `model=jev-latest`, resposta `answers.principal = {choice: "rh", confidence: 1}`
