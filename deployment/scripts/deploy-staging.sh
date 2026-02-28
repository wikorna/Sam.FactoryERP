#!/usr/bin/env bash
set -Eeuo pipefail

# ==============================
# ANE API - Staging Deployment
# Production-grade (Rocky Linux)
# ==============================

# ----- Pretty colors -----
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log()  { echo -e "${BLUE}$*${NC}"; }
ok()   { echo -e "${GREEN}$*${NC}"; }
warn() { echo -e "${YELLOW}$*${NC}"; }
err()  { echo -e "${RED}$*${NC}"; }

die() { err "ERROR: $*"; exit 1; }

on_error() {
  local exit_code=$?
  err "FAILED at line $1 (exit=$exit_code)"
  err "Hint: check logs -> docker logs ${CONTAINER_NAME}  (or ${CONTAINER_NAME_NEXT} if created)"
  exit "$exit_code"
}
trap 'on_error $LINENO' ERR

# ----- Configuration -----
CONTAINER_NAME="ane-api-staging"
CONTAINER_NAME_NEXT="ane-api-staging-next"
IMAGE_NAME="ane-api:staging"

HTTP_PORT=5000
HTTPS_PORT=5001
HTTPS_EXTERNAL_PORT=8443

APP_ROOT="/home/VdrEximAdmin/apps/ane-api"
CERT_DIR="/home/VdrEximAdmin/ca-certificates"
CERT_FILE="star_exim_go_th.crt"
KEY_FILE="wildcard_cert.key"

# Database connection - Staging (NOTE: env vars visible via docker inspect; consider secrets later)
DB_HOST="192.168.14.94"
DB_PORT="5432"
DB_NAME="aneuat"
DB_USER="dev"
DB_PASS="dev@2025"

# CORS Origins - Staging
CORS_ORIGIN_0="https://aneuat.exim.go.th"
CORS_ORIGIN_1="https://aneuat.exim.go.th:5001"
CORS_ORIGIN_2="https://aneuat.exim.go.th:5003"
CORS_ORIGIN_3="http://localhost:5000"
CORS_ORIGIN_4="http://localhost:4200"

ASPNETCORE_ENVIRONMENT="Staging"
ASPNETCORE_URLS="http://+:5000;https://+:5001"
HEALTH_URL="http://127.0.0.1:${HTTP_PORT}/health"
HEALTH_TIMEOUT_SEC=60
HEALTH_INTERVAL_SEC=2

# ----- Header -----
echo "========================================"
echo "ANE API - Staging Deployment Script"
echo "========================================"
echo ""

# ----- Pre-checks -----
log "[0] Pre-checks..."
command -v docker >/dev/null 2>&1 || die "docker not found"
command -v curl  >/dev/null 2>&1 || die "curl not found (install: sudo dnf install -y curl)"
docker info >/dev/null 2>&1 || die "docker daemon not reachable (permission?)"

# Certificates
[ -f "${CERT_DIR}/${CERT_FILE}" ] || die "Certificate not found: ${CERT_DIR}/${CERT_FILE}"
[ -f "${CERT_DIR}/${KEY_FILE}" ]  || die "Key not found: ${CERT_DIR}/${KEY_FILE}"

# App directories (create if missing)
log "[1/6] Creating required directories..."
mkdir -p "${APP_ROOT}/ImportData/Downloads" "${APP_ROOT}/ImportData/Processed" "${APP_ROOT}/ImportData/Error"
mkdir -p "${APP_ROOT}/ImportLoan/Downloads" "${APP_ROOT}/ImportLoan/Processed" "${APP_ROOT}/ImportLoan/Error"
mkdir -p "${APP_ROOT}/ImportCollateral/Downloads" "${APP_ROOT}/ImportCollateral/Processed" "${APP_ROOT}/ImportCollateral/Error"
mkdir -p "${APP_ROOT}/Logs"

# Network check
log "[2/6] Verifying Docker network..."
if ! docker network inspect ane-network >/dev/null 2>&1; then
  die "Docker network 'ane-network' not found. Create it first: docker network create ane-network"
fi

# Image check
log "[3/6] Verifying image exists: ${IMAGE_NAME}"
if ! docker image inspect "${IMAGE_NAME}" >/dev/null 2>&1; then
  warn "Image not found: ${IMAGE_NAME}"
  warn "Load it first, e.g.: docker load -i ane-api-staging.tar"
  exit 1
fi

# Remove any leftover "next"
if docker ps -a --format '{{.Names}}' | grep -qx "${CONTAINER_NAME_NEXT}"; then
  warn "Found existing ${CONTAINER_NAME_NEXT}; removing..."
  docker rm -f "${CONTAINER_NAME_NEXT}" >/dev/null 2>&1 || true
fi

# ----- Stop old container (downtime starts here) -----
log "[4/6] Stopping old container (if exists): ${CONTAINER_NAME}"
if docker ps -a --format '{{.Names}}' | grep -qx "${CONTAINER_NAME}"; then
  docker stop "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  docker rm "${CONTAINER_NAME}" >/dev/null 2>&1 || true
else
  warn "No existing ${CONTAINER_NAME} found."
fi

# ----- Start new container -----
log "[5/6] Starting new container: ${CONTAINER_NAME}"
docker run -d \
  --name "${CONTAINER_NAME}" \
  --restart unless-stopped \
  --network ane-network \
  -p "${HTTP_PORT}:5000" \
  -p "${HTTPS_PORT}:5001" \
  -p "${HTTPS_EXTERNAL_PORT}:5001" \
  -v "${CERT_DIR}:/https:ro" \
  -v "${APP_ROOT}/ImportData:/app/ImportData" \
  -v "${APP_ROOT}/ImportLoan:/app/ImportLoan" \
  -v "${APP_ROOT}/ImportCollateral:/app/ImportCollateral" \
  -v "${APP_ROOT}/Logs:/app/Logs" \
  -e "ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}" \
  -e "ASPNETCORE_URLS=${ASPNETCORE_URLS}" \
  -e "ASPNETCORE_Kestrel__Certificates__Default__Path=/https/${CERT_FILE}" \
  -e "ASPNETCORE_Kestrel__Certificates__Default__KeyPath=/https/${KEY_FILE}" \
  -e "ASPNETCORE_HTTPS_PORT=5001" \
  -e "ConnectionString=Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}" \
  -e "Cors__AllowedOrigins__0=${CORS_ORIGIN_0}" \
  -e "Cors__AllowedOrigins__1=${CORS_ORIGIN_1}" \
  -e "Cors__AllowedOrigins__2=${CORS_ORIGIN_2}" \
  -e "Cors__AllowedOrigins__3=${CORS_ORIGIN_3}" \
  -e "Cors__AllowedOrigins__4=${CORS_ORIGIN_4}" \
  -e "PortHttp=5000" \
  -e "PortHttps=5001" \
  -e "Sso__Url=https://aneuat.exim.go.th/" \
  "${IMAGE_NAME}" >/dev/null

# ----- Health check loop -----
log "[6/6] Waiting for health check: ${HEALTH_URL} (timeout ${HEALTH_TIMEOUT_SEC}s)"
start_ts="$(date +%s)"
while true; do
  # container must still be running
  if ! docker ps --format '{{.Names}}' | grep -qx "${CONTAINER_NAME}"; then
    err "Container exited unexpectedly."
    docker logs --tail 200 "${CONTAINER_NAME}" || true
    exit 1
  fi

  if curl -fsS "${HEALTH_URL}" >/dev/null 2>&1; then
    ok "Health check OK."
    break
  fi

  now_ts="$(date +%s)"
  elapsed=$(( now_ts - start_ts ))
  if [ "$elapsed" -ge "$HEALTH_TIMEOUT_SEC" ]; then
    err "Health check timed out after ${HEALTH_TIMEOUT_SEC}s"
    err "Last logs:"
    docker logs --tail 200 "${CONTAINER_NAME}" || true
    exit 1
  fi

  sleep "$HEALTH_INTERVAL_SEC"
done

# ----- Summary -----
ip_addr="$(hostname -I | awk '{print $1}')"

echo ""
ok "Container is running successfully!"
echo ""
ok "Container Details:"
docker ps --filter "name=^/${CONTAINER_NAME}$" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""
ok "Environment: ${ASPNETCORE_ENVIRONMENT}"
echo ""
ok "Access URLs:"
echo "  HTTP:  http://${ip_addr}:${HTTP_PORT}"
echo "  HTTPS: https://${ip_addr}:${HTTPS_PORT}"
echo "  HTTPS: https://${ip_addr}:${HTTPS_EXTERNAL_PORT}"
echo ""
ok "Health Check:"
echo "  curl ${HEALTH_URL}"
echo ""
ok "View Logs:"
echo "  docker logs -f ${CONTAINER_NAME}"
echo ""
