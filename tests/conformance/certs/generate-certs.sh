#!/usr/bin/env bash
# Generate a self-signed TLS cert for the RufistoWard OP container.
#
#   SAN = DNS:rufistoward-op  (the container DNS name the suite reaches)
#
# Produces:
#   op.pfx       -> mounted into rufistoward-op for Kestrel HTTPS (password: conformance)
#   op-cert.pem  -> the public cert; the conformance suite must TRUST this (truststore /
#                   browser CA) or discovery over https fails silently. See README › TLS.
#
# Re-run is idempotent-ish: it overwrites. Outputs are gitignored.
set -euo pipefail
cd "$(dirname "$0")"

HOST="rufistoward-op"
PFX_PASSWORD="${OP_PFX_PASSWORD:-conformance}"

openssl req -x509 -newkey rsa:2048 -nodes \
  -keyout op-key.pem -out op-cert.pem \
  -days 825 -sha256 \
  -subj "/CN=${HOST}" \
  -addext "subjectAltName=DNS:${HOST},DNS:localhost,IP:127.0.0.1"

openssl pkcs12 -export \
  -out op.pfx \
  -inkey op-key.pem -in op-cert.pem \
  -passout "pass:${PFX_PASSWORD}"

echo "Generated op.pfx (password: ${PFX_PASSWORD}) and op-cert.pem for SAN DNS:${HOST}"
echo "NEXT: make the conformance suite trust op-cert.pem — see tests/conformance/README.md (TLS)."
