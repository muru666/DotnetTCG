#!/usr/bin/env bash
# Levanta todo el proyecto en modo desarrollo desde la ruta base:
#   Postgres de dev (Docker) -> seed xy1 (migra + upsert idempotente) ->
#   backend (:5280) + frontend (:4200).
# Uso: ./run-dev.sh
# Parar: Ctrl+C (mata backend y frontend; Postgres queda corriendo con sus datos).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

PG_CONTAINER="dotnettcp-dev-pg"
API_PID=""

cleanup() {
  if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then
    echo "Deteniendo backend (pid $API_PID)..."
    kill "$API_PID" 2>/dev/null || true
  fi
}
trap cleanup INT TERM EXIT

need() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Falta '$1' en el PATH. Instalalo y reintentá." >&2
    exit 1
  fi
}
need docker
need dotnet

# Git flow: el dev siempre arranca desde develop (con el trabajo más actual
# disponible en el remoto). No fatal: se puede trabajar offline o en un zip.
if git rev-parse --git-dir >/dev/null 2>&1; then
  echo "==> Rama develop"
  if [ "$(git branch --show-current)" != "develop" ]; then
    git checkout develop
  fi
  if ! git pull --ff-only --quiet; then
    echo "    (aviso) no se pudo actualizar develop desde el remoto; se usa la versión local" >&2
  fi
  echo "    develop @ $(git rev-parse --short HEAD)"
fi

echo "==> Postgres de desarrollo"
if ! docker ps --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
  if docker ps -a --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
    docker start "$PG_CONTAINER" >/dev/null
  else
    docker run -d --name "$PG_CONTAINER" \
      -e POSTGRES_PASSWORD=postgres \
      -e POSTGRES_DB=dotnettcp_dev \
      -p 5432:5432 postgres:16 >/dev/null
  fi
fi
until docker exec "$PG_CONTAINER" pg_isready -U postgres >/dev/null 2>&1; do
  sleep 1
done
echo "    Postgres listo en localhost:5432"

echo "==> Seed xy1 (migra + upsert, sin llamar a la API externa)"
SEED_FILE="$ROOT/data/xy1.json"
dotnet run --project src/DotnetTCG.Api --no-build -- seed-cards "$SEED_FILE" 2>/dev/null \
  || dotnet run --project src/DotnetTCG.Api -- seed-cards "$SEED_FILE"

echo "==> Backend en http://localhost:5280"
dotnet run --project src/DotnetTCG.Api --no-build > /tmp/dotnettcg-api.log 2>&1 &
API_PID=$!
for _ in $(seq 1 30); do
  if curl -s -o /dev/null http://localhost:5280/health; then
    break
  fi
  sleep 1
done
curl -s -o /dev/null http://localhost:5280/health
echo "    Backend arriba (log: /tmp/dotnettcg-api.log)"

echo "==> Frontend en http://localhost:4200 (Ctrl+C para parar todo)"
cd src/DotnetTCG.Frontend
if [ ! -d node_modules ]; then
  echo "    Instalando dependencias (primera vez)..."
  npm install
fi
trap 'cleanup; exit 0' INT TERM
npm start
