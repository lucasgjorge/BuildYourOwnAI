# PRD - BuildYourOwnAI

**Status:** rascunho · **Data:** 2026-09-24 · **Etapa atual:** 1 (RAG MVP) entregue, mais organizações, escolha automática e Lacunas

## 1. Visão

Uma plataforma SaaS em que qualquer pessoa cria as **próprias IAs**: assistentes com nome e
personalidade (instruções) que respondem com base nos documentos que ela anexou, citando as fontes,
sem precisar programar nem entender de embeddings ou banco vetorial.

As IAs vivem numa **organização**, que guarda os documentos. Num chat só, a **escolha automática**
decide qual IA responde cada pergunta, e a resposta mostra o trecho do documento que a sustenta. O que nenhuma IA sabe responder vira uma **Lacuna** que o dono fecha uma vez.

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

O "tenant" continua sendo o usuário individual. Ele já agrupa as IAs em **organizações** (com dono
único, AD-010). Membros e papéis nas organizações ficam para a etapa 5.

## 4. Proposta de valor

1. Da conta criada à primeira resposta em minutos.
2. Respostas fundamentadas **só** nos documentos daquela IA, com as fontes citadas.
3. Várias IAs por organização, cada uma com um jeito de responder, sobre os mesmos documentos. Organizações diferentes não se misturam.
4. **Escolha automática:** o usuário pergunta num chat só e a pergunta vai para a IA certa. Cada resposta diz quem respondeu, e dá para perguntar a outra IA com um clique.
5. **Lacunas:** a pergunta que nenhuma IA soube responder vira tarefa do dono, e a resposta dele passa a ser conhecimento da organização.
6. **Study Mode:** o produto pergunta de volta. Gera perguntas de múltipla escolha dos documentos escolhidos e, quando a resposta está errada, mostra a certa com o trecho de origem.

## 5. Roadmap por etapas

| Etapa | Objetivo | Principais capacidades |
| --- | --- | --- |
| **1 - RAG MVP** (entregue) | provar o ciclo criar → anexar → perguntar | login e-mail/senha; CRUD de IAs; upload PDF/TXT/MD até 10 MB; pergunta com resposta e fontes; UI mínima |
| **1b - Organizações, escolha automática e Lacunas** (entregue) | várias IAs sobre o mesmo material, sem escolher na mão | organizações com documentos compartilhados; escolha automática por organização e entre todas (primitiva "choice" da TypeSafe via OpenRouter, modelo `jev-latest`); prévia do trecho citado; Study Mode com correção e trecho de origem; caixa de Lacunas; chat da organização como tela principal; página inicial pública |
| 2 - Conversa | experiência de chat real | histórico persistido e multi-turno (hoje a thread vive só na tela), streaming, ingestão em background para arquivos grandes, mais formatos (DOCX, HTML, URL) |
| 3 - Publicar | a IA sai da plataforma | link público do chat da organização (com a escolha automática), widget embutível, WhatsApp, API key |
| 4 - Monetizar | receita | planos, quotas de uso (perguntas, armazenamento), cobrança |
| 5 - Times | uso organizacional | membros, papéis e convites nas organizações que já existem |

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
| Modelo da escolha automática | `jev-latest` (TypeSafe "choice") via OpenRouter; limiar de confiança 0.6 ainda sem calibração com perguntas reais |
| Pergunta e descrições das IAs vão para um terceiro (OpenRouter) | só nome, organização e "Quando usar" vão ao roteador; nunca instruções nem documentos |
