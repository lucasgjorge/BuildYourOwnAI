# Project state

## Decisions

| ID | Decision | Rationale | Status | Date |
| --- | --- | --- | --- | --- |
| AD-001 | Monorepo: `src/BuildYourOwnAI.Api` (.NET 10, Minimal APIs) + `src/web` (React + TypeScript + Vite) servido pela Api na mesma origem; `tests/*` | um deploy só; mesma origem permite cookie `SameSite=Strict` sem CORS | active | 2026-09-23 |
| AD-002 | Backend em Vertical Slice: `Features/<Area>/<UseCase>.cs`, `DbContext` direto no handler, sem repositório/MediatR | escolha do usuário; menos cerimônia por feature | active | 2026-09-23 |
| AD-003 | Postgres + pgvector é o único datastore (relacional e vetorial) | transação e join com a posse do dado; um serviço a operar | active | 2026-09-23 |
| AD-004 | Provedor de IA atrás de `Microsoft.Extensions.AI` (`IChatClient`, `IEmbeddingGenerator`); OpenAI é o adapter atual | trocar de provedor = trocar o registro de DI; fakes nos testes | active | 2026-09-23 |
| AD-005 | Embeddings com 1536 dimensões (`text-embedding-3-small`), índice HNSW cosine | trocar o modelo de embedding exige reprocessar todos os chunks | active | 2026-09-23 |
| AD-006 | Isolamento por usuário via query filter global em `Assistant`; recurso de outro usuário responde 404 | um filtro esquecido não pode vazar dados | active | 2026-09-23 |
| AD-007 | Erros HTTP sempre como problem details (RFC 9457) | um formato de erro só, que a web sabe exibir | active | 2026-09-23 |
| AD-008 | Ingestão de documentos síncrona e atômica (1 transação), limite de 10 MB | escolha do usuário; mudar para background exige status + worker | active | 2026-09-23 |
| AD-009 | Autenticação com ASP.NET Core Identity (`MapIdentityApi`, `useCookies=true`), cookie HttpOnly `SameSite=Strict` | SPA não guarda token no browser (XSS); sem sessão feita à mão; migrável para IdP externo | active | 2026-09-23 |

## Handoff

**Feature**: rag-mvp
**Where**: C1-C51 fechados; Verifier rodada 3 = PASS em `875d5c6`; `validate_verification.py` exit 0
**In progress**: nada
**Next step**: configurar `AI:OpenAI:ApiKey` e testar o fluxo real no navegador; depois planejar a etapa 2 (conversa)
**Blockers**: chave da OpenAI (blocks go-live)
**Uncommitted**: nada
**Branch**: main (sem remoto)
