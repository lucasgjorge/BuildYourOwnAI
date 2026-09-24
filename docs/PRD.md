# PRD - BuildYourOwnAI

**Status:** rascunho · **Data:** 2026-09-23 · **Etapa atual:** 1 (RAG MVP)

## 1. Visão

Uma plataforma SaaS em que qualquer pessoa cria a **própria IA**: um assistente com nome e
personalidade (instruções) que responde com base nos documentos que ela anexou, citando as fontes,
sem precisar programar nem entender de embeddings ou banco vetorial.

## 2. Problema

Quem quer uma IA que responda com base nos próprios documentos hoje tem duas saídas:

- **Montar sozinho:** extração de texto, chunking, embeddings, banco vetorial e prompt. Exige
  saber programar e integrar vários serviços.
- **Colar trechos num chat genérico:** o chat esquece o contexto, não cita a fonte e mistura os assuntos.

Ainda não temos dados de mercado (volume, conversão). Esta seção deve ser revisada com evidência
antes da etapa de monetização.

## 3. Público-alvo

| Persona | Necessidade | Exemplo |
| --- | --- | --- |
| Profissional independente | responder dúvidas recorrentes a partir do próprio material | consultor com manuais de processo |
| Pequena equipe | base de conhecimento interna consultável | políticas de RH, runbooks |
| Estudante | estudar a partir das próprias apostilas | PDFs de um curso |

Na etapa 1 o "tenant" é o usuário individual. Times e organizações ficam para depois.

## 4. Proposta de valor

1. Da conta criada à primeira resposta em minutos.
2. Respostas fundamentadas **só** nos documentos daquela IA, com as fontes citadas.
3. Várias IAs isoladas por usuário (ex.: "RH" e "Jurídico" não se misturam).

## 5. Roadmap por etapas

| Etapa | Objetivo | Principais capacidades |
| --- | --- | --- |
| **1 - RAG MVP** (atual) | provar o ciclo criar → anexar → perguntar | login e-mail/senha; CRUD de IAs; upload PDF/TXT/MD até 10 MB; pergunta com resposta e fontes; UI mínima |
| 2 - Conversa | experiência de chat real | histórico multi-turno, streaming, ingestão em background para arquivos grandes, mais formatos (DOCX, HTML, URL) |
| 3 - Publicar | a IA sai da plataforma | link público, widget embutível, API key por IA |
| 4 - Monetizar | receita | planos, quotas de uso (perguntas, armazenamento), cobrança |
| 5 - Times | uso organizacional | organizações, papéis, IAs compartilhadas |

## 6. Escopo da etapa 1

Especificação detalhada, com critérios de aceite: [.specs/features/rag-mvp/plan.md](../.specs/features/rag-mvp/plan.md).

**Dentro**
- Cadastro e login com e-mail e senha.
- Criar, listar e apagar IAs (nome + instruções).
- Anexar documentos PDF, TXT e MD (até 10 MB), com deduplicação por conteúdo. Listar e apagar documentos.
- Perguntar a uma IA e receber resposta + fontes (documento e trecho).
- Isolamento total entre usuários e entre IAs do mesmo usuário.
- Rate limit de 20 perguntas/min por usuário.
- UI web mínima para todo o fluxo.

**Fora**
Histórico de conversa, streaming, cobrança, compartilhamento/publicação, ingestão em background,
OCR, outros formatos, confirmação de e-mail/recuperação de senha, login social, escolha de modelo
por IA, times.

## 7. Requisitos não funcionais

| Tema | Requisito na etapa 1 |
| --- | --- |
| Segurança | recurso de outro usuário responde 404; segredos fora do repositório; nenhum conteúdo de documento, pergunta ou resposta nos logs |
| Privacidade | apagar uma IA ou um documento remove fisicamente os trechos e embeddings |
| Custo | embeddings em lote; rate limit em perguntas; limite de 10 MB por arquivo |
| Portabilidade de IA | provedor atrás de `Microsoft.Extensions.AI`; trocar OpenAI por outro é configuração |
| Operação | um único datastore (Postgres + pgvector); ambiente local sobe com `docker compose up` |
| Qualidade | testes automatizados contra Postgres real; nenhum teste chama a OpenAI |

## 8. Arquitetura (resumo)

- **Monorepo .NET 10:** `BuildYourOwnAI.Api` (Minimal APIs, Vertical Slice) e `src/web` (React + TypeScript + Vite, servido pela Api na mesma origem).
- **Dados:** Postgres 17 + pgvector; embeddings de 1536 dimensões, índice HNSW (cosseno).
- **IA:** OpenAI (`text-embedding-3-small`, chat configurável) via `Microsoft.Extensions.AI`.
- **Auth:** ASP.NET Core Identity com cookie de sessão HttpOnly (mesma origem).
- **Padrões:** impostos pelo agente `.claude/agents/architecture-guardian.md`; decisões em `.specs/STATE.md`.

## 9. Métricas de sucesso da etapa 1

Metas a validar com os primeiros usuários. Nenhuma tem baseline ainda.

| Métrica | Meta inicial |
| --- | --- |
| Usuários que chegam à primeira pergunta respondida após cadastro | ≥ 60% |
| Respostas com pelo menos uma fonte | ≥ 90% das perguntas em IAs com documentos |
| Uploads que falham por erro do sistema (5xx) | < 2% |
| Tempo de upload de um PDF de 1 MB | < 15 s (p95) |

## 10. Riscos e questões em aberto

| Risco / questão | Mitigação / status |
| --- | --- |
| Ingestão síncrona estoura o timeout com arquivos grandes | limite de 10 MB; background na etapa 2 |
| Trocar o modelo de embedding exige reprocessar tudo | dimensão fixada em uma decisão (AD-005); reprocessamento planejado antes de qualquer troca |
| Custo da OpenAI sem controle por usuário | rate limit na etapa 1; quotas na etapa 4 |
| PDFs escaneados sem texto | rejeitados com 422; OCR fora do escopo |
| Chave da OpenAI de produção | pendente - bloqueia o go-live, não o desenvolvimento |
