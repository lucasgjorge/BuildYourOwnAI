# BuildYourOwnAI

SaaS onde cada usuário constrói a **própria IA**: cria um assistente com nome e instruções, anexa
documentos (PDF, TXT, MD) e faz perguntas que são respondidas **só com o conteúdo daquela IA**, com
as fontes citadas (RAG).

- Produto e roadmap: [docs/PRD.md](docs/PRD.md)
- Spec da etapa 1 (critérios de aceite): [.specs/features/rag-mvp/plan.md](.specs/features/rag-mvp/plan.md)
- Decisões de arquitetura: [.specs/STATE.md](.specs/STATE.md)
- Padrões de código (e o agente que os revisa): [AGENTS.md](AGENTS.md), [.claude/agents/architecture-guardian.md](.claude/agents/architecture-guardian.md)

## Stack

| Parte | Tecnologia |
| --- | --- |
| API | .NET 10, Minimal APIs em Vertical Slice (`src/BuildYourOwnAI.Api`) |
| Web | React 19 + TypeScript + Vite, TanStack Query, Tailwind (`src/web`) |
| Banco | Postgres 17 + pgvector (relacional e vetorial no mesmo banco) |
| Auth | ASP.NET Core Identity, cookie de sessão HttpOnly na mesma origem |
| IA | OpenAI (`text-embedding-3-small` + `gpt-4.1-mini`) via `Microsoft.Extensions.AI` |

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (Postgres local e testes de integração)
- Uma chave da API da OpenAI (sem ela o app sobe, mas upload e perguntas respondem `502`)

## Rodando localmente

### 1. Banco de dados

```bash
docker compose up -d
```

Sobe o Postgres com pgvector em `localhost:5432` (banco, usuário e senha: `byoai`). As migrations
são aplicadas automaticamente quando a API inicia em `Development`.

### 2. Chave da OpenAI

```bash
dotnet user-secrets set "AI:OpenAI:ApiKey" "sk-..." --project src/BuildYourOwnAI.Api
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

### 4. Web (modo desenvolvimento)

Em outro terminal:

```bash
cd src/web
npm install
npm run dev
```

Abra `http://localhost:5173`. O Vite repassa `/api` para a API, então o cookie de sessão continua
na mesma origem. Crie uma conta, crie uma IA, anexe um documento e pergunte.

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
npm --prefix src/web run test -- -t "shows empty state"   # um teste só
```

## Estrutura

```
src/BuildYourOwnAI.Api/
  Features/<Area>/<UseCase>.cs   um caso de uso por arquivo (endpoint + DTOs + handler)
  Common/                        chunker, extração de texto, erros compartilhados
  Infrastructure/                DbContext, migrations, usuário atual, registro da IA
src/web/src/
  features/<area>/               páginas, hooks e testes por área
  shared/api/client.ts           único cliente HTTP (problem details → ApiError)
tests/BuildYourOwnAI.Api.Tests/  testes de integração e de unidade da API
.specs/                          spec, checks e relatórios de verificação (tlc-spec-lean)
```

## API

Todas as rotas `/api/assistants*` exigem sessão. Os erros saem sempre como `application/problem+json`.

| Rota | O que faz |
| --- | --- |
| `POST /api/auth/register` · `POST /api/auth/login?useCookies=true` · `POST /api/auth/logout` · `GET /api/auth/manage/info` | conta e sessão |
| `POST/GET /api/assistants` · `GET/DELETE /api/assistants/{id}` | criar, listar, ver e apagar IAs |
| `POST/GET /api/assistants/{id}/documents` · `DELETE .../documents/{documentId}` | anexar (até 10 MB, PDF/TXT/MD), listar e apagar documentos |
| `POST /api/assistants/{id}/ask` | perguntar (até 20 por minuto por usuário); devolve `answer` e `sources` |

## Problemas comuns

| Sintoma | Causa / solução |
| --- | --- |
| Upload ou pergunta respondem `502` | Chave da OpenAI ausente ou inválida (passo 2) |
| `DockerUnavailableException` nos testes | Docker parado, ou falta o `DOCKER_HOST` no Windows (veja Testes) |
| A API não conecta no banco | `docker compose up -d` não rodou, ou a porta 5432 está ocupada por outro Postgres |
| Upload responde `422` | O arquivo não tem texto extraível (por exemplo, PDF escaneado; OCR está fora da etapa 1) |
