# BuildYourOwnAI

SaaS onde cada usuário constrói as **próprias IAs**. Ele cria uma **organização**, sobe os documentos
(PDF, TXT, MD) e cria nela várias IAs com jeitos diferentes de responder (ex.: RH, Culture, Tech Team).
No **chat da organização**, a **escolha automática** decide qual IA responde cada pergunta. A resposta
cita os trechos dos documentos de onde veio (RAG), e clicar num trecho abre a prévia dele ao lado do chat. O que nenhuma IA sabe responder vira uma **Lacuna**: o dono responde
uma vez e a resposta passa a ser conhecimento da organização. Na aba **Estudar**, o produto gera perguntas de
múltipla escolha dos documentos escolhidos e, quando a resposta está errada, mostra a certa com o trecho de origem ao lado.

- Produto e roadmap: [docs/PRD.md](docs/PRD.md)
- Specs com critérios de aceite, um por feature: [.specs/features/](.specs/features/). Os mais recentes:
  - [.specs/features/org-chat/plan.md](.specs/features/org-chat/plan.md): chat da organização e página inicial
  - [.specs/features/source-preview/plan.md](.specs/features/source-preview/plan.md): prévia do trecho e escolha automática
- Decisões de arquitetura: [.specs/STATE.md](.specs/STATE.md)
- Padrões de código (e o agente que os revisa): [AGENTS.md](AGENTS.md), [.claude/agents/architecture-guardian.md](.claude/agents/architecture-guardian.md)

## Stack

| Parte | Tecnologia |
| --- | --- |
| API | .NET 10, Minimal APIs em Vertical Slice (`src/BuildYourOwnAI.Api`) |
| Web | React 19 + TypeScript + Vite, TanStack Query, Tailwind (`src/web`) |
| Mobile | React Native + Expo SDK 58, Expo Router, TanStack Query (`src/mobile`); em construção |
| Banco | Postgres 17 + pgvector (relacional e vetorial no mesmo banco) |
| Auth | ASP.NET Core Identity: cookie de sessão HttpOnly na web, token bearer no app mobile |
| IA | OpenAI (`text-embedding-3-small` + `gpt-4.1-mini`) via `Microsoft.Extensions.AI`; escolha automática da IA = primitiva "choice" da TypeSafe via OpenRouter (modelo `jev-latest`), atrás de `IRoutingChoice` |

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (Postgres local e testes de integração)
- Uma chave da API da OpenAI (sem ela o app sobe, mas upload e perguntas respondem `502`)
- Opcional: uma chave da OpenRouter para a escolha automática (sem ela o chat sempre pergunta ao usuário qual IA deve responder)

## Rodando localmente

### 1. Banco de dados

```bash
docker compose up -d
```

Sobe o Postgres com pgvector em `localhost:5432` (banco, usuário e senha: `byoai`). As migrations
são aplicadas automaticamente quando a API inicia em `Development`.

### 2. Chaves de IA

```bash
dotnet user-secrets set "AI:OpenAI:ApiKey" "sk-..." --project src/BuildYourOwnAI.Api
# Escolha automática (opcional): o nome nu do modelo, sem o prefixo "typesafe/"
dotnet user-secrets set "AI:OpenRouter:ApiKey" "sk-or-..." --project src/BuildYourOwnAI.Api
dotnet user-secrets set "AI:OpenRouter:Model" "jev-latest" --project src/BuildYourOwnAI.Api
```

A chave fica em user-secrets, fora do repositório. Os modelos podem ser trocados em
`src/BuildYourOwnAI.Api/appsettings.json` (`AI:ChatModel`, `AI:EmbeddingModel`). Trocar o modelo de
embedding exige reprocessar os documentos, porque a coluna é `vector(1536)`.

### 3. API

```bash
dotnet tool restore
dotnet run --project src/BuildYourOwnAI.Api --launch-profile http
```

A API fica em `http://localhost:5295`.

Em `Development`, a API cria na primeira subida uma conta pronta para usar (se ela ainda não existir):

| E-mail | Senha |
| --- | --- |
| `admin@buildyourownai.local` | `Admin#2026` |

Os valores vêm de `DevSeed:AdminEmail` e `DevSeed:AdminPassword` em
`src/BuildYourOwnAI.Api/appsettings.Development.json`. Apague a seção para não criar a conta. Ela é
criada no startup, não numa migration, então nunca existe em produção. O produto ainda não tem
papéis, então é uma conta comum.

### 4. Web (modo desenvolvimento)

Em outro terminal:

```bash
cd src/web
npm install
npm run dev
```

Abra `http://localhost:5173`. O Vite repassa `/api` para a API, então o cookie de sessão continua
na mesma origem.

1. A página inicial (`/`) explica o produto. Crie uma conta.
2. Crie uma organização. Você cai na aba **Base**: suba documentos e crie IAs, preenchendo "Quando usar esta IA".
3. Abra a aba **Conversa** e pergunte. Com "Escolha automática", a pergunta vai sozinha para a IA certa; fixe uma IA em "Para" para perguntar direto a ela. Clique num trecho das fontes para ver a prévia à direita.
4. Perguntas sem resposta aparecem em **Lacunas**. Responda e a resposta vira documento da organização.
5. Na aba **Estudar**, marque os documentos, escolha 5, 10 ou 20 perguntas e clique em "Começar". Errou? A certa aparece com o trecho de origem à direita.

### 5. App mobile (React Native + Expo, em construção)

Estão prontas as telas de entrada, organizações e conversa (sem abrir o trecho citado e sem Lacunas). O que falta e como implementar cada tela:
[.specs/features/mobile-app/plan.md](.specs/features/mobile-app/plan.md), seção "Execução".

**Você precisa de**
- Node.js 22.13 ou mais novo.
- O app **Expo Go** atualizado no telefone, com suporte ao SDK 58 (no iPhone, versão 58 ou mais nova, que exige iOS 16.4+).
- Uma conta Expo (gratuita, em [expo.dev](https://expo.dev)): o Expo Go no iPhone só abre o projeto com a mesma conta no app e no computador.
- Telefone e computador na mesma rede Wi-Fi. Use só uma rede confiável: em desenvolvimento, senha e token trafegam em HTTP, sem criptografia.

**a. API acessível pela rede.** O `dotnet run` do passo 3 só escuta no próprio computador. Para o app, pare-o e suba assim (a web continua funcionando):

```powershell
dotnet run --project src/BuildYourOwnAI.Api --launch-profile http --urls http://0.0.0.0:5295
```

Mantenha o `--launch-profile http`: sem ele a API sobe fora de `Development` e não acha o banco.

**b. Endereço da API.** Descubra o IP do computador na rede (campo "Endereço IPv4" do adaptador Wi-Fi):

```powershell
ipconfig
```

Crie o `.env` a partir do exemplo e troque o IP em `EXPO_PUBLIC_API_URL`. Não use `localhost`: no telefone, `localhost` é o próprio telefone. O arquivo não vai para o git.

```powershell
Copy-Item src/mobile/.env.example src/mobile/.env
```

**c. Instalar e iniciar o Expo**

```powershell
npm --prefix src/mobile install
```

Entre com a sua conta Expo (só na primeira vez; ele pede usuário e senha no terminal) e confira que o Expo Go no telefone está na **mesma conta**:

```powershell
cd src/mobile; npx expo login
```

```powershell
npm start
```

**d. Abrir no telefone.** Leia o QR code do terminal com a câmera (iPhone) ou pelo Expo Go (Android), e entre com a sua conta da web (a de desenvolvimento do passo 3 não tem organizações). O token vale uma hora e ainda não é renovado: se as telas começarem a dar erro, toque em "Sair" e entre de novo. Mudanças no código recarregam sozinhas; mudança no `.env` exige reiniciar o Expo.

### Build de produção (um processo só)

### Build de produção (um processo só)

```bash
npm --prefix src/web run build   # gera src/BuildYourOwnAI.Api/wwwroot
dotnet run --project src/BuildYourOwnAI.Api --launch-profile http
```

A API passa a servir o SPA em `http://localhost:5295`. Rotas desconhecidas fora de `/api` devolvem
o `index.html`, e rotas `/api` desconhecidas devolvem 404 em problem details.

## Testes

### Backend (xUnit + Testcontainers)

Os testes sobem um Postgres com pgvector descartável via Docker e usam fakes determinísticos no
lugar da OpenAI. Nenhum teste chama a API real.

```bash
dotnet test
```

Com Docker Desktop no Windows (contexto `desktop-linux`), aponte o Testcontainers para o engine
antes, **no PowerShell**:

```powershell
$env:DOCKER_HOST = "npipe://./pipe/dockerDesktopLinuxEngine"
dotnet test
```

Para rodar um teste só: `dotnet test tests/BuildYourOwnAI.Api.Tests --filter "FullyQualifiedName~AskTests"`.

### Web (Vitest + Testing Library + MSW)

```bash
npm --prefix src/web run test
npm --prefix src/web run test -- -t "chat sends to organization routing"   # um teste só
```

### Mobile (Jest + `jest-expo` + Testing Library)

Rodam no computador, sem telefone e sem a API (HTTP e armazenamento do aparelho são simulados).

```bash
npm --prefix src/mobile test
npm --prefix src/mobile test -- -t "login stores tokens and opens organizations"   # um teste só
npm --prefix src/mobile run typecheck
npm --prefix src/mobile run bundle   # gera o bundle Android e iOS, para pegar erro que o teste não pega
```

## Estrutura

```
src/BuildYourOwnAI.Api/
  Features/<Area>/<UseCase>.cs   um caso de uso por arquivo (endpoint + DTOs + handler)
  Common/                        pipeline de pergunta, ingestão, lacunas, chunker, erros compartilhados
  Infrastructure/                DbContext, migrations, usuário atual, registro da IA
src/web/src/
  features/<area>/               páginas, hooks, tipos e testes por área (home, chat, organizations, routing, gaps, auth)
  shared/                        layout com a barra lateral, componentes de UI, cores das IAs
  shared/api/client.ts           único cliente HTTP (problem details → ApiError)
  index.css                      tokens de cor e tipografia (Tailwind @theme)
src/mobile/src/
  app/                           rotas do Expo Router (só declaram a tela)
  features/<area>/               telas, hooks, tipos e testes, como na web
  shared/                        cliente HTTP, sessão, componentes e theme.ts (mesmas cores da web)
tests/BuildYourOwnAI.Api.Tests/  testes de integração e de unidade da API
.specs/                          spec, checks e relatórios de verificação (tlc-spec-lean)
```

## API

Todas as rotas fora de `/api/auth` exigem sessão. Recurso de outro usuário responde `404`. Os erros
saem sempre como `application/problem+json`.

| Rota | O que faz |
| --- | --- |
| `POST /api/auth/register` · `POST /api/auth/login?useCookies=true` · `POST /api/auth/logout` · `GET /api/auth/manage/info` | conta e sessão |
| `POST/GET /api/organizations` · `GET/DELETE /api/organizations/{id}` | criar, listar, ver e apagar organizações (apagar leva IAs, documentos e lacunas) |
| `POST/GET /api/organizations/{id}/documents` · `DELETE .../documents/{documentId}` | anexar (até 10 MB, PDF/TXT/MD), listar e apagar documentos da organização |
| `POST /api/assistants` · `GET/DELETE /api/assistants/{id}` | criar (com `organizationId` e `routingDescription`), ver e apagar IAs |
| `POST /api/assistants/{id}/ask` | perguntar direto a uma IA; devolve `answer`, `found` e `sources` |
| `POST /api/organizations/{id}/route/ask` | perguntar com escolha automática entre as IAs da organização; `kind` = `answered`, `clarify` ou `noMatch` |
| `POST /api/route/ask` | perguntar com escolha automática entre as IAs de todas as organizações |
| `GET /api/organizations/{id}/documents/{documentId}/chunks/{index}?around=1` | o trecho citado e seus vizinhos, para a prévia |
| `GET /api/gaps` · `POST /api/gaps/{id}/answer` · `POST /api/gaps/{id}/dismiss` | lacunas abertas; responder (vira documento) ou dispensar |
| `POST /api/organizations/{id}/study-sessions` | gera perguntas de múltipla escolha (`questionCount` 5, 10 ou 20; `documentIds` opcional), sem o gabarito |
| `POST /api/study-sessions/{sessionId}/questions/{questionId}/answer` | corrige uma resposta (`option` 0 a 3) e devolve a certa, a explicação e o trecho de origem |

As três rotas de pergunta e a criação de sessão de estudo somam no mesmo limite de 20 chamadas por minuto por usuário.

## Problemas comuns

| Sintoma | Causa / solução |
| --- | --- |
| Upload ou pergunta respondem `502` | Chave da OpenAI ausente ou inválida (passo 2) |
| O chat sempre pergunta "Qual destas IAs deve responder?" com as IAs em ordem alfabética | A escolha automática falhou: falta `AI:OpenRouter:*`, a chave é inválida ou o modelo não é `jev-latest` (o log mostra `Routing call failed`). Em ordem de probabilidade, é só confiança abaixo de `AI:Routing:ConfidenceThreshold` (0.6) |
| O chat responde que nenhuma IA está disponível (`422`) | Nenhuma IA da organização tem "Quando usar esta IA" preenchido |
| `DockerUnavailableException` nos testes | Docker parado, ou falta o `DOCKER_HOST` no Windows (veja Testes). O `docker compose` funciona sem ele |
| A API não conecta no banco | `docker compose up -d` não rodou, ou a porta 5432 está ocupada por outro Postgres |
| Upload responde `422` | O arquivo não tem texto extraível (por exemplo, PDF escaneado; OCR está fora da etapa 1) |
| Expo Go: "Project is incompatible with this version of Expo Go" | Atualize o Expo Go na loja; o projeto usa o SDK 58 |
| O app abre, mas mostra "Não foi possível falar com o servidor." | API sem `--urls http://0.0.0.0:5295`, IP errado no `src/mobile/.env` (reinicie o Expo depois de mudar) ou firewall. Teste `http://<IP>:5295` no navegador do telefone |
| O Expo Go não carrega o projeto | Telefone em outra rede, ou o firewall do Windows bloqueia o Node (comum em rede marcada como Pública). Teste `http://<IP>:8081/status` no navegador do telefone |
| "You need to be signed in to Expo Go and Expo CLI to open your project" | Rode `npx expo login` em `src/mobile`, entre com a mesma conta no Expo Go e reinicie o Expo |
| O app mostra "Defina EXPO_PUBLIC_API_URL" | Falta o `src/mobile/.env` (passo 5b) |
| O telefone continua mostrando uma tela antiga | O Expo Go reaproveitou o bundle guardado. Feche o Expo Go e suba em outra porta, que vira um endereço novo: `npx expo start --clear --port 8082` em `src/mobile` |
