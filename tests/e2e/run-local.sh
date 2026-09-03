#!/usr/bin/env bash
#
# Runs the browser smoke test against a locally published artifact.
#
# This mirrors the `build` job in .github/workflows/pages.yml. It is a convenience, NOT a
# second source of truth: if the two ever disagree, the workflow is right, because the
# workflow is what gates the merge. Keep the ORDER in particular — the <base href> rewrite
# has to happen before 404.html is copied, or the fallback carries the wrong base and the
# deep-link check fails for a reason that has nothing to do with the deep links.
#
# Usage:  tests/e2e/run-local.sh [port]
#
# NETWORK: checks 2, 3 and 4 load Chart.js from cdn.jsdelivr.net, because they are what
# exercises the SRI hashes. Behind a proxy that cannot reach it they fail with
# ERR_TUNNEL_CONNECTION_FAILED and `window.Chart is undefined`, and you get 4/7. That is the
# network, not the application. Checks 1, 5, 6 and 7 need nothing external.
set -euo pipefail

port="${1:-8080}"
base="/SupercompensationApp/"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
out="$(mktemp -d)"
cd "$root"

cleanup() {
  [[ -n "${server:-}" ]] && kill "$server" 2>/dev/null || true
  rm -rf "$out"
}
trap cleanup EXIT

echo "==> publishing"
dotnet publish SupercompensationApp.csproj -c Release -o "$out/publish" >/dev/null

index="$out/publish/wwwroot/index.html"

echo "==> rewriting <base href> to $base"
# Grepped first for the same reason pages.yml greps: if the committed tag is ever
# reformatted, the sed silently matches nothing and every asset resolves against the wrong
# root, which shows up as a blank page and no failed step.
grep -q '<base href="/" />' "$index" \
  || { echo "could not find the expected <base href=\"/\" /> in $index" >&2; exit 1; }
sed -i "s|<base href=\"/\" />|<base href=\"$base\" />|" "$index"

echo "==> adding the SPA fallback and .nojekyll"
cp "$index" "$out/publish/wwwroot/404.html"
touch "$out/publish/wwwroot/.nojekyll"

echo "==> serving on $port"
node tests/e2e/serve.mjs "$out/publish/wwwroot" "$base" "$port" >"$out/serve.log" 2>&1 &
server=$!

for _ in $(seq 1 30); do
  curl -fsS -o /dev/null "http://localhost:$port$base" && break
  sleep 1
done
curl -fsS -o /dev/null "http://localhost:$port$base" \
  || { echo "the static server never came up:" >&2; cat "$out/serve.log" >&2; exit 1; }

if [[ ! -d tests/e2e/node_modules ]]; then
  echo "==> installing Playwright"
  (cd tests/e2e && npm install --no-audit --no-fund >/dev/null)
  (cd tests/e2e && npx playwright install --with-deps chromium >/dev/null)
fi

echo "==> driving it"
node tests/e2e/smoke.mjs "http://localhost:$port$base"
