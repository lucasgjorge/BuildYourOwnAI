# BuildYourOwnAI

SaaS onde cada usuário constrói as **próprias IAs**. Ele cria uma **organização**, sobe os documentos
(PDF, TXT, MD) e cria nela várias IAs com jeitos diferentes de responder (ex.: RH, Culture, Tech Team).
No **chat da organização**, o **Jev** escolhe qual IA responde cada pergunta. A resposta cita os
documentos de onde veio (RAG). O que nenhuma IA sabe responder vira uma **Lacuna**: o dono responde
uma vez e a resposta passa a ser conhecimento da organização.

- Produto e roadmap: [docs/PRD.md](docs/PRD.md)
- Specs com critérios de aceite:
  - [.specs/features/rag-mvp/plan.md](.specs/features/rag-mvp/plan.md): contas, documentos, perguntas
  - [.specs/features/jev-gaps/plan.md](.specs/features/jev-gaps/plan.md): organizações, Jev e Lacunas
  - [.specs/features/org-chat/plan.md](.specs/features/org-chat/plan.md): chat da organização e página inicial
- Decisões de arquitetura: [.specs/STATE.md](.specs/STATE.md)
- Padrões de código (e o agente que os revisa): [AGENTS.md](AGENTS.md), [.claude/agents/architecture-guardian.md](.claude/agents/architecture-guardian.md)

## Stack

| Parte | Tecnologia |
| --- | --- |
| API | .NET 10, Minimal APIs em Vertical Slice (`src/BuildYourOwnAI.Api`) |
| Web | React 19 + TypeScript + Vite, TanStack Query, Tailwind (`src/web`) |
| Banco | Postgres 17 + pgvector (relacional e vetorial no mesmo banco) |
| Auth | ASP.NET Core Identity, cookie de sessão HttpOnly na mesma origem |
| IA | OpenAI (`text-embedding-3-small` + `gpt-4.1-mini`) via `Microsoft.Extensions.AI`; roteador do Jev via OpenRouter (cliente keyed `router`) |

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (Postgres local e testes de integração)
- Uma chave da API da OpenAI (sem ela o app sobe, mas upload e perguntas respondem `502`)
- Opcional: uma chave da OpenRouter para o Jev (sem ela o Jev sempre pergunta ao usuário qual IA deve responder)

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
# Jev (opcional): um modelo da OpenRouter que aceite resposta em JSON, no formato fornecedor/modelo
dotnet user-secrets set "AI:OpenRouter:ApiKey" "sk-or-..." --project src/BuildYourOwnAI.Api
dotnet user-secrets set "AI:OpenRouter:Model" "<fornecedor/modelo>" --project src/BuildYourOwnAI.Api
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
3. Abra a aba **Conversa** e pergunte. Com "Jev decide", o Jev escolhe a IA; fixe uma IA em "Para" para perguntar direto a ela.
4. Perguntas sem resposta aparecem em **Lacunas**. Responda e a resposta vira documento da organização.

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
npm --prefix src/web run test -- -t "chat sends to organization jev"   # um teste só
```

## Estrutura

```
src/BuildYourOwnAI.Api/
  Features/<Area>/<UseCase>.cs   um caso de uso por arquivo (endpoint + DTOs + handler)
  Common/                        pipeline de pergunta, ingestão, lacunas, chunker, erros compartilhados
  Infrastructure/                DbContext, migrations, usuário atual, registro da IA
src/web/src/
  features/<area>/               páginas, hooks, tipos e testes por área (home, chat, organizations, jev, gaps, auth)
  shared/                        layout com a barra lateral, componentes de UI, cores das IAs
  shared/api/client.ts           único cliente HTTP (problem details → ApiError)
  index.css                      tokens de cor e tipografia (Tailwind @theme)
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
| `POST /api/organizations/{id}/jev/ask` | perguntar ao Jev da organização; `kind` = `answered`, `clarify` ou `noMatch` |
| `POST /api/jev/ask` | perguntar ao Jev global (IAs de todas as organizações) |
| `GET /api/gaps` · `POST /api/gaps/{id}/answer` · `POST /api/gaps/{id}/dismiss` | lacunas abertas; responder (vira documento) ou dispensar |

As três rotas de pergunta somam no mesmo limite de 20 perguntas por minuto por usuário.

## Problemas comuns

| Sintoma | Causa / solução |
| --- | --- |
| Upload ou pergunta respondem `502` | Chave da OpenAI ausente ou inválida (passo 2) |
| O Jev sempre pergunta "Qual destas IAs deve responder?" | Falta `AI:OpenRouter:*`, o modelo não existe na OpenRouter ou não devolve JSON (passo 2) |
| O Jev responde que nenhuma IA está disponível (`422`) | Nenhuma IA da organização tem "Quando usar esta IA" preenchido |
| `DockerUnavailableException` nos testes | Docker parado, ou falta o `DOCKER_HOST` no Windows (veja Testes). O `docker compose` funciona sem ele |
| A API não conecta no banco | `docker compose up -d` não rodou, ou a porta 5432 está ocupada por outro Postgres |
| Upload responde `422` | O arquivo não tem texto extraível (por exemplo, PDF escaneado; OCR está fora da etapa 1) |
