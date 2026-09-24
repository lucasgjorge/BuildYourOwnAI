# Project state

## Decisions

| ID | Decision | Rationale | Status | Date |
| --- | --- | --- | --- | --- |
| AD-001 | Monorepo: `src/BuildYourOwnAI.Api` (.NET 10, Minimal APIs) + `src/web` (React + TypeScript + Vite) servido pela Api na mesma origem; `tests/*` | um deploy só; mesma origem permite cookie `SameSite=Strict` sem CORS | active | 2026-09-23 |
| AD-002 | Backend em Vertical Slice: `Features/<Area>/<UseCase>.cs`, `DbContext` direto no handler, sem repositório/MediatR | escolha do usuário; menos cerimônia por feature | active | 2026-09-23 |
| AD-003 | Postgres + pgvector é o único datastore (relacional e vetorial) | transação e join com a posse do dado; um serviço a operar | active | 2026-09-23 |
| AD-004 | Provedor de IA atrás de `Microsoft.Extensions.AI` (`IChatClient`, `IEmbeddingGenerator`); OpenAI é o adapter atual | trocar de provedor = trocar o registro de DI; fakes nos testes | active | 2026-09-23 |
| AD-005 | Embeddings com 1536 dimensões (`text-embedding-3-small`), índice HNSW cosine | trocar o modelo de embedding exige reprocessar todos os chunks | active | 2026-09-23 |
| AD-006 | Isolamento por usuário via query filter global em `Assistant`; recurso de outro usuário responde 404 | um filtro esquecido não pode vazar dados | superseded by AD-010 | 2026-09-23 |
| AD-007 | Erros HTTP sempre como problem details (RFC 9457) | um formato de erro só, que a web sabe exibir | active | 2026-09-23 |
| AD-008 | Ingestão de documentos síncrona e atômica (1 transação), limite de 10 MB | escolha do usuário; mudar para background exige status + worker | active | 2026-09-23 |
| AD-009 | Autenticação com ASP.NET Core Identity (`MapIdentityApi`, `useCookies=true`), cookie HttpOnly `SameSite=Strict` | SPA não guarda token no browser (XSS); sem sessão feita à mão; migrável para IdP externo | active | 2026-09-23 |
| AD-010 | `Organization` é a raiz de posse: query filter `OwnerId == CurrentUserId` em `Organization`; `Assistant`, `Document` e `Chunk` filtram pela organização; documentos pertencem à organização e são compartilhados pelas IAs dela; `Gap` tem `OwnerId` próprio. Recurso de outro usuário responde 404 | várias IAs sobre o mesmo material sem duplicar embeddings; a mesma entidade ganha membros na etapa 5 (jev-gaps, door 1-2) | active | 2026-09-24 |
| AD-011 | Segundo `IChatClient` keyed `"router"` (OpenRouter via endpoint compatível com OpenAI, `AI:OpenRouter:*`) só para o roteamento do Jev; respostas e embeddings continuam no cliente padrão | escolha do usuário; mantém AD-004 (handler só vê a abstração) (jev-gaps, door 4) | superseded by AD-012 | 2026-09-24 |
| AD-012 | O roteador do Jev é a primitiva "choice" da TypeSafe (`jev-latest`) via OpenRouter, `POST {base}systemone`, atrás de `IJevChoice` (`Infrastructure/Ai/JevChoice.cs`); opções por posição (`"1".."N"`) mais `"nenhuma"`; decisão por `confidence >= AI:Jev:ConfidenceThreshold` (0.6) | `jev-latest` não é modelo de chat: via `IChatClient` toda chamada falhava e o Jev caía sempre no fallback (jev-choice, doors 1-2) | active | 2026-09-24 |
| AD-013 | O produto não usa "Jev" como nome: a função é "escolha automática" na UI e `Routing` no código (rotas `/api/route/ask` e `/api/organizations/{id}/route/ask`, `IRoutingChoice`, `AI:Routing:*`, tokens `--color-route*`). "jev-latest" só aparece como id do modelo do provedor na configuração. Specs antigos e a migration `OrganizationsJevGaps` ficam como histórico | "Jev" vai ser o nome de um negócio (source-preview, door 2) | active | 2026-09-24 |

## Handoff

**Feature**: org-chat (chat da organização com Jev, identidade visual nova, página inicial)
**Where**: C1-C33 fechados; Verifier rodada 2 = PASS em `932c987`; `validate_verification.py` exit 0. jev-gaps também PASS (rodada 2 em `95be488`)
**In progress**: nada
**Next step**: trocar `AI:OpenRouter:Model` por um id válido da OpenRouter (hoje `jev-latest`) e testar o Jev de verdade; depois a etapa 2 (histórico persistido e streaming)
**Blockers**: modelo da OpenRouter e chave da OpenAI de produção (blocks go-live)
**Uncommitted**: nada
**Branch**: main (não enviado ao origin). Dev: conta `admin@buildyourownai.local` criada no startup em Development
