---
name: cloud-agent-starter
description: Practical setup, run, and test workflows for Cursor Cloud agents working in this repository.
---

# Cloud Agent Starter — Job Assistant monorepo

Use this skill when you need to run, build, or smoke-test this repository from Cursor Cloud.

## Repository orientation

- **Backend:** `backend/JobAssistant.Api` — ASP.NET Core 8 Web API (`/api/health`, `/api/stack-status`, `/api/agent/run`).
- **Frontend:** `frontend` — React + Vite + TypeScript; dev proxy forwards `/api` to `http://127.0.0.1:5287`.
- **RAG service:** `services/rag-api` — FastAPI + FAISS; text ingest/query at `/rag/ingest` and `/rag/query`; store under `services/rag-api/var/faiss/` (or `FAISS_STORE_DIR`).
- **AI agent:** `services/ai-agent` — FastAPI ReAct loop on port **8002** with tools for applications + RAG; callable via `POST /agent/run` or .NET `POST /api/agent/run`. Env: `JOB_ASSISTANT_API_BASE_URL`, `RAG_API_BASE_URL`.
- **Infra:** `docker-compose.yml` — Postgres + `rag-api` + `ai-agent` image builds.

Credentials belong in environment variables or `.env` (not committed). Example names are in `.env.example`.

## Cloud setup

1. Start from the repo root: `cd /workspace`
2. **.NET 8 SDK** is required for `dotnet build` / `dotnet run`.
3. **Node.js** for the frontend: `cd frontend && npm install`.
4. **Docker** may be unavailable in some Cloud sandboxes (no `docker.sock`). If `docker compose` fails, rely on local builds and optional manual Postgres/RAG on the host.

## Backend

```bash
dotnet build backend/JobAssistant.sln
dotnet test backend/JobAssistant.sln
dotnet run --project backend/JobAssistant.Api/JobAssistant.Api.csproj --urls http://127.0.0.1:5287
```

Smoke (API must be running):

```bash
curl -fsS http://127.0.0.1:5287/api/health
curl -fsS http://127.0.0.1:5287/api/stack-status
curl -fsS "http://127.0.0.1:5287/api/applications?status=saved&search=acme"
curl -fsS "http://127.0.0.1:5287/api/agent/runs?limit=5"
```

`stack-status` reports whether Postgres, the RAG `/health` endpoint, and the AI agent `/health` endpoint are reachable. On startup the API applies versioned SQL under `Data/Migrations`.

One-command local stack (Docker required): `./scripts/dev-up.sh`

## Frontend

```bash
cd frontend && npm install && npm run build
```

Dev server (requires API on 5287 for full stack UI):

```bash
cd frontend && npm run dev
```

## RAG service (Python)

```bash
cd services/rag-api
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
.venv/bin/uvicorn app.main:app --host 127.0.0.1 --port 8001
```

Check:

```bash
curl -fsS http://127.0.0.1:8001/health
```

## AI agent (Python)

```bash
cd services/ai-agent
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
.venv/bin/uvicorn app.main:app --host 127.0.0.1 --port 8002
```

Check:

```bash
curl -fsS http://127.0.0.1:8002/health
curl -fsS -X POST http://127.0.0.1:8002/agent/run \
  -H 'Content-Type: application/json' \
  -d '{"goal":"Research the ReAct loop"}'
```

## Docker Compose (when Docker daemon works)

```bash
docker compose up -d --build
./scripts/verify-dev-connectivity.sh
```

## Updating this skill

Keep commands aligned with `README.md` when ports, paths, or service names change.
