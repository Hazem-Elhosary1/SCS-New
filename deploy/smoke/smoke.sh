#!/usr/bin/env bash
# Smoke test run after every deployment (quickstart.md Scenario 2b).
#
#   deploy/smoke/smoke.sh <expected-commit> <front-end-url>...
#
# Through each front-end address it checks that the platform is ready, that the platform
# reports the expected commit, and that the front end itself reports the same commit.
# Exits non-zero on the first difference.
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: smoke.sh <expected-commit> <front-end-url>..." >&2
  exit 2
fi

expected="$1"
shift

if [[ ! "$expected" =~ ^[0-9a-f]{40}$ ]]; then
  echo "expected-commit must be a 40-character commit id" >&2
  exit 2
fi

# Reads one string field from a small JSON document without needing jq on the runner.
field() { sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" | head -n 1; }

fetch() { curl --silent --show-error --fail --max-time 15 --retry 5 --retry-delay 3 --retry-all-errors "$1"; }

failed=0
for site in "$@"; do
  site="${site%/}"
  echo "== $site"

  if ! ready="$(fetch "$site/api/health/ready")"; then
    echo "   FAIL  platform is not ready through this address"; failed=1; continue
  fi
  echo "   ok    platform ready"

  platform_commit="$(fetch "$site/api/v1/meta/version" | field commit || true)"
  if [[ "$platform_commit" != "$expected" ]]; then
    echo "   FAIL  platform reports commit '${platform_commit:-none}', expected $expected"; failed=1
  else
    echo "   ok    platform commit"
  fi

  site_commit="$(fetch "$site/version.json" | field commit || true)"
  if [[ "$site_commit" != "$expected" ]]; then
    echo "   FAIL  front end reports commit '${site_commit:-none}', expected $expected"; failed=1
  else
    echo "   ok    front-end commit"
  fi
done

exit "$failed"
