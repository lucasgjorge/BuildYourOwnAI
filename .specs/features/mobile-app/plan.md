# App mobile - conversar com as IAs e fechar Lacunas pelo celular (React Native + Expo)

## Problem

O produto só existe como web. Quem quer consultar as próprias IAs fora do computador depende do
navegador do celular, e a sessão atual é um cookie de mesma origem pensado para o SPA. Os clientes
fora do browser ficaram explicitamente fora do escopo na etapa 1 (rag-mvp, `Out of scope`).

A fonte não traz números. A evidência é o pedido do usuário em 2026-11-26: criar uma pasta no repo
para as páginas mobile em React Native, escolhendo Expo porque fica mais fácil de testar.

Depois desta mudança, o usuário abre o app no próprio telefone pelo Expo Go, entra com a mesma conta
da web, escolhe uma organização, conversa com as IAs dela (com a escolha automática), abre o trecho
citado e responde ou dispensa Lacunas.

**Guardrail (pedido do usuário):** o app segue o desenho da aplicação web, tanto na organização do
código quanto no visual. Onde este plano não disser o contrário, a web decide.

## Flow

Reusa a Api inteira como ela está (rotas, problem details, filtro de dono, rate limit) e o login por
token que o `MapIdentityApi` já oferece; da web, reaproveita por cópia o contrato (tipos, tratamento
de erro, chaves de consulta, cores) e o comportamento do chat. Nenhum código de produção da Api muda.

1. app abre -> `src/mobile` (door 1) lê os tokens do armazenamento seguro do aparelho (door 2); sem token, tela Entrar
2. Entrar / Criar conta -> `Features/Auth/AuthEndpoints` e `Register` (exists) - `POST /api/auth/login` sem `useCookies` devolve o par de tokens (door 2)
3. toda chamada -> `src/mobile` (door 1) envia `Authorization: Bearer`; em 401 renova uma vez por `POST /api/auth/refresh` e repete
4. Organizações -> `Features/Organizations` (exists) - lista e detalhe com as IAs
5. Conversa -> `Features/Routing/RouteAsk` (exists) na escolha automática, ou `Features/Ask/AskAssistant` (exists) quando uma IA está fixada; a thread vive só na tela
6. trecho citado -> `Features/Documents/GetDocumentChunks` (exists) - o trecho e os vizinhos, numa tela por cima da conversa
7. Lacunas -> `Features/Gaps` (exists) - lista, responde, dispensa

## Impact

| Front | What changes |
| --- | --- |
| domain | nenhum termo novo; o app usa os mesmos nomes da web (organização, IA, escolha automática, Lacunas, trecho) |
| auth | existing term: "sessão" significava só o cookie da web; passa a incluir o par de tokens do app. AD-009 continua valendo para o SPA; o app precisa de uma decisão própria (door 2). Quem depende do esquema de autenticação hoje: todo `RequireAuthorization()`, a policy de `Admin` e o rate limit por usuário |
| contrato da Api | `Auth`, `Organizations`, `Routing`, `Ask`, `Documents` (chunks) e `Gaps` passam a ter um segundo consumidor: mudar um formato exige atualizar web **e** mobile |
| repositório | pasta nova `src/mobile` com `package.json` próprio; `AGENTS.md` (layout e comandos), `README.md`, `docs/PRD.md` (o app entra no roadmap) e o agente `architecture-guardian` ganham a parte mobile, incluindo uma seção "Mobile" no checklist com as regras da door 5 |
| build | `dotnet build`, `dotnet test` e `npm run build` da web não mudam |
| stored data | nothing to migrate |

## Relations

None - no stored-data shape change

## Surface

Nenhuma rota nova no servidor. Estas duas já existem (vêm do Identity), mas passam a ser consumidas
com esta assinatura pela primeira vez.

| Route | In | Out | Status |
| --- | --- | --- | --- |
| `POST /api/auth/login` (sem `useCookies`) | `email`, `password` | `tokenType` · `accessToken` · `expiresIn` · `refreshToken` | `200`, `401` |
| `POST /api/auth/refresh` | `refreshToken` | `tokenType` · `accessToken` · `expiresIn` · `refreshToken` | `200`, `401` |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. Projeto mobile e stack (dependências novas) | `src/mobile/`: Expo SDK 58 (`expo` 58.x), React Native, TypeScript, Expo Router (cada arquivo em `src/mobile/src/app/` é uma tela), TanStack Query como na web. Só bibliotecas que rodam no Expo Go | PWA ou Capacitor sobre a web atual: não é React Native nem Expo. React Native CLI puro: exige Android Studio/Xcode para testar, e iOS não compila no Windows |
| 2. Autenticação do app (AD-016 em `.specs/STATE.md`) | `POST /api/auth/login` sem `useCookies` + `POST /api/auth/refresh` (bearer do Identity); `accessToken` e `refreshToken` só no `expo-secure-store`; header `Authorization: Bearer <accessToken>` | Cookie como na web: é `Secure`, então não volta em HTTP na rede local, e o armazenamento dele fica por conta do sistema do aparelho. JWT próprio: emissão e renovação para manter |
| 3. Como web e mobile dividem código | sem pacote compartilhado; `src/mobile` copia os tipos e o cliente HTTP, com o mesmo comentário `// Mirrors ...` que a web já usa para espelhar a Api | npm workspaces com pacote `shared`: mexe no build e nos imports da web e exige configurar o Metro, antes de sabermos quanto os dois vão de fato dividir |
| 4. Testes do app (dependência nova) | `jest-expo` + `@testing-library/react-native`, rodando com `npm test` em `src/mobile`; HTTP simulado, sem OpenAI e sem servidor | Vitest como na web: não transforma React Native nem os módulos do Expo sem configuração própria |
| 5. Guardrail: o desenho do app espelha o da web (dependência nova: `expo-font` e as fontes) | `src/mobile/src/app/` só declara rotas; `src/mobile/src/features/<area>/` com telas, `api.ts`, `types.ts` e testes, nas mesmas áreas da web (`auth`, `organizations`, `chat`, `routing`, `assistants`, `gaps`); `src/mobile/src/shared/` só para o que 2+ features usam (`api/client.ts` único, `ui.tsx` com `Button`/`Alert`/input, `lanes.ts`, `theme.ts`). Regras do checklist "Web" do `architecture-guardian`: dados do servidor só por TanStack Query, TypeScript strict sem `any`, um guard único de sessão, erro mostra o título sem limpar o formulário, ação destrutiva confirma, biblioteca de UI ou estado global só com decisão em `STATE.md`. Visual: `StyleSheet` + `theme.ts` com os tokens de `index.css`; fontes Bricolage Grotesque, Instrument Sans e JetBrains Mono | NativeWind (classes Tailwind como na web): a versão estável é a 4, e o suporte ao Tailwind 4 da web está na 5, ainda release candidate (`5.0.0-rc.1`). Biblioteca de componentes pronta: traz uma identidade diferente da web |

- Nothing else in this change is hard to reverse

## Criteria

### S1: entrar no app com a conta da web (P1)

A mesma conta funciona no celular e continua entrada ao reabrir o app.

**Acceptance Criteria**

1. WHEN `POST /api/auth/login` sem `useCookies` recebe credenciais válidas THEN the system SHALL responder `200` com `accessToken` e `refreshToken`, sem `Set-Cookie`
2. WHEN `GET /api/auth/me` recebe `Authorization: Bearer` de um token válido THEN the system SHALL responder `200` com `email` e `fullName` desse usuário
3. IF um bearer do usuário A pede a organização do usuário B THEN the system SHALL responder `404`
4. IF o bearer é inválido ou expirado THEN the system SHALL responder `401`
5. WHEN o usuário entra com e-mail e senha válidos THEN the system SHALL guardar os dois tokens no `expo-secure-store` e mostrar Organizações
6. The system SHALL nunca gravar token ou senha fora do `expo-secure-store` nem escrevê-los em log
7. IF o login responde `401` THEN the system SHALL mostrar "E-mail ou senha inválidos."
8. WHEN o usuário cria a conta com nome completo, e-mail e senha THEN the system SHALL chamar `POST /api/auth/register`, entrar e mostrar Organizações
9. IF o cadastro responde `400` THEN the system SHALL mostrar o título e as mensagens do problem details, mantendo o que foi digitado
10. WHEN o app abre com tokens guardados válidos THEN the system SHALL mostrar Organizações sem pedir senha
11. WHEN uma chamada autenticada responde `401` THEN the system SHALL fazer um único `POST /api/auth/refresh`, mesmo com várias chamadas em paralelo, guardar o par novo e repetir cada chamada uma vez
12. IF o refresh responde `401` THEN the system SHALL apagar os tokens e mostrar Entrar com "Sua sessão expirou. Entre novamente."
13. IF o refresh falha por rede ou `5xx` THEN the system SHALL manter os tokens e mostrar o erro com "Tentar de novo"
14. WHEN o usuário toca em "Sair" THEN the system SHALL apagar os tokens, limpar os dados em memória e mostrar Entrar
15. IF o servidor não responde THEN the system SHALL mostrar "Não foi possível falar com o servidor."
16. IF `EXPO_PUBLIC_API_URL` não está definida THEN the system SHALL mostrar uma tela dizendo para defini-la em `src/mobile/.env`

**Independent test:** criar a conta na web, entrar no telefone, fechar e reabrir o app, sair.

### S2: conversar com as IAs de uma organização (P1)

O chat da organização funciona no celular como na web.

**Acceptance Criteria**

17. WHEN a tela Organizações abre THEN the system SHALL listar cada organização com nome, número de IAs e número de documentos
18. IF não há organizações THEN the system SHALL mostrar "Você ainda não tem organizações. Crie a primeira na versão web."
19. IF a organização não tem IAs THEN the system SHALL mostrar "Esta organização ainda não tem IAs" e dizer para criá-las na versão web
20. WHILE a conversa está vazia the system SHALL listar as IAs da organização com o "Quando usar" de cada uma
21. WHEN o usuário envia uma pergunta com "Escolha automática" THEN the system SHALL chamar `POST /api/organizations/{id}/route/ask` com `question` e, havendo resposta anterior na conversa, `previous { question, assistantId }`
22. WHEN o usuário envia uma pergunta com uma IA selecionada em "Para" THEN the system SHALL chamar `POST /api/assistants/{id}/ask`
23. WHEN a resposta é `answered` THEN the system SHALL mostrar "Respondido por <IA>", o selo "escolha automática" e um botão "Perguntar a <IA>" para cada alternativa, que refaz a mesma pergunta àquela IA
24. WHEN a resposta tem `found: false` THEN the system SHALL mostrar "Não encontrado nos documentos - registrado em Lacunas" sem listar fontes
25. WHEN a resposta é `clarify` THEN the system SHALL mostrar "Qual destas IAs deve responder?" com um botão por candidata, e a resposta da escolhida substitui esse aviso
26. WHEN a resposta é `noMatch` THEN the system SHALL mostrar "Nenhuma IA desta organização sabe responder isso ainda. A pergunta foi para Lacunas." com atalho para Lacunas
27. IF a escolha automática responde `422` THEN the system SHALL mostrar "Nenhuma IA desta organização tem 'Quando usar' preenchido." e devolver o texto ao campo
28. IF a pergunta falha com outro erro THEN the system SHALL mostrar o título do problem details e devolver o texto ao campo
29. WHILE uma pergunta está em andamento the system SHALL desabilitar "Enviar" e os botões de IA
30. IF o texto é vazio ou só espaços THEN the system SHALL não enviar nada
31. WHEN o usuário sai da organização e entra de novo THEN the system SHALL mostrar a conversa vazia

**Independent test:** na web, preparar uma organização com duas IAs; no telefone, perguntar, receber a resposta de uma e perguntar à outra com um toque.

### S3: ver o trecho que sustenta a resposta (P1)

Um toque na fonte mostra o texto do documento.

**Acceptance Criteria**

32. WHEN a resposta tem `found: true` THEN the system SHALL listar as fontes agrupadas por documento, com um botão "trecho N" por trecho
33. WHEN o usuário toca em "trecho N" THEN the system SHALL abrir por cima da conversa o nome do arquivo, "Trecho N de M" e o trecho citado destacado entre os vizinhos
34. WHEN o usuário fecha o trecho THEN the system SHALL voltar à conversa com todas as mensagens
35. IF a leitura do trecho falha THEN the system SHALL mostrar o título do erro com a opção de fechar

**Independent test:** a partir de uma resposta com fontes, abrir e fechar um trecho.

### S4: fechar Lacunas pelo celular (P2)

O dono responde no telefone o que nenhuma IA soube.

**Acceptance Criteria**

36. WHEN a aba Lacunas abre THEN the system SHALL listar as lacunas na ordem da Api, com pergunta, número de vezes, organização ou "Sem organização" e a IA quando houver
37. IF não há lacunas THEN the system SHALL mostrar "Nenhuma lacuna. Suas IAs responderam tudo o que foi perguntado."
38. WHILE há lacunas abertas the system SHALL mostrar a quantidade na aba Lacunas
39. WHEN o usuário envia a resposta THEN the system SHALL chamar `POST /api/gaps/{id}/answer`, mostrar "Processando..." sem permitir segundo envio e tirar a lacuna da lista
40. WHILE uma lacuna sem organização não tem organização escolhida the system SHALL manter "Responder" desabilitado
41. WHEN o usuário toca em "Dispensar" THEN the system SHALL pedir confirmação nativa com "Dispensar esta lacuna? A pergunta sai da lista e não volta." antes de chamar `POST /api/gaps/{id}/dismiss`
42. IF responder ou dispensar falha THEN the system SHALL mostrar o título do problem details e manter o texto digitado

**Independent test:** perguntar algo fora dos documentos, responder pela aba Lacunas e perguntar de novo.

### S5: o app segue o desenho da web (P1)

Quem conhece a web reconhece o app e acha o código no mesmo lugar. Vale para S1 a S4.

**Acceptance Criteria**

43. The system SHALL definir em `src/mobile/src/shared/theme.ts` as cores `fog`, `surface`, `ink`, `muted`, `line`, `route`, `route-soft`, `danger`, `danger-soft`, `success`, `success-soft` e `lane-1` a `lane-6` com os mesmos valores de `src/web/src/index.css`, e um teste SHALL falhar quando um deles divergir
44. The system SHALL não ter cor literal em `src/mobile/src` fora de `theme.ts`
45. The system SHALL usar Bricolage Grotesque nos títulos, Instrument Sans no texto e JetBrains Mono nos rótulos técnicos, como na web
46. The system SHALL chamar `fetch` somente em `src/mobile/src/shared/api/client.ts`
47. The system SHALL dar a cada IA da organização a cor da mesma posição que ela tem na web

**Independent test:** abrir a mesma organização na web e no telefone e comparar cores, fontes e a cor de cada IA.

## Out of scope

| Excluded | Why |
| --- | --- |
| Study Mode no app | não entrou na primeira versão (escolha do usuário em 2026-11-26) |
| Base: criar organização e IAs, enviar e apagar documentos | idem; continua na web |
| Chat entre todas as IAs (`/all`) | não faz parte do mínimo pedido; as lacunas que ele gera aparecem no app |
| Tela de uso do admin | ferramenta de operação, fica na web |
| Publicar na App Store / Google Play (builds EAS) | o pedido é testar com Expo; publicar exige contas pagas e API pública |
| Alvo web do Expo | já existe a web; exigiria CORS na Api |
| Histórico persistido, streaming, offline, notificações, biometria, modo escuro | a web também não tem ou são etapas posteriores |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Nome da pasta | `src/mobile` | segue `src/web` (AD-001) | n |
| Onde testar | telefone físico com Expo Go, na mesma rede do PC; Android e iPhone valem | é o caminho do Expo sem emulador; no Windows não há simulador de iOS | n |
| Como o telefone alcança a Api | `dotnet run --project src/BuildYourOwnAI.Api --urls http://0.0.0.0:5295` e `EXPO_PUBLIC_API_URL=http://<IP do PC>:5295` | HTTP só em desenvolvimento, em rede confiável: senha e token trafegam sem cifra, e a conta admin de desenvolvimento fica visível na rede | n |
| Abertura do trecho | só no toque; a web abre sozinha o primeiro trecho | no celular a prévia cobre a resposta | n |
| "Sair" | apaga os tokens do aparelho; o `refreshToken` não é revogado no servidor e expira sozinho (padrão do Identity: 14 dias) | o bearer do Identity não tem revogação por aparelho; revogar exigiria mudar a Api | n |
| Alcance de "semelhante à web" | mesma estrutura de código e mesma identidade visual; o layout se adapta ao celular (abas no lugar da barra lateral, trecho por cima em vez de coluna) | o pedido não separou arquitetura de visual, então valem os dois | n |
| Idioma | textos em português, iguais aos da web | mesmo produto | n |

**Open questions:**

| # | Kind | Question | Until answered |
| --- | --- | --- | --- |
| 1 | open | O Expo Go da loja do seu telefone já abre projetos do SDK 58? O anúncio do SDK 58 dizia que a versão das lojas sairia semanas depois, e não encontrei confirmação | o plano fixa SDK 58; se o seu Expo Go ainda for do 57, o projeto começa no 57 |
| 2 | blocks go-live | Onde a Api vai ficar publicada, com HTTPS? | o app funciona só contra a Api na rede local |

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| screen `Entrar` e `Criar conta` | carregando e erro | AC 7, AC 9, AC 15; botão mostra "Processando..." como na web |
| screen `Entrar` e `Criar conta` | vazio, não autorizado | n/a - são formulários públicos |
| screen `Organizações` | vazio | AC 18 |
| screen `Organizações` | carregando, erro, não autorizado | existing - padrão da web ("Carregando..." e título do problem details); AC 12 e AC 15 |
| screen `Organizações` | ordem | existing - a ordem que a Api devolve |
| screen `Conversa` | vazio | AC 19, AC 20 |
| screen `Conversa` | carregando e erro | AC 27, AC 28, AC 29 |
| screen `Conversa` | não autorizado e organização de outro dono | AC 12; existing - `404` com o título da Api |
| screen `Prévia do trecho` | carregando | existing - "Carregando trecho…" da web |
| screen `Prévia do trecho` | erro | AC 35 |
| screen `Lacunas` | vazio | AC 37 |
| screen `Lacunas` | carregando e erro de lista | existing - "Carregando lacunas…" e título do problem details |
| screen `Lacunas` | confirmação de ação destrutiva | AC 41 |
| API `login` e `refresh` | formato de erro e códigos | existing - respostas do Identity (`401`); AC 7, AC 12 |
| API `login` e `refresh` | quem chama, versionamento, rate limit | n/a - rotas públicas do Identity que já existem, sem mudança no servidor |

## Sources

- Pedido do usuário em 2026-11-26 - React Native com Expo, pasta nova no repo; v1 = login, organizações, chat com fontes e Lacunas
- Pedido do usuário em 2026-11-26 - guardrail: system design semelhante ao da aplicação web
- `.claude/agents/architecture-guardian.md` (seção Web) e `src/web/src/index.css` - regras e tokens que o app espelha
- `docs/PRD.md` e `src/web/src/features/{chat,gaps,auth}` - comportamento e textos de referência
- [Expo SDK 58](https://expo.dev/changelog/sdk-58) - versão atual do SDK
