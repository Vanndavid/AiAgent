# Job Assistant (SaaS foundation)

Monorepo for a job-application assistant: track applications, retrieve similar job descriptions via RAG, and run a callable ReAct AI agent that can use those services as tools.

**Stack:** ASP.NET Core 8 API · React (Vite + TypeScript) · PostgreSQL · FastAPI RAG (FAISS) · FastAPI AI agent

Local/dev connectivity is wired first; AWS (for example API Gateway + managed Postgres) can sit in front later.

---

## What works today

| Capability | Status |
|------------|--------|
| Stack health dashboard (`/`) | Probes API, Postgres, RAG, AI agent |
| Job applications CRUD (`/applications`) | Create / list / update / delete with status workflow |
| Applications filters | Status, search (company/role), sort + optimistic inline status/delete |
| AI agent UI (`/agent`) | Runs goals via `POST /api/agent/run`; shows recent saved runs |
| Agent → applications tools | `list_applications`, `get_application`, `create_application` |
| Agent → RAG tools | `rag_retrieve`, `rag_ingest` |
| RAG text ingest / query | `POST /rag/ingest`, `POST /rag/query` (+ vector search) |
| Versioned SQL migrations | `Data/Migrations` + `schema_migrations` on API startup |
| Agent run history | Persisted in Postgres; `GET /api/agent/runs` |
| Fake LLM + FakeEmbeddings | Deterministic local demos **without API keys** |
| Dev bring-up script | `./scripts/dev-up.sh` (Compose + API + Vite) |
| Automated tests | .NET xUnit (query/migrations), agent policy tests, RAG round-trip |

---

## Layout

| Path | Role |
|------|------|
| `backend/JobAssistant.Api` | .NET 8 minimal API; CORS for Vite; migrations; applications + agent run history |
| `backend/JobAssistant.Api.Tests` | xUnit tests for migration discovery and list-query parsing |
| `frontend` | React + TypeScript UI (stack status, applications, agent) via Vite `/api` proxy |
| `services/rag-api` | FastAPI + FAISS + LangChain; text ingest/query; persists under `var/faiss` or `/data/faiss` |
| `services/ai-agent` | FastAPI ReAct agent with applications + RAG + research/save tools; `POST /agent/run` |
| `docker-compose.yml` | Postgres + RAG + AI agent container definitions |
| `scripts/dev-up.sh` | One-command local bring-up (Compose + API + Vite) |
| `scripts/verify-dev-connectivity.sh` | Smoke script for `/api/stack-status` |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+ (for Vite)
- Python 3.12+ (optional if you only run RAG / agent in Docker)
- Docker Desktop or Docker Engine + Compose (for Postgres and containerized Python services)

---

## Configuration

Copy `.env.example` to `.env` in the repo root if you want to override defaults.

| Variable | Used by | Purpose |
|----------|---------|---------|
| `ConnectionStrings__PostgreSQL` | .NET API | Npgsql connection string (defaults match Compose) |
| `Services__RagApiBaseUrl` | .NET API | RAG base URL (default `http://localhost:8001`) |
| `Services__AiAgentBaseUrl` | .NET API | AI agent base URL (default `http://localhost:8002`) |
| `JOB_ASSISTANT_API_BASE_URL` | AI agent | Where agent tools call `/api/applications` |
| `RAG_API_BASE_URL` | AI agent | Where agent tools call `/rag/*` |
| `POSTGRES_PORT` / `RAG_PORT` / `AI_AGENT_PORT` | Compose | Host port overrides |

The Vite dev server proxies `/api` to `http://127.0.0.1:5287` (see `frontend/vite.config.ts`). The API listens on **5287**.

---

## Run everything locally

**Option A — one script** (requires Docker):

```bash
chmod +x scripts/dev-up.sh
./scripts/dev-up.sh
```

Starts Compose (Postgres + RAG + agent), the .NET API on `:5287`, and Vite on `http://localhost:5173`. Ctrl+C stops API/Vite; Compose keeps running.

**Option B — manual**

1. **Infrastructure**

   ```bash
   docker compose up -d --build
   ```

   Starts Postgres (`localhost:5432`), RAG (`localhost:8001`), and the AI agent (`localhost:8002`).

   > **Port conflict?** Copy `.env.example` → `.env` and change `POSTGRES_PORT` (and the matching port in `ConnectionStrings__PostgreSQL`). Same for `RAG_PORT` / `Services__RagApiBaseUrl` or `AI_AGENT_PORT` / `Services__AiAgentBaseUrl`.

2. **.NET API** (from repo root)

   ```bash
   dotnet run --project backend/JobAssistant.Api/JobAssistant.Api.csproj
   ```

3. **React**

   ```bash
   cd frontend && npm install && npm run dev
   ```

   Open the printed URL (typically `http://localhost:5173`).

### RAG service without Docker

```bash
cd services/rag-api
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --host 127.0.0.1 --port 8001
```

FAISS store defaults to `services/rag-api/var/faiss/` (`jobs.faiss` + `jobs.pkl`). Seed job chunks are created on first start.

```bash
curl -fsS -X POST http://127.0.0.1:8001/rag/ingest \
  -H 'Content-Type: application/json' \
  -d '{"texts":["Example Co seeks a Backend Engineer with ASP.NET and Postgres."],"metadatas":[{"company":"Example Co","role":"Backend Engineer"}]}'

curl -fsS -X POST http://127.0.0.1:8001/rag/query \
  -H 'Content-Type: application/json' \
  -d '{"query":"backend engineer postgresql","k":3}'
```

### AI agent without Docker

```bash
cd services/ai-agent
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
export JOB_ASSISTANT_API_BASE_URL=http://127.0.0.1:5287
export RAG_API_BASE_URL=http://127.0.0.1:8001
uvicorn app.main:app --reload --host 127.0.0.1 --port 8002
```

```bash
curl -fsS -X POST http://127.0.0.1:8002/agent/run \
  -H 'Content-Type: application/json' \
  -d '{"goal":"List my job applications and retrieve similar backend engineer roles from RAG."}'
```

Or through the .NET API: `POST /api/agent/run` (UI at `/agent`).

| Tool | Calls |
|------|--------|
| `list_applications` / `get_application` / `create_application` | .NET `/api/applications` |
| `rag_retrieve` / `rag_ingest` | RAG `/rag/query` and `/rag/ingest` |
| `web_search` / `save_file` | Stub search + local notes |

---

## Verify connectivity

```bash
chmod +x scripts/verify-dev-connectivity.sh
./scripts/verify-dev-connectivity.sh
```

Builds the API, briefly runs it, and prints JSON from `/api/stack-status`. If Docker is unavailable, start services manually and curl `/api/health` and `/api/stack-status`.

## Builds & tests

```bash
dotnet build backend/JobAssistant.sln
dotnet test backend/JobAssistant.sln

cd frontend && npm run lint && npm run build

# Python (from each service directory, with venv activated)
cd services/ai-agent && pip install pytest && python -m pytest tests/
cd services/rag-api && pip install pytest && python -m pytest tests/
```

---

## Future plan

The product goal is a multi-tenant SaaS that helps people **track applications**, **understand job descriptions**, and **act through an agent** (research, draft, update pipeline) with durable data and real models. Work is ordered so each phase ships usable value on top of the current scaffold.

### Phase 1 — Make the demo trustworthy (foundation) ✅ in progress / largely done

Finish turning stubs into reliable local product loops.

1. **Postgres always on in the happy path** — ✅ `scripts/dev-up.sh`; UI fails soft when DB is down; 503 JSON for agent tools  
2. **Schema migrations** — ✅ versioned SQL under `Data/Migrations` + `schema_migrations`  
3. **Applications UX** — ✅ status filter, search, sort, empty states, optimistic inline status/delete  
4. **Agent run history** — ✅ Postgres + `GET /api/agent/runs` + UI on `/agent`  
5. **Automated tests** — ✅ .NET xUnit + agent policy tests + RAG ingest/query round-trip  

**Exit criteria:** With Compose + API + UI, a user can CRUD applications, ingest a JD, retrieve it, and run an agent goal that lists apps and retrieves RAG hits—all with tests in CI.

### Phase 2 — Real intelligence (models)

Keep the same JSON tool-call contract; swap fakes for providers behind env config.

1. **Real embeddings in `rag-api`**  
   Pluggable embedding backend (OpenAI / local sentence-transformers / Bedrock). Store dimension + model name in metadata. Migration path from FakeEmbeddings (re-index).

2. **JD ingest pipeline**  
   UI + API: paste or upload job description → chunk → embed → FAISS (and optionally link `application_id` in metadata). Delete/reindex endpoints.

3. **Real LLM for the agent**  
   `call_llm` → OpenAI / Anthropic / Azure / Bedrock with structured JSON (or tool-calling). Keep the fake LLM as `AGENT_LLM=fake` for offline CI.

4. **Better agent tools**  
   - `update_application` / `delete_application`  
   - `web_search` → real search API  
   - Optional: draft cover letter / outreach email from application + retrieved JD chunks  

5. **Secrets & config**  
   Document required keys in `.env.example`; never commit secrets. Support per-service env in Compose.

**Exit criteria:** Agent goals produce useful answers with a real model; RAG similarity is meaningful for real JDs; offline fake mode still works for CI.

### Phase 3 — Product depth (job hunt workflows)

1. **Auth & multi-tenant data**  
   Users (email/password or OAuth), `user_id` on applications and agent runs, API auth (JWT or session), row-level ownership checks.

2. **Application state machine**  
   Formal transitions (`saved` → `applied` → `interview` → `offer` / `rejected`), optional history table, follow-up / reminder dates, calendar hooks later.

3. **Email / inbox ingestion (stretch)**  
   Ingest recruiter emails or “application received” messages; suggest status updates; attach to an application.

4. **Agent ↔ application linking**  
   Every agent run can target an `application_id`; store citations from RAG chunks used in the answer.

5. **Frontend product shell**  
   Auth screens, protected routes, clearer navigation, application detail page (notes, JD, agent history).

**Exit criteria:** Two users cannot see each other’s data; a single application has timeline + related agent runs + linked JD chunks.

### Phase 4 — Platform & deploy

1. **Containerize the .NET API** and optional all-in-one Compose profile for full stack.

2. **Cloud target (AWS-oriented)**  
   - RDS Postgres  
   - ECS/EKS or App Runner for API + Python services (or Lambda + API Gateway for the API edge)  
   - Persistent volume or managed vector store for FAISS (or migrate to pgvector / OpenSearch)  
   - Secrets Manager / SSM for keys  

3. **Observability**  
   Structured logs, request IDs across API → agent → RAG, basic metrics/health for each service.

4. **CI/CD**  
   Build + test on PR; publish images; optional staging environment.

5. **Vector store evolution**  
   Evaluate **pgvector** (one less service) vs kept FAISS microservice vs managed search—choose based on scale and ops cost.

**Exit criteria:** Deployable staging environment with HTTPS, managed DB, and documented runbooks.

### Phase 5 — Differentiation (later)

- Resume upload + match score against JD chunks  
- Interview prep Q&A grounded in the JD + resume  
- Multi-agent workflows (researcher vs writer) with shared scratchpad  
- Browser extension “save this posting” → create application + ingest  
- Billing / plans if SaaS-ready  

---

## Suggested build order (short)

1. ~~Migrations + application filters + agent run history~~ (Phase 1)  
2. Real embeddings + JD ingest UI  
3. Real LLM behind the existing tool contract  
4. Auth + ownership  
5. Deploy staging on AWS  

---

## Non-goals (for now)

- Perfect retrieval quality before a real embedding model exists  
- Multi-region / high-availability design before a first staging deploy  
- Replacing the monorepo with separate repos until service boundaries stabilize  
