# App mobile checks

Profile: standard
Plan: `.specs/features/mobile-app/plan.md`

65 checks in 5 slices · 5 one-way doors · 2 open, of which 0 block o build (a pergunta 2 do plano bloqueia o go-live)

Comandos de prova:

- API (PowerShell, com `DOCKER_HOST`, ver AGENTS.md): `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~<Class>.<Method>"`
- Mobile: `npm --prefix src/mobile test -- -t "<nome do teste>"` (o script e o projeto nascem nesta feature)
- `jest -t` sem teste correspondente sai com `0`: a prova só vale se a saída mostrar o teste nomeado executado e passando

Os testes de tela montam as rotas reais de `src/mobile/src/app`, com HTTP e `expo-secure-store` simulados.
Nada aqui prova o app rodando num telefone: isso são os "Independent test" do plano, manuais.

## Checks

### S1 - Entrar no app com a conta da web

**C1** - `POST /api/auth/login` sem `useCookies`, com credenciais válidas, responde `200` com `tokenType = "Bearer"`, `accessToken` e `refreshToken` não vazios e `expiresIn` > 0, sem `Set-Cookie` (AC 1, door 2) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Login_without_cookies_returns_tokens"`

**C48** - O mesmo login com senha errada e com e-mail desconhecido responde `401`, sem token no corpo (Surface) ✓
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Token_login_with_wrong_credentials_returns_401"`

**C49** - `POST /api/auth/refresh` com um `refreshToken` válido responde `200` com um par novo, e o `accessToken` novo abre `GET /api/auth/me` (Surface, AC 11)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Refresh_returns_a_working_pair"`

**C50** - `POST /api/auth/refresh` com um token inventado responde `401` (Surface, AC 12)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Refresh_with_invalid_token_returns_401"`

**C2** - `GET /api/auth/me` com `Authorization: Bearer <accessToken>` e sem cookie responde `200` com o `email` e o `fullName` desse usuário (AC 2)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Bearer_identifies_the_user"`

**C3** - Com o bearer do usuário A, `GET /api/organizations/{id}` de uma organização de B responde `404`, e `GET /api/gaps` não traz lacunas de B (AC 3)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Bearer_cannot_see_another_users_data"`

**C4** - Um bearer inventado e um bearer expirado respondem `401` em `GET /api/auth/me`, sem `Location` (AC 4)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Invalid_or_expired_bearer_returns_401"`

**C63** - Com bearer, o limite de perguntas continua por usuário: a 21ª pergunta de A no minuto responde `429` e a primeira de B responde `200`; o bearer sem papel Admin recebe `403` em `GET /api/admin/usage` (Impact: auth)
Proof: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~MobileAuthTests.Bearer_keeps_rate_limit_per_user_and_roles"`

**C5** - Entrar com credenciais válidas grava `accessToken` e `refreshToken` no `expo-secure-store`, mostra Organizações e a chamada seguinte leva `Authorization: Bearer <accessToken>` (AC 5, door 2) ✓
Proof: `npm --prefix src/mobile test -- -t "login stores tokens and opens organizations"`

**C6** - Em login, refresh e erro, nenhuma chamada a `console.*` contém token ou senha; e `src/mobile/src` não importa `async-storage` nem `expo-file-system` (AC 6) (parcial: falta o caminho de refresh)
Proof: `npm --prefix src/mobile test -- -t "tokens and password are never logged"`
Proof: `npm --prefix src/mobile test -- -t "only secure store persists data"`

**C7** - Um `401` no login mostra "E-mail ou senha inválidos." e não dispara `POST /api/auth/refresh` (AC 7) ✓
Proof: `npm --prefix src/mobile test -- -t "login failure shows invalid credentials"`

**C8** - Criar conta envia `fullName`, `email`, `password` a `POST /api/auth/register`, faz o login e mostra Organizações (AC 8)
Proof: `npm --prefix src/mobile test -- -t "register creates account and signs in"`

**C9** - Um `400` no cadastro mostra o `title` e as mensagens de `errors`, e os três campos mantêm o que foi digitado (AC 9)
Proof: `npm --prefix src/mobile test -- -t "register errors keep the typed values"`

**C62** - Enquanto login ou cadastro não respondem, o botão mostra "Processando..." e fica desabilitado (Observable) (parcial: falta o formulário de cadastro)
Proof: `npm --prefix src/mobile test -- -t "auth forms show processing state"`

**C10** - Abrir o app com tokens guardados mostra Organizações sem a tela Entrar; sem tokens, mostra Entrar (AC 10) ✓
Proof: `npm --prefix src/mobile test -- -t "app start follows the stored session"`

**C11** - Com 3 chamadas em paralelo respondendo `401`, há exatamente um `POST /api/auth/refresh`, o par novo é gravado e cada chamada é repetida uma vez com o token novo (AC 11)
Proof: `npm --prefix src/mobile test -- -t "parallel 401s refresh once and retry"`

**C56** - Se a chamada repetida também responde `401`, não há segundo refresh e o erro chega à tela (AC 11)
Proof: `npm --prefix src/mobile test -- -t "retried 401 does not refresh again"`

**C57** - Um `401` que chega depois do refresh terminar, de uma chamada feita com o token antigo, repete com o token novo sem outro refresh (AC 11)
Proof: `npm --prefix src/mobile test -- -t "late 401 reuses the refreshed token"`

**C12** - Um refresh `401` apaga os dois tokens e mostra Entrar com "Sua sessão expirou. Entre novamente." (AC 12)
Proof: `npm --prefix src/mobile test -- -t "expired refresh returns to login"`

**C13** - Um refresh que falha por rede ou com `500` mantém os tokens gravados, mostra o erro e "Tentar de novo", que refaz a chamada (AC 13)
Proof: `npm --prefix src/mobile test -- -t "refresh outage keeps the session"`

**C14** - "Sair" apaga os dois tokens, esvazia o cache do TanStack Query e mostra Entrar (AC 14) ✓
Proof: `npm --prefix src/mobile test -- -t "logout clears tokens and cache"`

**C15** - Uma falha de rede mostra "Não foi possível falar com o servidor." (AC 15) ✓
Proof: `npm --prefix src/mobile test -- -t "network failure shows server unreachable"`

**C16** - Sem `EXPO_PUBLIC_API_URL`, o app mostra a tela que manda defini-la em `src/mobile/.env`, sem chamar `fetch` (AC 16) ✓
Proof: `npm --prefix src/mobile test -- -t "missing api url shows setup screen"`

**C51** - O bundle do app é gerado para Android e iOS sem erro (door 1) ✓
Proof: `npm --prefix src/mobile run bundle`

**C52** - `src/mobile/package.json` fixa `expo` na faixa 58 e usa `expo-router` como entrada (door 1; se o Expo Go do usuário for 57, o plano manda começar no 57 e este check volta para aprovação) ✓
Proof: `npm --prefix src/mobile test -- -t "app config pins expo sdk 58"`

**C53** - Nenhum arquivo de `src/mobile` importa de `src/web`, e cada `types.ts` espelhado traz o comentário `// Mirrors` (door 3) ✓
Proof: `npm --prefix src/mobile test -- -t "mobile mirrors types without importing web"`

### S2 - Conversar com as IAs

**C17** - Organizações lista cada organização com nome, número de IAs e número de documentos, na ordem da Api (AC 17)
Proof: `npm --prefix src/mobile test -- -t "organizations list shows counts"`

**C18** - Sem organizações, mostra "Você ainda não tem organizações. Crie a primeira na versão web." (AC 18)
Proof: `npm --prefix src/mobile test -- -t "empty organizations points to web"`

**C59** - Organizações mostra "Carregando..." enquanto espera e o `title` do problem details quando falha (Observable)
Proof: `npm --prefix src/mobile test -- -t "organizations loading and error"`

**C19** - Organização sem IAs mostra "Esta organização ainda não tem IAs" e manda criá-las na versão web, sem campo de pergunta (AC 19)
Proof: `npm --prefix src/mobile test -- -t "organization without assistants points to web"`

**C58** - Abrir uma organização que responde `404` mostra o `title` da Api (Observable)
Proof: `npm --prefix src/mobile test -- -t "missing organization shows api title"`

**C20** - Conversa vazia lista cada IA com o "Quando usar"; a sem "Quando usar" mostra o mesmo texto da web (AC 20)
Proof: `npm --prefix src/mobile test -- -t "empty conversation lists the assistants"`

**C21** - A primeira pergunta com "Escolha automática" envia só `question` a `POST /api/organizations/{id}/route/ask`; a seguinte envia também `previous { question, assistantId }` da última resposta (AC 21)
Proof: `npm --prefix src/mobile test -- -t "automatic choice sends the previous turn"`

**C22** - Com uma IA selecionada em "Para", a pergunta vai para `POST /api/assistants/{id}/ask` (AC 22)
Proof: `npm --prefix src/mobile test -- -t "pinned assistant is asked directly"`

**C23** - `answered` mostra "Respondido por <IA>" e "escolha automática"; "Perguntar a <alternativa>" chama o ask daquela IA com a mesma pergunta (AC 23)
Proof: `npm --prefix src/mobile test -- -t "answer offers the alternatives"`

**C24** - `found: false` mostra "Não encontrado nos documentos - registrado em Lacunas" e nenhum botão "trecho N" (AC 24)
Proof: `npm --prefix src/mobile test -- -t "not found answer cites nothing"`

**C25** - `clarify` mostra "Qual destas IAs deve responder?" com um botão por candidata; tocar chama o ask e a resposta toma o lugar do aviso (AC 25)
Proof: `npm --prefix src/mobile test -- -t "clarify is replaced by the chosen answer"`

**C26** - `noMatch` mostra "Nenhuma IA desta organização sabe responder isso ainda. A pergunta foi para Lacunas." e o atalho abre a aba Lacunas (AC 26)
Proof: `npm --prefix src/mobile test -- -t "no match links to gaps"`

**C27** - Um `422` da escolha automática mostra "Nenhuma IA desta organização tem 'Quando usar' preenchido." e o campo volta com o texto (AC 27)
Proof: `npm --prefix src/mobile test -- -t "no eligible assistant restores the text"`

**C28** - Para `404`, `429`, `502` e falha de rede, a conversa mostra o título do erro e o campo volta com o texto (AC 28, AC 15)
Proof: `npm --prefix src/mobile test -- -t "ask errors show title and restore text"`

**C29** - Enquanto a pergunta não responde, "Enviar" e os botões de IA ficam desabilitados, e um segundo toque não faz outra chamada (AC 29)
Proof: `npm --prefix src/mobile test -- -t "pending question disables sending"`

**C30** - Texto vazio e texto só com espaços não geram chamada (AC 30)
Proof: `npm --prefix src/mobile test -- -t "blank question is not sent"`

**C31** - Voltar para Organizações e abrir a mesma de novo mostra a conversa vazia (AC 31)
Proof: `npm --prefix src/mobile test -- -t "reopening organization starts empty"`

### S3 - Ver o trecho

**C32** - `found: true` com 3 fontes de 2 documentos mostra 2 grupos, na ordem de citação, com um botão "trecho N" (índice + 1) por trecho; nada abre sozinho (AC 32, Assumptions)
Proof: `npm --prefix src/mobile test -- -t "sources are grouped by document"`

**C33** - Tocar em "trecho N" lê `GET /api/organizations/{id}/documents/{documentId}/chunks/{index}?around=1` e mostra o arquivo, "Trecho N de M" e o trecho citado marcado como selecionado entre os vizinhos (AC 33)
Proof: `npm --prefix src/mobile test -- -t "source opens with cited chunk highlighted"`

**C60** - Enquanto o trecho não chega, a tela mostra "Carregando trecho…" (Observable)
Proof: `npm --prefix src/mobile test -- -t "source preview shows loading"`

**C34** - Fechar o trecho volta à conversa com as mesmas mensagens (AC 34)
Proof: `npm --prefix src/mobile test -- -t "closing source keeps the conversation"`

**C35** - Um `404` na leitura mostra o `title` e "Fechar prévia" continua funcionando (AC 35)
Proof: `npm --prefix src/mobile test -- -t "source error can be closed"`

### S4 - Fechar Lacunas

**C36** - Lacunas lista na ordem da Api, com pergunta, vezes, organização ou "Sem organização" e a IA quando houver (AC 36)
Proof: `npm --prefix src/mobile test -- -t "gaps list shows details in api order"`

**C37** - Sem lacunas, mostra "Nenhuma lacuna. Suas IAs responderam tudo o que foi perguntado." (AC 37)
Proof: `npm --prefix src/mobile test -- -t "empty gaps message"`

**C61** - Lacunas mostra "Carregando lacunas…" enquanto espera e o `title` quando a lista falha (Observable)
Proof: `npm --prefix src/mobile test -- -t "gaps loading and error"`

**C38** - Com 2 lacunas abertas a aba mostra "2"; com nenhuma, não mostra número (AC 38)
Proof: `npm --prefix src/mobile test -- -t "gaps tab shows open count"`

**C39** - Responder envia `answer` e `organizationId: null` quando a lacuna já tem organização, mostra "Processando...", ignora um segundo toque e tira a lacuna da lista (AC 39)
Proof: `npm --prefix src/mobile test -- -t "answering a gap removes it"`

**C40** - Lacuna sem organização mantém "Responder" desabilitado até escolher uma, e envia o `organizationId` escolhido (AC 40)
Proof: `npm --prefix src/mobile test -- -t "gap without organization requires choosing one"`

**C41** - "Dispensar" abre o `Alert.alert` com "Dispensar esta lacuna? A pergunta sai da lista e não volta."; cancelar não chama a Api, confirmar chama `POST /api/gaps/{id}/dismiss` (AC 41)
Proof: `npm --prefix src/mobile test -- -t "dismiss asks for confirmation"`

**C42** - Responder com `400`, `404`, `409` ou `502`, e dispensar com `404` ou `409`, mostra o `title` e mantém o texto digitado (AC 42)
Proof: `npm --prefix src/mobile test -- -t "gap errors keep the typed answer"`

### S5 - Seguir o desenho da web

**C43** - As 17 cores de `theme.ts` são iguais às de `src/web/src/index.css`; o teste lê o CSS do disco e falha em divergência ou nome ausente (AC 43) ✓
Proof: `npm --prefix src/mobile test -- -t "theme colors match the web"`

**C44** - Nenhum arquivo de `src/mobile/src` além de `theme.ts` contém cor em hexadecimal, `rgb(`, `hsl(` ou nome de cor em propriedade de cor (`transparent` é aceito) (AC 44) ✓
Proof: `npm --prefix src/mobile test -- -t "no literal colors outside theme"`

**C45** - O layout raiz carrega as três famílias; um título usa Bricolage Grotesque, um parágrafo Instrument Sans e um rótulo técnico ("trecho N") JetBrains Mono (AC 45) (parcial: falta o rótulo técnico "trecho N")
Proof: `npm --prefix src/mobile test -- -t "screens use the web fonts"`

**C46** - `fetch(` aparece em `src/mobile/src` somente em `shared/api/client.ts` (AC 46) ✓
Proof: `npm --prefix src/mobile test -- -t "only the api client calls fetch"`

**C47** - A IA na posição i (0..6) da organização recebe a cor `lane-(i mod 6 + 1)` na lista, no "Para" e na resposta (AC 47)
Proof: `npm --prefix src/mobile test -- -t "assistant lane color follows its position"`

**C54** - O TypeScript do app compila em `strict`, e nenhum arquivo de `src/mobile/src` usa `any` explícito (door 5) ✓
Proof: `npm --prefix src/mobile run typecheck`
Proof: `npm --prefix src/mobile test -- -t "no explicit any"`

**C55** - Cada arquivo de `src/mobile/src/app` só declara rota ou layout (sem `useQuery`, `useMutation` nem `fetch`), e as telas ficam em `src/mobile/src/features/<area>/` (door 5) ✓
Proof: `npm --prefix src/mobile test -- -t "routes only declare routes"`

**C64** - O `architecture-guardian` tem uma seção "Mobile", e `AGENTS.md`, `README.md` e `docs/PRD.md` citam `src/mobile` (Impact: repositório) ✓
Proof: `grep -q "Mobile" .claude/agents/architecture-guardian.md && grep -q "src/mobile" AGENTS.md && grep -q "src/mobile" README.md && grep -q "src/mobile" docs/PRD.md`

**C65** - A web continua compilando e com os testes verdes, sem arquivo alterado em `src/web` nem em `src/BuildYourOwnAI.Api` (Impact: build; door 3) ✓
Proof: `npm --prefix src/web run build && npm --prefix src/web test && git diff --quiet 252f016 -- src/web src/BuildYourOwnAI.Api`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/auth/login` sem `useCookies` statuses (2) | 200 C1 · 401 C48 | - |
| `POST /api/auth/refresh` statuses (2) | 200 C49 · 401 C50 | - |
| bearer recusado (2) | inventado C4 · expirado C4 | - |
| decisão do cliente HTTP (8) | sem 401 C5 · 401 e refresh ok C11 · 401 repetido C56 · 401 atrasado C57 · refresh 401 C12 · refresh rede ou 5xx C13 · 401 no login C7 · sem URL C16 | - |
| início do app (2) | com tokens C10 · sem tokens C10 | - |
| resultado da escolha automática (3) | answered C23 · clarify C25 · noMatch C26 | - |
| `found` (2) | true C32 · false C24 | - |
| erros da pergunta (5) | 422 C27 · 404 C28 · 429 C28 · 502 C28 · rede C28 | - |
| alvo da pergunta (2) | automático C21 · IA fixada C22 | - |
| texto não enviável (2) | vazio C30 · só espaços C30 | - |
| erros de responder lacuna (4) | 400 C42 · 404 C42 · 409 C42 · 502 C42 | - |
| erros de dispensar (2) | 404 C42 · 409 C42 | - |
| confirmação de dispensar (2) | cancelar C41 · confirmar C41 | - |
| organização da lacuna (2) | com C39 · sem C40 | - |
| estados de Entrar e Criar conta (3) | processando C62 · erro de login C7 · erro de cadastro C9 | - |
| estados de Organizações (3) | carregando C59 · erro C59 · vazio C18 | - |
| estados da Conversa (5) | sem IAs C19 · vazia C20 · pendente C29 · erro C28 · organização 404 C58 | - |
| estados da Prévia (3) | carregando C60 · conteúdo C33 · erro C35 | - |
| estados de Lacunas (3) | carregando C61 · erro C61 · vazio C37 | - |
| cores do tema (17) | C43, table-driven sobre as 17 | - |
| doors (5) | 1 C51, C52 · 2 C1, C5, C11 · 3 C53, C65 · 4 C5, C51 · 5 C43, C46, C54, C55 | - |
| entidades (0) | nenhuma mudança de dados armazenados | - |
| startup do app: providers, fontes e guard de sessão (1 assembly) | layout raiz real, montado também pelos testes de tela C10, C45 | - |

- Claims de status/rota da Api (C1-C4, C48-C50, C63) cruzam a fronteira HTTP contra Postgres real
- Claims de tela montam as rotas reais; o HTTP é simulado, então o contrato com a Api é provado só pelos checks de API e pelo comentário `// Mirrors`: nenhum check roda app e Api juntos

## Test policy

O `architecture-guardian` responde nível e profundidade para Api e web, não para o app. Linhas para `src/mobile`, seguindo o precedente da web:

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decide e é alcançado por tela (`shared/api/client.ts`: refresh e erros) | um pela tela ou pelo cliente público, com HTTP simulado | um caso afirmado por linha da tabela de decisão |
| Tela com estados (chat, Lacunas, autenticação, prévia) | um por estado, montando a rota real | cada estado e cada erro nomeado no plano |
| Mapeamento fixo (`theme.ts`, `lanes.ts`) | um próprio | todos os membros |
| Repasse (hooks `api.ts`, `types.ts`, arquivos de rota) | nenhum próprio | coberto pela prova da tela |

Evidence:

- `src/web/src/shared/api/client.ts`: ramos por `ok`, `204`, corpo vazio e problem details; o do app soma refresh (6 resultados) -> decide
- `src/web/src/features/chat/Thread.tsx`: 5 tipos de entrada, 2 alvos de pergunta, ramo do `422` -> decide
- `src/web/src/features/organizations/api.ts`: cada hook repassa uma chamada -> instrumentação
- análogo no repo: `src/web/src/features/chat/chat.test.tsx` e `gaps.test.tsx`, provados no nível de tela com MSW

## Swept

- validation: C9, C30, C40
- failure modes: C13, C15, C28, C35, C42
- idempotency: C29 e C39 (segundo toque ignorado); resposta repetida a uma lacuna já fechada é o `409` de C42
- authorization: C2, C3, C4, C12, C63
- concurrency: C11, C56, C57
- data lifecycle: C14 - "Sair" apaga o que está no aparelho; o `refreshToken` segue válido no servidor até expirar (assumption do plano, aprovada com o plano)
- dependency failure: C13, C15, C28
- state transitions: C10, C12, C14 (sem sessão <-> com sessão)
- observability: C6 - o app não registra conteúdo nem segredo; a Api não muda

## Handoff

- Leitura: web a espelhar ≈ 59 KB, testes web de referência ≈ 30 KB, infraestrutura de teste da Api ≈ 15 KB ≈ 26k tokens
- Escrita: app ≈ 55 KB (14k), testes do app ≈ 60 KB (15k), testes da Api ≈ 8 KB (2k), configuração e docs ≈ 8 KB (2k)
- Folga para ajustar `jest-expo` e Expo SDK 58, que não existem no repo ≈ 15k (estimativa sem base medida)
- Total ≈ 74k, abaixo do budget de 150k - um builder
- Mechanism: one builder (dentro do budget, sem pergunta)
- **Boundary:** o usuário reduziu a primeira entrega à tela Entrar e sua base; feitos os checks marcados `✓`, o resto está em plan.md `## Execução`. Verifier não rodado: a feature não está completa
- **Settled mid-build:** implementar só uma tela e deixar instruções para outro dev; branch a partir da `main` atual (`origin/main`, `252f016`); C65 passou a comparar com `252f016`
- **Abandoned:** nada
