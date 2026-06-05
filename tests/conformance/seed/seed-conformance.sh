#!/usr/bin/env bash
# Seed the conformance OP with the admin user + (TODO Task 2) the conformance client,
# scopes, and login-able test user. Idempotent where possible.
#
# Run AFTER `docker compose up` is healthy (from tests/conformance/).
set -euo pipefail
cd "$(dirname "$0")/.."

ADMIN_EMAIL="${ADMIN_EMAIL:-admin@conformance.local}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-Conformance!1}"

echo "==> Waiting for rufistoward-op to be healthy..."
# /health returns JSON with overall status; loop until it responds 200.
until docker compose exec -T rufistoward-op \
      sh -c 'wget -q -O- --no-check-certificate https://localhost:5001/health >/dev/null 2>&1'; do
  sleep 2
done

echo "==> Seeding admin user ($ADMIN_EMAIL) via the seed-admin CLI command..."
# Runs a one-off process in the OP container; shares the Postgres DB. Idempotent:
# the command exits non-zero if the admin already exists — tolerate that.
docker compose exec -T rufistoward-op \
  dotnet RufistoWard.Host.dll seed-admin --email "$ADMIN_EMAIL" --password "$ADMIN_PASSWORD" \
  || echo "   (admin may already exist — continuing)"

echo
echo "==> TODO (Task 2): seed the conformance client + scopes + test user."
echo "    Two grounded options:"
echo "    A) RECOMMENDED — add a 'seed-conformance' CLI command (mirror SeedAdminCommand)"
echo "       so it creates a client with a KNOWN client_id/secret deterministically:"
echo "         docker compose exec -T rufistoward-op dotnet RufistoWard.Host.dll seed-conformance"
echo "       Needed because POST /api/admin/clients (CreateClientRequest: ClientName,"
echo "       GrantTypes[], RedirectUris[], AllowedScopes[], RequireClientSecret) does NOT"
echo "       accept a client_id — the server generates it, so the API path can't pin a"
echo "       stable client_id/secret for the static-client test config."
echo "    B) Admin API — cookie-login as admin (POST /login: UsernameOrEmail, Password),"
echo "       then POST /api/admin/clients and capture the generated ClientId/secret from"
echo "       the response into ./config/test-plans/*.json. Scopes via /api/admin/scopes."
echo
echo "    Conformance client must have: code grant + PKCE, scopes 'openid profile email',"
echo "    redirect URIs = the suite callbacks (see ./config/test-plans/rufistoward-basic.json)."
