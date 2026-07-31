# Job Assistant (SaaS foundation)

Monorepo scaffold for a job-application assistant: **ASP.NET Core** API, **React** (Vite), **PostgreSQL**, a **FastAPI** RAG microservice with **FAISS**, and a callable **ReAct AI agent** service. AWS (for example API Gateway) can sit in front later; this repo wires local/dev connectivity first.

## Layout

| Path | Role |
|------|------|
| `backend/JobAssistant.Api` | .NET 8 minimal API, CORS for Vite, `/api/stack-status` probes Postgres + RAG + AI agent |
| `frontend` | React + TypeScript UI that calls the API via Vite proxy |
| `services/rag-api` | FastAPI + FAISS; text ingest/query + vector search; persists under `var/faiss` or `/data/faiss` |
| `services/ai-agent` | FastAPI ReAct agent with tools for applications, RAG, and research/save; `POST /agent/run` |
| `docker-compose.yml` | Postgres + RAG + AI agent container definitions |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+ (for Vite)
- Python 3.12+ (optional if you only run RAG in Docker)
- Docker Desktop or Docker Engine + Compose (for Postgres and containerized RAG)

## Configuration

Copy `.env.example` to `.env` in the repo root if you want to override defaults. The API reads:

- `ConnectionStrings__PostgreSQL` — Npgsql connection string (defaults match `docker-compose.yml`)
- `Services__RagApiBaseUrl` — base URL for the Python RAG service (default `http://localhost:8001`)
- `Services__AiAgentBaseUrl` — base URL for the Python AI agent (default `http://localhost:8002`)

The Vite dev server proxies `/api` to `http://127.0.0.1:5287` (see `frontend/vite.config.ts`). The API listens on **5287** in `Properties/launchSettings.json`.

## Run everything locally

1. **Infrastructure**

   ```bash
   docker compose up -d --build
   ```

   This starts Postgres on `localhost:5432`, the RAG API on `localhost:8001`, and the AI agent on `localhost:8002`.

   > **Port conflict?** If you already have PostgreSQL running locally on 5432, copy `.env.example` to `.env` and change `POSTGRES_PORT` (and the matching port in `ConnectionStrings__PostgreSQL`). Same for `RAG_PORT` / `Services__RagApiBaseUrl` if 8001 is taken, or `AI_AGENT_PORT` / `Services__AiAgentBaseUrl` if 8002 is taken.

2. **.NET API** (from repo root)

   ```bash
   dotnet run --project backend/JobAssistant.Api/JobAssistant.Api.csproj
   ```

3. **React**

   ```bash
   cd frontend && npm install && npm run dev
   ```

   Open the printed URL (typically `http://localhost:5173`). The home page calls `/api/stack-status` and shows whether Postgres and the RAG service respond.

### RAG service without Docker

From `services/rag-api`:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --host 127.0.0.1 --port 8001
```

The FAISS index defaults to `services/rag-api/var/faiss/` (LangChain `jobs.faiss` + `jobs.pkl`). Seed job chunks are created on first start.

Ingest / query without Docker:

```bash
curl -fsS -X POST http://127.0.0.1:8001/rag/ingest \
  -H 'Content-Type: application/json' \
  -d '{"texts":["Example Co seeks a Backend Engineer with ASP.NET and Postgres."],"metadatas":[{"company":"Example Co","role":"Backend Engineer"}]}'

curl -fsS -X POST http://127.0.0.1:8001/rag/query \
  -H 'Content-Type: application/json' \
  -d '{"query":"backend engineer postgresql","k":3}'
```

### AI agent without Docker

From `services/ai-agent`:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --host 127.0.0.1 --port 8002
```

Point the agent at the API + RAG (defaults match local ports):

```bash
export JOB_ASSISTANT_API_BASE_URL=http://127.0.0.1:5287
export RAG_API_BASE_URL=http://127.0.0.1:8001
```

Call it directly:

```bash
curl -fsS -X POST http://127.0.0.1:8002/agent/run \
  -H 'Content-Type: application/json' \
  -d '{"goal":"List my job applications and retrieve similar backend engineer roles from RAG."}'
```

Or through the .NET API (once running): `POST /api/agent/run` with the same JSON body. The UI page is at `/agent`.

Agent tools (deterministic fake LLM picks based on goal keywords):

| Tool | Calls |
|------|--------|
| `list_applications` / `get_application` / `create_application` | .NET `/api/applications` |
| `rag_retrieve` / `rag_ingest` | RAG `/rag/query` and `/rag/ingest` |
| `web_search` / `save_file` | Stub search + local notes |

## Verify connectivity

With Docker running Postgres + RAG:

```bash
chmod +x scripts/verify-dev-connectivity.sh
./scripts/verify-dev-connectivity.sh
```

The script builds the API, briefly runs it, and prints JSON from `/api/stack-status`. If the Docker daemon is unavailable (some CI sandboxes), run the three manual steps above on your machine instead.

## Builds

```bash
dotnet build backend/JobAssistant.sln
cd frontend && npm run build
```

## Next steps (product)

- Auth / multi-tenant data model in Postgres
- Email ingestion + application state machine in the API
- Real embedding model (replace FakeEmbeddings) and optional sync metadata in SQL
- Real LLM provider for the agent (keep the JSON tool-call contract)
- Deploy: RDS Postgres, ECS/EKS or Lambda + API Gateway, separate image for `rag-api`
