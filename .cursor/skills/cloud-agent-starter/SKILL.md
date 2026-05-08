---
name: cloud-agent-starter
description: Practical setup, run, and test workflows for Cursor Cloud agents working in this repository.
---

# Cloud Agent Starter — Job Assistant monorepo

Use this skill when you need to run, build, or smoke-test this repository from Cursor Cloud.

## Repository orientation

- **Backend:** `backend/JobAssistant.Api` — ASP.NET Core 8 Web API (`/api/health`, `/api/stack-status`).
- **Frontend:** `frontend` — React + Vite + TypeScript; dev proxy forwards `/api` to `http://127.0.0.1:5287`.
- **RAG service:** `services/rag-api` — FastAPI + FAISS; default index path under `services/rag-api/var/faiss/` when not using Docker.
- **Infra:** `docker-compose.yml` — Postgres + `rag-api` image build.

Credentials belong in environment variables or `.env` (not committed). Example names are in `.env.example`.

## Cloud setup

1. Start from the repo root: `cd /workspace`
2. **.NET 8 SDK** is required for `dotnet build` / `dotnet run`.
3. **Node.js** for the frontend: `cd frontend && npm install`.
4. **Docker** may be unavailable in some Cloud sandboxes (no `docker.sock`). If `docker compose` fails, rely on local builds and optional manual Postgres/RAG on the host.

## Backend

```bash
dotnet build backend/JobAssistant.sln
dotnet run --project backend/JobAssistant.Api/JobAssistant.Api.csproj --urls http://127.0.0.1:5287
```

Smoke (API must be running):

```bash
curl -fsS http://127.0.0.1:5287/api/health
curl -fsS http://127.0.0.1:5287/api/stack-status
```

`stack-status` reports whether Postgres and the RAG `/health` endpoint are reachable.

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

## Docker Compose (when Docker daemon works)

```bash
docker compose up -d --build
./scripts/verify-dev-connectivity.sh
```

## Updating this skill

Keep commands aligned with `README.md` when ports, paths, or service names change.
