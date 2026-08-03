#!/usr/bin/env bash
# One-command local bring-up: Compose infra + API + Vite (Phase 1).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "==> Docker Compose (Postgres + RAG + AI agent)"
docker compose up -d --build

echo "==> Waiting for Postgres"
for i in $(seq 1 45); do
  if docker compose exec -T postgres pg_isready -U jobassistant -d jobassistant >/dev/null 2>&1; then
    break
  fi
  sleep 1
  if [[ "$i" -eq 45 ]]; then
    echo "Postgres did not become ready in time." >&2
    exit 1
  fi
done

echo "==> Waiting for RAG /health"
for i in $(seq 1 45); do
  if curl -fsS "http://127.0.0.1:${RAG_PORT:-8001}/health" >/dev/null 2>&1; then
    break
  fi
  sleep 1
  if [[ "$i" -eq 45 ]]; then
    echo "RAG API did not become healthy in time." >&2
    exit 1
  fi
done

echo "==> Waiting for AI agent /health"
for i in $(seq 1 45); do
  if curl -fsS "http://127.0.0.1:${AI_AGENT_PORT:-8002}/health" >/dev/null 2>&1; then
    break
  fi
  sleep 1
  if [[ "$i" -eq 45 ]]; then
    echo "AI agent did not become healthy in time." >&2
    exit 1
  fi
done

export PATH="${HOME}/.dotnet:/usr/share/dotnet:/usr/local/bin:${PATH}"
export JOB_ASSISTANT_API_BASE_URL="${JOB_ASSISTANT_API_BASE_URL:-http://127.0.0.1:5287}"
export RAG_API_BASE_URL="${RAG_API_BASE_URL:-http://127.0.0.1:8001}"

echo "==> Starting .NET API on :5287"
dotnet run --project "$ROOT/backend/JobAssistant.Api/JobAssistant.Api.csproj" --urls "http://127.0.0.1:5287" &
API_PID=$!

cleanup() {
  kill "$API_PID" "$FE_PID" 2>/dev/null || true
}
trap cleanup EXIT

for i in $(seq 1 45); do
  if curl -fsS "http://127.0.0.1:5287/api/health" >/dev/null 2>&1; then
    break
  fi
  sleep 1
  if [[ "$i" -eq 45 ]]; then
    echo ".NET API did not respond in time." >&2
    exit 1
  fi
done

echo "==> Stack status"
curl -fsS "http://127.0.0.1:5287/api/stack-status" | python3 -m json.tool

echo "==> Starting Vite frontend"
(
  cd "$ROOT/frontend"
  if [[ ! -d node_modules ]]; then
    npm install
  fi
  npm run dev -- --host localhost --port 5173
) &
FE_PID=$!

echo ""
echo "Dev stack is up:"
echo "  UI:     http://localhost:5173"
echo "  API:    http://127.0.0.1:5287"
echo "  RAG:    http://127.0.0.1:8001"
echo "  Agent:  http://127.0.0.1:8002"
echo "Press Ctrl+C to stop the API and Vite (Compose keeps running)."
wait
