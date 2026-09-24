# BuildYourOwnAI

SaaS onde o usuário constrói as próprias IAs: cria uma organização, anexa documentos, cria IAs
nela e conversa no chat da organização, onde a escolha automática decide qual IA responde (RAG). Perguntas sem
resposta viram Lacunas. Produto: [docs/PRD.md](docs/PRD.md). Decisões de projeto: [.specs/STATE.md](.specs/STATE.md).

## tlc-spec-lean

profile: standard
budget: 150k

## Layout

```
src/BuildYourOwnAI.Api/          Minimal APIs, EF Core, Identity, IA
  Features/<Area>/<UseCase>.cs   um caso de uso por arquivo (endpoint + request/response + handler)
  Common/                        o que 2+ slices usam: AskPipeline, DocumentIngestion, GapRecorder
  Infrastructure/                DbContext, migrations, registro de IA, current user
src/web/                         React + TypeScript + Vite; `npm run build` gera o wwwroot da Api
  src/features/<area>/           páginas, api hooks, types e testes por área; tokens visuais em src/index.css
tests/BuildYourOwnAI.Api.Tests/  xUnit + WebApplicationFactory + Testcontainers (pgvector)
src/web/src/**/*.test.tsx        Vitest + Testing Library + MSW
docker-compose.yml               Postgres 17 + pgvector
```

## Commands

```bash
docker compose up -d                                  # Postgres em localhost:5432
dotnet build
dotnet test                                           # precisa do Docker (Testcontainers)
# Docker Desktop no Windows (contexto desktop-linux): no PowerShell, antes do dotnet test
#   $env:DOCKER_HOST = "npipe://./pipe/dockerDesktopLinuxEngine"
dotnet run --project src/BuildYourOwnAI.Api          # em Development cria admin@buildyourownai.local / Admin#2026
cd src/web && npm install && npm run dev             # Vite em localhost:5173, proxy /api -> Api
cd src/web && npm test
dotnet ef migrations add <Name> --project src/BuildYourOwnAI.Api
dotnet user-secrets set "AI:OpenAI:ApiKey" "<key>" --project src/BuildYourOwnAI.Api
dotnet user-secrets set "AI:OpenRouter:ApiKey" "<key>" --project src/BuildYourOwnAI.Api   # escolha automática da IA
dotnet user-secrets set "AI:OpenRouter:Model" "<model>" --project src/BuildYourOwnAI.Api
```

## Rules

Arquitetura e padrões: siga o agente [.claude/agents/architecture-guardian.md](.claude/agents/architecture-guardian.md)
e rode-o sobre o diff antes de declarar uma feature pronta. Resumo das regras que não se negociam:

- Todo acesso a `Assistant`/`Document`/`Chunk` passa por uma `Organization` resolvida pelo query filter de dono (AD-010); `Gap` tem dono próprio. Recurso de outro usuário responde 404.
- Nenhum handler usa o SDK da OpenAI direto. Use `IChatClient` / `IEmbeddingGenerator<string, Embedding<float>>`.
- Erro HTTP é sempre problem details (`Results.Problem` / `Results.ValidationProblem`).
- Testes não chamam a OpenAI. Use os fakes de IA; o banco é Postgres real via Testcontainers.
- Nunca logue conteúdo de documento, pergunta, resposta, token ou chave.
- Não faça `git push`, deploy ou alteração de dados de produção sem pedido explícito.
