#!/usr/bin/env bash
# ANE API - Offline Docker Build (Staging) - Rocky Linux
# ======================================================
# Build locally -> publish -> docker build from published output -> export image to tar
# Works with bash and zsh.
#
# Usage:
#   ./deployment/scripts/build-offline-staging.sh
#   IMAGE_TAG=staging ./deployment/scripts/build-offline-staging.sh
#   NO_CLEANUP=1 ./deployment/scripts/build-offline-staging.sh
#
# Env:
#   IMAGE_NAME (default: ane-api)
#   IMAGE_TAG  (default: staging)
#   API_CSPROJ (default: src/API/API.csproj)
#   DOCKERFILE (default: deployment/Dockerfile.offline)
#   NO_CLEANUP (default: 0)  # set to 1 to keep deployment temp folders

set -Eeuo pipefail

# -------- helpers --------
say() { printf '%s\n' "$*"; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

step() {
  local i="$1" total="$2" msg="$3"
  echo
  echo "[$i/$total] $msg"
}

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "Missing required command: $1 ($2)"
}

rm_rf_if_exists() {
  local p="$1"
  [ -e "$p" ] && rm -rf "$p"
}

mkdirp() {
  mkdir -p "$1"
}

copy_dir_if_exists() {
  local src="$1" dst="$2" label="$3"
  if [ -d "$src" ]; then
    mkdirp "$dst"
    # preserve attrs; works across most Rocky setups
    cp -a "$src/." "$dst/"
    say "  - Copied $label"
    return 0
  fi
  say "  - WARNING: $label not found at $src"
  return 1
}

copy_file_if_exists() {
  local src="$1" dst="$2" label="$3"
  if [ -f "$src" ]; then
    mkdirp "$(dirname "$dst")"
    cp -a "$src" "$dst"
    say "  - Copied $label"
    return 0
  fi
  say "  - WARNING: $label not found at $src"
  return 1
}

# -------- locate repo root --------
# This script expected at: <repo>/deployment/scripts/build-offline-staging.sh
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]:-${(%):-%x}}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$REPO_ROOT"

# -------- config --------
IMAGE_NAME="${IMAGE_NAME:-ane-api}"
IMAGE_TAG="${IMAGE_TAG:-staging}"
FULL_IMAGE_NAME="${IMAGE_NAME}:${IMAGE_TAG}"

API_CSPROJ="${API_CSPROJ:-src/API/API.csproj}"
DOCKERFILE="${DOCKERFILE:-deployment/Dockerfile.offline}"

DEPLOY_ROOT="$REPO_ROOT/deployment"
PUBLISH_DIR="$DEPLOY_ROOT/publish"
DEPS_DIR="$DEPLOY_ROOT/docker-deps"
SOURCES_DIR="$DEPLOY_ROOT/Sources"
CERTS_DIR="$DEPLOY_ROOT/ca-certificates"
LICENSES_DIR="$DEPLOY_ROOT/Licenses"
ENTRYPOINT_DST="$DEPLOY_ROOT/docker-entrypoint.sh"
OUTPUT_DIR="$DEPLOY_ROOT/output"

TAR_FILE="${IMAGE_NAME}-${IMAGE_TAG}.tar"
TAR_PATH="$OUTPUT_DIR/$TAR_FILE"

TOTAL_STEPS=9

# -------- pre-checks --------
step 1 "$TOTAL_STEPS" "Pre-check prerequisites..."
need_cmd dotnet "Install .NET SDK on Rocky Linux."
need_cmd docker "Install Docker Engine and ensure your user can run docker."
[ -f "$API_CSPROJ" ] || die "API csproj not found: $API_CSPROJ"
[ -f "$DOCKERFILE" ] || die "Dockerfile not found: $DOCKERFILE"

# Optional git commit label (if repo)
GIT_COMMIT=""
if command -v git >/dev/null 2>&1 && git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  GIT_COMMIT="$(git rev-parse --short HEAD 2>/dev/null || true)"
fi

# Cleanup on exit unless NO_CLEANUP=1
NO_CLEANUP="${NO_CLEANUP:-0}"
cleanup() {
  if [ "$NO_CLEANUP" = "1" ]; then
    say ""
    say "NOTE: NO_CLEANUP=1 -> keeping deployment temp folders for inspection."
    return 0
  fi

  rm_rf_if_exists "$PUBLISH_DIR"
  rm_rf_if_exists "$DEPS_DIR"
  rm_rf_if_exists "$SOURCES_DIR"
  rm_rf_if_exists "$CERTS_DIR"
  rm_rf_if_exists "$LICENSES_DIR"
  [ -f "$ENTRYPOINT_DST" ] && rm -f "$ENTRYPOINT_DST" || true
}
trap cleanup EXIT

# -------- clean --------
step 2 "$TOTAL_STEPS" "Cleaning previous deployment build artifacts..."
rm_rf_if_exists "$PUBLISH_DIR"
rm_rf_if_exists "$DEPS_DIR"
rm_rf_if_exists "$SOURCES_DIR"
rm_rf_if_exists "$CERTS_DIR"
rm_rf_if_exists "$LICENSES_DIR"
[ -f "$ENTRYPOINT_DST" ] && rm -f "$ENTRYPOINT_DST" || true

# -------- fix line endings / permissions (Linux side) --------
step 3 "$TOTAL_STEPS" "Ensuring scripts are LF + executable (best-effort)..."
# On Linux, CRLF sometimes sneaks in from Windows. If dos2unix exists, run it.
if command -v dos2unix >/dev/null 2>&1; then
  # convert all .sh under repo (safe scope: deployment + root entrypoint)
  find "$REPO_ROOT" -type f \( -name "*.sh" -o -name ".env" -o -name "*.conf" \) -print0 2>/dev/null \
    | xargs -0 -r dos2unix >/dev/null 2>&1 || true
  say "  - dos2unix applied (best-effort)"
else
  say "  - dos2unix not installed; skipping CRLF conversion (optional: sudo dnf install -y dos2unix)"
fi

# Ensure entrypoint executable (if exists in root)
if [ -f "$REPO_ROOT/docker-entrypoint.sh" ]; then
  chmod +x "$REPO_ROOT/docker-entrypoint.sh" || true
fi

# -------- copy dependencies/assets into build context --------
step 4 "$TOTAL_STEPS" "Copying dependencies/assets into deployment context..."

# docker-deps
if [ -d "$REPO_ROOT/docker-deps" ]; then
  mkdirp "$DEPS_DIR"
  cp -a "$REPO_ROOT/docker-deps/." "$DEPS_DIR/"
  say "  - Copied docker-deps"
else
  mkdirp "$DEPS_DIR"
  say "  - Created empty deployment/docker-deps (no .deb files found)"
fi

# Sources: Fonts + Reports
API_SOURCES="$REPO_ROOT/src/API/Sources"
if [ -d "$API_SOURCES" ]; then
  mkdirp "$SOURCES_DIR"
  copy_dir_if_exists "$API_SOURCES/Fonts" "$SOURCES_DIR/Fonts" "Sources/Fonts" || true
  copy_dir_if_exists "$API_SOURCES/Reports" "$SOURCES_DIR/Reports" "Sources/Reports" || true

  # counts (best-effort)
  FONT_COUNT=$(find "$SOURCES_DIR/Fonts" -maxdepth 1 -type f -name "*.ttf" 2>/dev/null | wc -l | tr -d ' ' || true)
  REPORT_COUNT=$(find "$SOURCES_DIR/Reports" -maxdepth 1 -type f -name "*.mrt" 2>/dev/null | wc -l | tr -d ' ' || true)
  [ -d "$SOURCES_DIR/Fonts" ] && say "  - Thai fonts: ${FONT_COUNT:-0}"
  [ -d "$SOURCES_DIR/Reports" ] && say "  - Report templates: ${REPORT_COUNT:-0}"
else
  say "  - WARNING: Sources folder not found: $API_SOURCES"
fi

# CA certificates
if [ -d "$REPO_ROOT/ca-certificates" ]; then
  mkdirp "$CERTS_DIR"
  cp -a "$REPO_ROOT/ca-certificates/"*.crt "$CERTS_DIR/" 2>/dev/null || true
  CERT_COUNT=$(find "$CERTS_DIR" -maxdepth 1 -type f -name "*.crt" 2>/dev/null | wc -l | tr -d ' ' || true)
  say "  - CA certificates: ${CERT_COUNT:-0}"
else
  say "  - WARNING: ca-certificates folder not found (TLS trust inside container may fail)"
fi

# docker-entrypoint.sh
copy_file_if_exists "$REPO_ROOT/docker-entrypoint.sh" "$ENTRYPOINT_DST" "docker-entrypoint.sh" || true

# Licenses
copy_dir_if_exists "$REPO_ROOT/src/API/Licenses" "$LICENSES_DIR" "Licenses" || true

# -------- dotnet publish --------
step 5 "$TOTAL_STEPS" "dotnet publish (Release) -> deployment/publish ..."
# Offline-safe: avoid restore; rely on local NuGet cache / prior restore
dotnet publish "$API_CSPROJ" -c Release -o "$PUBLISH_DIR" --nologo --no-restore

# -------- docker build --------
step 6 "$TOTAL_STEPS" "docker build (offline Dockerfile) ..."
mkdirp "$OUTPUT_DIR"

LABELS=(--label "org.opencontainers.image.title=${IMAGE_NAME}"
        --label "org.opencontainers.image.created=$(date -Iseconds)")
if [ -n "$GIT_COMMIT" ]; then
  LABELS+=(--label "org.opencontainers.image.revision=${GIT_COMMIT}")
fi

docker build -f "$DOCKERFILE" -t "$FULL_IMAGE_NAME" "${LABELS[@]}" "$DEPLOY_ROOT"

# -------- docker save --------
step 7 "$TOTAL_STEPS" "docker save -> $TAR_PATH"
docker save "$FULL_IMAGE_NAME" -o "$TAR_PATH"

# -------- verify --------
step 8 "$TOTAL_STEPS" "Verifying tar output..."
[ -f "$TAR_PATH" ] || die "Tar not found after export: $TAR_PATH"
SIZE_BYTES=$(stat -c %s "$TAR_PATH" 2>/dev/null || echo 0)
SIZE_MB=$(( (SIZE_BYTES + 1048575) / 1048576 ))
say "  - Tar size: ${SIZE_MB} MB"
say "  - Image: $FULL_IMAGE_NAME"
[ -n "$GIT_COMMIT" ] && say "  - Git: $GIT_COMMIT"

# -------- summary --------
step 9 "$TOTAL_STEPS" "DONE - Next steps"
cat <<EOF

========================================
BUILD SUCCESSFUL (Staging)
========================================
Image: $FULL_IMAGE_NAME
File:  deployment/output/$TAR_FILE
Size:  ${SIZE_MB} MB

Deploy on target server:
  docker load -i /tmp/$TAR_FILE
  sudo ./deploy-staging.sh

EOF
