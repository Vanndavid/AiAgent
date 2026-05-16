# AGENTS.md

## Cursor Cloud specific instructions

### Overview

Job Assistant is a monorepo with three services plus Docker-managed infrastructure. See `README.md` for full documentation. Key ports:

| Service | Port | Run command |
|---|---|---|
| ASP.NET Core API | 5287 | `dotnet run --project backend/JobAssistant.Api/JobAssistant.Api.csproj --urls http://127.0.0.1:5287` |
| React (Vite) | 5173 | `cd frontend && npm run dev` |
| RAG API (FastAPI) | 8001 | `cd services/rag-api && .venv/bin/uvicorn app.main:app --host 127.0.0.1 --port 8001` |
| PostgreSQL | 5432 | `docker compose up -d postgres` (requires Docker daemon running) |

### Starting Docker

Docker must be started manually in Cloud VMs before running `docker compose`:

```bash
sudo dockerd &>/tmp/dockerd.log &
sleep 3
```

The fuse-overlayfs storage driver and iptables-legacy are required for nested Docker. These are installed by the update script's system-dependency step.

### Starting Postgres

```bash
docker compose up -d postgres
```

Wait for readiness: `docker compose exec -T postgres pg_isready -U jobassistant -d jobassistant`

### RAG service (optional)

The .NET API handles RAG being unavailable gracefully (`ragApi: false` in `/api/stack-status`). Start it only if working on RAG features:

```bash
cd services/rag-api && .venv/bin/uvicorn app.main:app --host 127.0.0.1 --port 8001
```

### Lint / build / test

- **Frontend lint:** `cd frontend && npm run lint`
- **Frontend build (includes tsc):** `cd frontend && npm run build`
- **Backend build:** `dotnet build backend/JobAssistant.sln`
- **Smoke test (API must be running):** `curl -fsS http://127.0.0.1:5287/api/health` and `curl -fsS http://127.0.0.1:5287/api/stack-status`

### Gotchas

- The Vite dev server binds to `localhost` not `127.0.0.1` — use `http://localhost:5173` (not `http://127.0.0.1:5173`) for browser access and curl.
- The frontend proxies `/api` requests to `http://127.0.0.1:5287` — the .NET API must be running for the UI dashboard to show connected services.
- The .NET SDK is installed to `/usr/share/dotnet` with a symlink at `/usr/local/bin/dotnet`.
