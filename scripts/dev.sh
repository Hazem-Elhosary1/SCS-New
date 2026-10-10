#!/usr/bin/env bash
# Starts the whole system on this machine: database, platform API and the three front ends.
# See docs/setup.md. Ctrl+C stops the API and the front ends; the database container keeps
# running (stop it with: docker compose -f deploy/compose/docker-compose.yml down).
#
#   scripts/dev.sh           everything
#   scripts/dev.sh --no-web  database and API only
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose="$root/deploy/compose/docker-compose.yml"
env_file="$root/deploy/compose/.env"
no_web=false
[[ "${1:-}" == "--no-web" ]] && no_web=true

step() { printf '\n==> %s\n' "$1"; }

step "Configuration"
if [[ ! -f "$env_file" ]]; then
  cp "$root/deploy/compose/.env.example" "$env_file"
  echo "Created deploy/compose/.env from .env.example (sample values)."
fi
while IFS= read -r line; do
  [[ "$line" =~ ^[[:space:]]*# || -z "${line// }" ]] && continue
  export "${line%%=*}=${line#*=}"
done < "$env_file"

step "Database"
docker compose -f "$compose" --env-file "$env_file" up -d --wait sqlserver
docker compose -f "$compose" --env-file "$env_file" --profile init run --rm sqlserver-init

step "Structure"
dotnet run --project "$root/src/server/tools/Scs.Migrator" -c Release

pids=()
cleanup() { for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done; }
trap cleanup EXIT INT TERM

step "Platform API"
ASPNETCORE_URLS="http://localhost:5080" \
  dotnet run --project "$root/src/server/host/Scs.Api" -c Release --no-launch-profile &
pids+=($!)

if [[ "$no_web" == false ]]; then
  step "Front ends"
  cd "$root/src/web"
  [[ -d node_modules ]] || npm ci
  for app in clinic patient ops; do
    npm run "start:$app" &
    pids+=($!)
  done
fi

cat <<'EOF'

Smart Clinic OS is starting. When the builds finish:

  Platform   http://localhost:5080/health/ready
  Version    http://localhost:5080/v1/meta/version
  Clinic     http://localhost:4201
  Patient    http://localhost:4202
  Operators  http://localhost:4203
  Jobs       http://localhost:5080/jobs
  API docs   http://localhost:5080/openapi/v1.json

Press Ctrl+C to stop.
EOF

wait
