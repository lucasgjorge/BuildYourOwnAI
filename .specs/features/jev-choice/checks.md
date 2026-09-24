# Jev via primitiva de escolha checks

Profile: standard
Plan: `.specs/features/jev-choice/plan.md`

10 checks in 1 slice · 2 one-way doors · 0 open

Comando de prova: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"` (PowerShell com `DOCKER_HOST`, ver AGENTS.md).

Checks do jev-gaps/org-chat afetados: o fake do roteador passa de `IChatClient` (JSON `{choice, confident}`) para `IJevChoice` (`choice`/`confidence`), com os mesmos marcadores `__ROUTE_x__`. As asserções dos testes existentes do Jev não mudam.

## Checks

### S1 - Jev escolhe de verdade · ~8 files · ~45 KB · ~11k

**C1** - Com IAs elegíveis "Culture" e "RH" em "Nexora", o `IJevChoice` recebe `state` = a pergunta, as chaves `["1", "2", "nenhuma"]`, "1" com "Culture", "Nexora" e a descrição da Culture, "2" com "RH", "Nexora" e a descrição do RH (AC 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevChoiceTests.Sends_question_and_one_option_per_assistant_plus_none"`

**C2** - Escolha "2" com `confidence = 0.9` e probabilidades A=0.02, B=0.9, C=0.03, D=0.05 responde `answered` com B e `alternatives` = [D, C, A] (AC 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevChoiceTests.Confident_choice_answers_with_alternatives_by_probability"`

**C3** - Escolha "nenhuma" com confiança >= 0.6 responde `noMatch` e grava a lacuna (global e por organização) (AC 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.No_match_records_gap_without_organization"`
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.No_match_records_gap_in_organization"`

**C4** - `confidence = 0.59` responde `clarify` com as IAs em ordem de probabilidade (a mais provável primeiro, até 3) e o chat de resposta não é chamado; `confidence = 0.6` já responde `answered` (AC 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevChoiceTests.Below_threshold_clarifies_by_probability"`

**C5** - Exceção do `IJevChoice`, escolha "xyz", "0" e "N+1" respondem o fallback `clarify` com até 5 IAs por nome (AC 5) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Router_failure_falls_back_to_clarify"`

**C6** - `OpenRouterJevChoice` faz `POST https://openrouter.ai/api/v1/systemone` com `Authorization: Bearer <chave>` e corpo com `model`, `state`, `questions.principal.type = "choice"`, `instructions` e `criteria`; devolve `choice`, `confidence` e `probabilities` lidos de `answers.principal` (AC 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OpenRouterJevChoiceTests.Posts_choice_request_and_reads_principal_answer"`

**C7** - Resposta `400` com corpo contendo um marcador, e resposta `200` sem `answers.principal`, lançam exceção cuja mensagem não contém o marcador nem a chave (AC 7) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OpenRouterJevChoiceTests.Failure_throws_without_body_or_key"`

**C8** - Nada enviado ao `IJevChoice` contém o marcador das instruções nem o do documento (AC 8) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~JevTests.Router_never_receives_instructions_or_documents"`

**C9** - Nenhum log contém a pergunta nem as respostas depois de Jev `answered` e `noMatch` (AC 9) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~ObservabilityTests.Jev_and_gaps_never_log_content"`

**C10** - Sem `AI:OpenRouter:ApiKey`, o Jev responde o fallback `clarify` (door 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~UnconfiguredAiTests.Jev_without_router_key_falls_back_to_clarify"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| desfechos da decisão (5) | answered C2 · noMatch C3 · clarify abaixo do limiar C4 · fallback por erro C5 · fallback por chave desconhecida C5 | - |
| limiar (2 bordas) | 0.59 C4 · 0.6 C4 | - |
| respostas do HTTP (3) | 2xx com principal C6 · não-2xx C7 · 2xx sem principal C7 | - |
| doors (2) | 1 C6, C7, C10 · 2 C1, C2, C4 | - |
| startup config: `IJevChoice` (2 assemblies) | `Program.cs` via `AddAi` C10 · `ApiFactory` substitui pelo fake C1 | - |

## Swept

- validation: C5 (chave fora das enviadas)
- failure modes: C5, C7
- idempotency: n/a - a chamada ao Jev não grava nada além da lacuna, já idempotente (jev-gaps)
- authorization: existing - escopo e 404 por organização do org-chat, a mesma consulta de elegíveis
- concurrency: n/a - nenhuma escrita nova
- data lifecycle: n/a - nada persistido
- dependency failure: C5, C7, C10
- state transitions: n/a - nenhuma entidade com estado
- observability: C9

## Handoff

- Leitura: `JevAsk`, registro de IA, fakes e testes do Jev ≈ 60 KB ≈ 15k. Escrita ≈ 25 KB ≈ 6k. Saída de testes ≈ 5k. Total ≈ 26k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
- **Boundary:** C1-C10 fechados no commit da Api (este)
- **Settled mid-build:** o usuário apontou o app `C:\reserve\aria` como referência do contrato do `jev-latest`
- **Abandoned:** o fake do roteador dava ao escolhido a confiança e dividia o resto igualmente; com `LOW1` e duas outras opções, as outras ficavam mais prováveis que a escolhida. Agora o escolhido é sempre o mais provável, como na primitiva real
