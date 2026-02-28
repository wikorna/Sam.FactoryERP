#!/bin/bash
set -euo pipefail

echo "[entrypoint] starting..."

# Install CA certificates from /https if present
if [ -d "/https" ] && ls /https/*.crt >/dev/null 2>&1; then
  echo "[entrypoint] installing CA certificates from /https..."
  cp /https/*.crt /usr/local/share/ca-certificates/ 2>/dev/null || true
  update-ca-certificates 2>/dev/null || echo "[entrypoint] warning: update-ca-certificates failed"
else
  echo "[entrypoint] no extra CA certs found in /https"
fi

exec "$@"
