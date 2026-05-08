#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "==> Starting Postgres + RAG API (Docker Compose)"
docker compose up -d --build

echo "==> Waiting for Postgres"
for i in $(seq 1 30); do
  if docker compose exec -T postgres pg_isready -U jobassistant -d jobassistant >/dev/null 2>&1; then
    break
  fi
  sleep 1
  if [[ "$i" -eq 30 ]]; then
    echo "Postgres did not become ready in time." >&2
    exit 1
  fi
done

echo "==> Waiting for RAG /health"
for i in $(seq 1 30); do
  if curl -fsS "http://127.0.0.1:8001/health" >/dev/null 2>&1; then
    break
  fi
  sleep 1
  if [[ "$i" -eq 30 ]]; then
    echo "RAG API did not become healthy in time." >&2
    exit 1
  fi
done

echo "==> Building .NET API"
dotnet build "$ROOT/backend/JobAssistant.Api/JobAssistant.Api.csproj" -v minimal

echo "==> Starting .NET API briefly for probe"
dotnet run --project "$ROOT/backend/JobAssistant.Api/JobAssistant.Api.csproj" --urls "http://127.0.0.1:5287" &
DOTNET_PID=$!
cleanup() {
  kill "$DOTNET_PID" 2>/dev/null || true
}
trap cleanup EXIT

for i in $(seq 1 30); do
  if curl -fsS "http://127.0.0.1:5287/api/stack-status" >/dev/null 2>&1; then
    break
  fi
  sleep 1
  if [[ "$i" -eq 30 ]]; then
    echo ".NET API did not respond in time." >&2
    exit 1
  fi
done

echo "==> Stack status from API"
curl -fsS "http://127.0.0.1:5287/api/stack-status" | python3 -m json.tool

echo ""
echo "All probes passed. Run 'docker compose logs -f' for service logs."
