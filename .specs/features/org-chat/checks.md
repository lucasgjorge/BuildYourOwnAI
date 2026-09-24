# Chat da organização com Jev checks

Profile: standard
Plan: `.specs/features/org-chat/plan.md`

33 checks in 4 slices · 2 one-way doors · 0 open

Comandos de prova:

- API (PowerShell, precisa do Docker Desktop): `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"`
- Web: `npm --prefix src/web run test -- -t "<nome do teste>"`

Checks de tela do jev-gaps afetados (a tela mudou por decisão aprovada, as asserções não enfraquecem):

- C56-C59 (página da organização) e os rag-mvp migrados em `organizations.test.tsx` são re-apontados para a aba Base (`/organizations/{id}/knowledge`). C58 (apagar organização) passa a ser feito na aba Base (AC 24)
- C60 (página da IA) é substituído por C23 deste plano: `/assistants/{id}` redireciona para o chat com a IA fixada
- C30-C31 (Jev global): o botão de alternativa passa a se chamar "Perguntar a <IA>" e a resposta nova é acrescentada à thread (AC 11 e AC 25). As asserções de conteúdo continuam as mesmas

## Checks

### S1 - Jev escopado à organização · ~3 files · ~25 KB · ~6k

**C1** - Com IAs elegíveis em duas organizações do mesmo usuário e uma alheia, o prompt do roteador de `POST /api/organizations/{A}/jev/ask` contém nome e descrição só das IAs elegíveis de A (AC 1) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.Router_sees_only_this_organization"`

**C2** - Com escolha confiante, responde `200` `kind=answered` com a IA escolhida de A, `answer`/`found`/`sources` do documento de A, e `alternatives` só com IAs de A (AC 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.Confident_choice_answers_within_organization"`

**C3** - Com `choice=null` confiante, responde `200` `kind=noMatch` e existe 1 lacuna aberta com a pergunta, `organization_id = A` e `assistant_id` nulo (AC 3) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.No_match_records_gap_in_organization"`

**C4** - Organização de outro usuário e id inexistente respondem `404` problem details, e o roteador não recebe chamada com aquela pergunta (AC 4) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.Foreign_or_missing_organization_returns_404"`

**C5** - Organização sem IA elegível (sem IAs, e só com IA sem descrição) responde `422` problem details mesmo com outra organização do usuário tendo IA elegível, e o roteador não recebe chamada (AC 5) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.No_eligible_in_this_organization_returns_422"`

**C6** - Regras herdadas: `question` vazia e com 2001 caracteres -> `400` com `errors.question`; `confident=false` -> `clarify`; exceção do roteador -> `clarify`; falha do chat depois do roteamento -> `502` sem a mensagem do provedor (AC 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.Keeps_global_jev_rules"`

**C7** - 10 perguntas no ask, 5 no Jev global e 5 no Jev da organização passam; a 21ª (no Jev da organização) responde `429` (AC 6) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~OrganizationJevTests.Shares_rate_limit_with_ask_and_global_jev"`

**C8** - `POST /api/organizations/{id}/jev/ask` sem cookie responde `401` (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AuthTests.Protected_route_without_session_returns_401"`

### S2 - Chat como tela principal · ~10 files · ~60 KB · ~15k

**C9** - Logado com organizações, `/organizations` leva para `/organizations/o1` (a primeira da lista) e mostra a aba Conversa com o campo "Mensagem" (AC 7)
Proof: `npm --prefix src/web run test -- -t "organizations opens the first organization chat"`

**C10** - Logado sem organizações, `/organizations` mostra "Crie sua primeira organização"; criar leva para `/organizations/{novoId}/knowledge` (AC 8)
Proof: `npm --prefix src/web run test -- -t "no organizations shows create and lands on knowledge"`

**C11** - A barra lateral (`navigation` "Principal") tem um link por organização para `/organizations/{id}`, "Nova organização", "Jev (todas)" -> `/jev`, "Lacunas (2)" -> `/gaps` e o botão "Sair" (AC 9)
Proof: `npm --prefix src/web run test -- -t "sidebar lists organizations and destinations"`

**C12** - Com alvo "Jev decide", enviar chama `POST /api/organizations/o1/jev/ask` com a pergunta; a thread mostra a pergunta, "Respondido por RH", "via Jev", o texto da resposta, o `fileName` da fonte e o botão "Perguntar a Culture" (AC 10)
Proof: `npm --prefix src/web run test -- -t "chat sends to organization jev and shows who answered"`

**C13** - Clicar "Perguntar a Culture" chama `POST /api/assistants/{cultureId}/ask` com a mesma pergunta; a thread passa a ter as duas respostas, "Respondido por RH" e "Respondido por Culture" (AC 11)
Proof: `npm --prefix src/web run test -- -t "asking an alternative appends to the thread"`

**C14** - Fixar "Tech Team" no seletor "Para" e enviar chama `POST /api/assistants/{techId}/ask` (e não o Jev); a resposta mostra "Respondido por Tech Team" sem "via Jev" (AC 12)
Proof: `npm --prefix src/web run test -- -t "pinned assistant is asked directly"`

**C15** - `kind=clarify` mostra "Qual destas IAs deve responder?" com um botão por candidato; clicar em "Culture" acrescenta "Respondido por Culture" (AC 13)
Proof: `npm --prefix src/web run test -- -t "chat clarify lets the user pick"`

**C16** - `kind=noMatch` mostra "Nenhuma IA desta organização sabe responder isso ainda. A pergunta foi para Lacunas." com link para `/gaps` (AC 14)
Proof: `npm --prefix src/web run test -- -t "chat no match points to gaps"`

**C17** - Uma resposta com `found=false` mostra "Não encontrado nos documentos - registrado em Lacunas"; com `found=true`, não mostra (AC 15)
Proof: `npm --prefix src/web run test -- -t "not found answer is flagged"`

**C18** - Enquanto o Jev responde, a thread mostra "Jev está escolhendo…" e o botão "Enviar" fica desabilitado; com IA fixada, mostra "Tech Team está respondendo…" (AC 16)
Proof: `npm --prefix src/web run test -- -t "chat shows who is working while pending"`

**C19** - Um `502` mostra o `title` na thread (`role=alert`) e o campo "Mensagem" volta a ter o texto enviado (AC 17)
Proof: `npm --prefix src/web run test -- -t "chat error shows title and restores the message"`

**C20** - Um `422` do Jev da organização mostra "Nenhuma IA desta organização tem 'Quando usar' preenchido" com link para `/organizations/o1/knowledge` (AC 18)
Proof: `npm --prefix src/web run test -- -t "chat without eligible assistants links to base"`

**C21** - Organização sem IAs: a aba Conversa mostra "Esta organização ainda não tem IAs" com link para a aba Base e não mostra o campo "Mensagem" (AC 19)
Proof: `npm --prefix src/web run test -- -t "organization without assistants points to base"`

**C22** - Thread vazia com IAs: mostra o nome e a `routingDescription` de cada IA (AC 20)
Proof: `npm --prefix src/web run test -- -t "empty thread shows what each assistant answers"`

**C23** - `/assistants/a2` leva para `/organizations/o1` com "Culture" marcado no seletor "Para" (AC 21)
Proof: `npm --prefix src/web run test -- -t "assistant link opens the organization chat pinned"`

**C24** - Depois de uma resposta na thread de o1, clicar na organização o2 na barra lateral mostra a thread de o2 sem aquela resposta (AC 22)
Proof: `npm --prefix src/web run test -- -t "switching organization starts an empty thread"`

### S3 - Base e telas existentes · ~8 files · ~40 KB · ~10k

**C25** - Aba Base: os testes de `organizations.test.tsx` (documentos, upload, IAs, "Quando usar esta IA", vazio/carregando/404, confirmações de apagar documento e IA, erro de upload) passam em `/organizations/{id}/knowledge` sem enfraquecer asserções (AC 23)
Proof: `npm --prefix src/web run test -- -t "organization page lists documents and assistants|organization page empty, loading and not found|delete asks for confirmation|disables button while processing \(upload\)|empty document list shows only upload|upload error shows problem title"`

**C26** - Apagar a organização na aba Base pede confirmação citando "IAs, os documentos e as lacunas"; cancelar não chama `DELETE`; confirmar chama `DELETE /api/organizations/o1` e leva para `/organizations` (AC 24)
Proof: `npm --prefix src/web run test -- -t "organization delete confirms"`

**C27** - Os testes do Jev global (`jev.test.tsx`, C30-C34 do jev-gaps) passam com a thread compartilhada (AC 25)
Proof: `npm --prefix src/web run test -- -t "jev answered shows who answered and switches|jev clarify lets the user pick|jev no match points to gaps|jev without eligible assistants explains how to enable|jev loading and error states"`

**C28** - Os testes de Lacunas (`gaps.test.tsx`, C51-C55 do jev-gaps) passam no layout novo (AC 26)
Proof: `npm --prefix src/web run test -- -t "nav shows open gap count|gaps empty state|gaps answer requires organization when missing|gaps dismiss confirms|gaps loading and error states|gaps list error shows problem title"`

**C29** - A folha de estilo global define `:focus-visible` com contorno visível para controles interativos e um bloco `@media (prefers-reduced-motion: reduce)` que zera `animation` e `transition` (AC 27)
Proof: `npm --prefix src/web run test -- -t "styles keep focus visible and respect reduced motion"`

### S4 - Página inicial pública · ~3 files · ~20 KB · ~5k

**C30** - `/` sem sessão mostra o título da página inicial, os 3 passos em ordem ("Monte a organização", "Crie as IAs", "Pergunte ao Jev") e não redireciona para `/login` (AC 28, AC 29)
Proof: `npm --prefix src/web run test -- -t "home explains how it works in three steps"`

**C31** - A página inicial mostra pelo menos 3 exemplos do que montar, cada um com nome e as IAs dele, e um trecho que cita "Lacunas" (AC 30)
Proof: `npm --prefix src/web run test -- -t "home shows what you can build"`

**C32** - Sem sessão, a página inicial tem os links "Criar conta" -> `/register` e "Entrar" -> `/login`, e não tem "Abrir o chat" (AC 31)
Proof: `npm --prefix src/web run test -- -t "home without session offers sign up and sign in"`

**C33** - Com sessão, a página inicial tem o link "Abrir o chat" -> `/organizations` e não tem "Criar conta" (AC 32)
Proof: `npm --prefix src/web run test -- -t "home with session opens the chat"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/organizations/{id}/jev/ask` statuses (7) | 200 C2 · 400 C6 · 401 C8 · 404 C4 · 422 C5 · 429 C7 · 502 C6 | - |
| `kind` do Jev da organização (3) | `answered` C2 · `clarify` C6 · `noMatch` C3 | - |
| escopo do roteador (3) | outra organização do usuário C1 · outro usuário C1 · sem descrição C5 | - |
| `{id}` × {outro usuário, inexistente} (2) | outro C4 · inexistente C4 | - |
| alvo da mensagem (2) | Jev decide C12 · IA fixada C14 | - |
| mensagens da thread (7) | answered C12 · alternativa acrescentada C13 · clarify C15 · noMatch C16 · found=false C17 · erro C19 · 422 C20 | - |
| estados da aba Conversa (5) | sem IAs C21 · thread vazia C22 · carregando C18 · erro C19 · sem sessão existing (`RequireAuth`, rag-mvp C29) | - |
| entradas no produto (4) | `/organizations` com orgs C9 · sem orgs C10 · `/assistants/{id}` C23 · troca de organização C24 | - |
| barra lateral (5) | organizações C11 · Nova organização C11 · Jev (todas) C11 · Lacunas (N) C11 · Sair C11 | - |
| telas (6) | Conversa C12-C24 · Base C25, C26 · Organizações vazia C10 · Jev global C27 · Lacunas C28 · Início C30-C33 | - |
| CTA da página inicial por sessão (2) | sem sessão C32 · com sessão C33 | - |
| doors (2) | 1 C1, C4, C7 · 2 C24 | - |
| qualidade (2) | foco visível C29 · movimento reduzido C29 | - |

- Claims de status/rota/shape da Api cruzam a fronteira HTTP via `WebApplicationFactory` contra Postgres real; claims de tela via Testing Library + MSW
- C29 é prova estática sobre o CSS (não há motor de layout no happy-dom para medir foco ou animação)

## Test policy

O rag-mvp e o jev-gaps já respondem às duas perguntas (`.specs/features/rag-mvp/checks.md` `## Test policy`), e esta feature não traz forma nova de código. Sem linhas novas.

## Swept

- validation: C6
- failure modes: C6, C19
- idempotency: n/a - a rota não grava nada além da lacuna, cuja idempotência já é provada no jev-gaps (checagens 37 e 38 de lá)
- authorization: C4, C8; rate limit C7
- concurrency: n/a - nenhuma escrita nova; o upsert de lacuna já é provado sob concorrência no jev-gaps (checagem 38 de lá)
- data lifecycle: C24 (thread descartada ao trocar de organização, door 2)
- dependency failure: C6
- state transitions: n/a - nenhuma entidade com estado nova
- observability: existing - `JevAsk` loga só `Kind` e contagem (jev-gaps, checagem 50 de lá), a rota nova passa pelo mesmo handler

## Handoff

- Leitura: web atual ≈ 45 KB + `JevAsk` e testes ≈ 30 KB ≈ 19k. Escrita: S1 ≈ 6k, S2 ≈ 15k, S3 ≈ 10k. Saída de testes ≈ 10k. S4 ≈ 5k. Total ≈ 65k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
