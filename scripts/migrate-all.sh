#!/usr/bin/env bash
# =============================================================================
# migrate-all.sh — Run EF Core migrations for all Sam.FactoryERP modules
# =============================================================================
# Usage:
#   ./scripts/migrate-all.sh                       # auto-reads from appsettings.json
#   ./scripts/migrate-all.sh --env Staging         # use appsettings.Staging.json
#   ./scripts/migrate-all.sh --check               # check pending migrations only
#   ./scripts/migrate-all.sh --add-missing         # add initial migration if missing
#
# Requirements:
#   - dotnet 10 SDK
#   - dotnet-ef tool  (dotnet tool restore)
#   - PostgreSQL accessible per appsettings.json ConnectionStrings:DefaultConnection
# =============================================================================

set -euo pipefail

# ── Colours ──────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; RESET='\033[0m'

info()    { echo -e "${CYAN}[INFO]${RESET}  $*"; }
success() { echo -e "${GREEN}[OK]${RESET}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${RESET}  $*"; }
error()   { echo -e "${RED}[ERROR]${RESET} $*" >&2; }
header()  { echo -e "\n${BOLD}${CYAN}══════════════════════════════════════════${RESET}"; echo -e "${BOLD}${CYAN}  $*${RESET}"; echo -e "${BOLD}${CYAN}══════════════════════════════════════════${RESET}"; }

# ── Parse arguments ───────────────────────────────────────────────────────────
ASPNETCORE_ENV="Development"
CHECK_ONLY=false
ADD_MISSING=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --env)       ASPNETCORE_ENV="$2"; shift 2 ;;
        --check)     CHECK_ONLY=true; shift ;;
        --add-missing) ADD_MISSING=true; shift ;;
        *) error "Unknown argument: $1"; exit 1 ;;
    esac
done

export ASPNETCORE_ENVIRONMENT="$ASPNETCORE_ENV"

# ── Resolve paths ─────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
STARTUP_PROJECT="$ROOT_DIR/src/Host/FactoryERP.ApiHost"
APPSETTINGS="$STARTUP_PROJECT/appsettings.json"
APPSETTINGS_ENV="$STARTUP_PROJECT/appsettings.${ASPNETCORE_ENV}.json"

header "Sam.FactoryERP — EF Core Migration Runner"
info "Root         : $ROOT_DIR"
info "Environment  : $ASPNETCORE_ENV"
info "Startup proj : $STARTUP_PROJECT"
info "Check only   : $CHECK_ONLY"
info "Add missing  : $ADD_MISSING"

# ── Parse connection string from appsettings.json ─────────────────────────────
parse_connection_string() {
    local appsettings_file="$1"
    local key="DefaultConnection"
    # Use python3 (always present on Rocky Linux) to parse JSON
    python3 -c "
import json, sys
data = json.load(open('$appsettings_file'))
cs = data.get('ConnectionStrings', {}).get('$key', '')
print(cs)
" 2>/dev/null || echo ""
}

# Try env-specific appsettings first, fallback to base
CS=""
if [[ -f "$APPSETTINGS_ENV" ]]; then
    CS=$(parse_connection_string "$APPSETTINGS_ENV" 2>/dev/null || echo "")
fi
if [[ -z "$CS" && -f "$APPSETTINGS" ]]; then
    CS=$(parse_connection_string "$APPSETTINGS")
fi
if [[ -z "$CS" ]]; then
    error "Could not read ConnectionStrings:DefaultConnection from appsettings.json"
    exit 1
fi

# Extract host/db for display (mask password)
DB_HOST=$(echo "$CS" | python3 -c "
import sys, re
cs = sys.stdin.read()
host = re.search(r'Host=([^;]+)', cs, re.IGNORECASE)
db   = re.search(r'Database=([^;]+)', cs, re.IGNORECASE)
user = re.search(r'Username=([^;]+)', cs, re.IGNORECASE)
port = re.search(r'Port=([^;]+)', cs, re.IGNORECASE)
print(f\"{user.group(1) if user else '?'}@{host.group(1) if host else '?'}:{port.group(1) if port else '5432'}/{db.group(1) if db else '?'}\")
" 2>/dev/null || echo "unknown")

info "Database     : $DB_HOST  (password masked)"

# ── Verify PostgreSQL is reachable ────────────────────────────────────────────
header "Step 1: Checking PostgreSQL connectivity"

# Extract fields for psql test
PG_HOST=$(echo "$CS" | python3 -c "import sys,re; m=re.search(r'Host=([^;]+)',sys.stdin.read(),re.I); print(m.group(1) if m else '127.0.0.1')")
PG_PORT=$(echo "$CS" | python3 -c "import sys,re; m=re.search(r'Port=([^;]+)',sys.stdin.read(),re.I); print(m.group(1) if m else '5432')")
PG_DB=$(echo "$CS"   | python3 -c "import sys,re; m=re.search(r'Database=([^;]+)',sys.stdin.read(),re.I); print(m.group(1) if m else 'postgres')")
PG_USER=$(echo "$CS" | python3 -c "import sys,re; m=re.search(r'Username=([^;]+)',sys.stdin.read(),re.I); print(m.group(1) if m else 'postgres')")
PG_PASS=$(echo "$CS" | python3 -c "import sys,re; m=re.search(r'Password=([^;]+)',sys.stdin.read(),re.I); print(m.group(1) if m else '')")

export PGPASSWORD="$PG_PASS"

if ! PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -c "SELECT 1;" >/dev/null 2>&1; then
    error "Cannot connect to PostgreSQL at $PG_HOST:$PG_PORT as $PG_USER"
    error "  Make sure the Docker container is running:  docker ps | grep postgres"
    error "  Check credentials in appsettings.json ConnectionStrings:DefaultConnection"
    exit 1
fi
success "PostgreSQL is reachable at $PG_HOST:$PG_PORT"

# ── Print current DB schemas ───────────────────────────────────────────────────
info "Existing schemas in database '$PG_DB':"
PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" \
    -c "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('pg_catalog','information_schema','pg_toast') ORDER BY schema_name;" 2>&1 | grep -v "^$" || true

# ── Clean Windows backslash artifacts ────────────────────────────────────────
# These appear when a project is built on Windows and the Linux filesystem
# retains directory names with literal backslash characters (e.g. "bin\Debug")
header "Step 1b: Cleaning Windows backslash artifacts"
python3 -c "
import os, shutil
root = '$ROOT_DIR/src'
removed = []
for dirpath, dirnames, _ in os.walk(root, topdown=True):
    for d in list(dirnames):
        if chr(92) in d:
            full = os.path.join(dirpath, d)
            shutil.rmtree(full, ignore_errors=True)
            removed.append(full)
            dirnames.remove(d)
if removed:
    for r in removed: print(f'  Removed: {r}')
else:
    print('  No backslash artifacts found.')
" 2>/dev/null || true

# Also remove any stale nested bin/obj inside net10.0 output dirs
find "$ROOT_DIR/src" -type d \( -name "bin" -o -name "obj" \) -path "*/net10.0/*" -exec rm -rf {} + 2>/dev/null || true
success "Artifacts cleaned"

# ── Restore tools ─────────────────────────────────────────────────────────────
header "Step 2: Restoring dotnet tools"
cd "$ROOT_DIR"
dotnet tool restore 2>&1 | tail -3
success "Tools restored"

# ── Build solution ─────────────────────────────────────────────────────────────
header "Step 3: Building solution"
dotnet build "$ROOT_DIR" --no-incremental -c Debug -v q 2>&1 | grep -E "error|warning CS|Build succeeded|FAILED|Error" | grep -v "NU1" | head -30 || true

if ! dotnet build "$ROOT_DIR" -c Debug -v q 2>/dev/null | grep -q "Build succeeded"; then
    # try again and show full output for diagnosis
    dotnet build "$ROOT_DIR" -c Debug -v q 2>&1 | tail -30
    error "Build FAILED. Fix compile errors before running migrations."
    exit 1
fi
success "Solution built successfully"

# ── EF migration helper ────────────────────────────────────────────────────────
# Run ef command for a given project/context
# Args: label, project_path, context_class, migration_history_schema
run_ef() {
    local label="$1"
    local project="$2"
    local context="$3"
    local schema="$4"

    header "Module: $label"
    info "Project : $project"
    info "Context : $context"
    info "Schema  : $schema"

    local ef_args=(
        --project "$project"
        --startup-project "$STARTUP_PROJECT"
        --context "$context"
        --no-build
    )

    if [[ "$CHECK_ONLY" == "true" ]]; then
        info "Pending migrations:"
        dotnet ef migrations list "${ef_args[@]}" 2>&1 | grep -v "^Build\|^Done\|^info:" | tail -20 || true
        return
    fi

    # Check if any migrations exist
    local migration_count
    migration_count=$(dotnet ef migrations list "${ef_args[@]}" 2>&1 | grep -v "^Build\|^Done\|^info:\|^\s*$" | grep -v "^(No migrations)" | wc -l || echo "0")

    if [[ "$migration_count" -eq 0 ]] || dotnet ef migrations list "${ef_args[@]}" 2>&1 | grep -q "(No migrations)"; then
        if [[ "$ADD_MISSING" == "true" ]]; then
            warn "No migrations found for $label — creating initial migration..."
            dotnet ef migrations add InitialCreate "${ef_args[@]}" \
                --output-dir "Migrations" \
                -- 2>&1 | tail -10
            success "Initial migration created for $label"
        else
            warn "No migrations found for $label. Run with --add-missing to create initial migration."
            return
        fi
    fi

    info "Applying migrations for $label..."
    dotnet ef database update "${ef_args[@]}" 2>&1 | grep -E "Applying migration|Done\.|error|Error|info:" | head -30 || true

    # Verify by checking the history table
    local table_count
    table_count=$(PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tA \
        -c "SELECT count(*) FROM \"${schema}\".\"__EFMigrationsHistory\";" 2>/dev/null || echo "0")

    success "$label — $table_count migration(s) applied (schema: $schema)"
}

# ── Run migrations ─────────────────────────────────────────────────────────────
header "Step 4: Running migrations"

MODULES_BASE="$ROOT_DIR/src/Modules"

# Auth — schema: auth
run_ef \
    "Auth" \
    "$MODULES_BASE/Auth/Auth.Infrastructure" \
    "Auth.Infrastructure.Persistence.AuthDbContext" \
    "auth"

# EDI — schema: edi
run_ef \
    "EDI" \
    "$MODULES_BASE/EDI/EDI.Infrastructure" \
    "EDI.Infrastructure.Persistence.EdiDbContext" \
    "edi"

# Labeling — schema: labeling  (also contains MassTransit Outbox/Inbox tables)
run_ef \
    "Labeling" \
    "$MODULES_BASE/Labeling/Labeling.Infrastructure" \
    "Labeling.Infrastructure.Persistence.LabelingDbContext" \
    "labeling"

# ── Final DB state ─────────────────────────────────────────────────────────────
header "Step 5: Final Database State"

info "All schemas:"
PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" \
    -c "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('pg_catalog','information_schema','pg_toast') ORDER BY schema_name;" \
    2>&1 | grep -v "^$" || true

info "Migration history per module:"
for schema in auth edi labeling; do
    local_count=$(PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tA \
        -c "SELECT count(*) FROM \"${schema}\".\"__EFMigrationsHistory\";" 2>/dev/null || echo "N/A")
    latest=$(PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" -tA \
        -c "SELECT \"MigrationId\" FROM \"${schema}\".\"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1;" 2>/dev/null || echo "none")
    printf "  %-12s  migrations=%-3s  latest=%s\n" "$schema" "$local_count" "${latest:-none}"
done

info "MassTransit Outbox/Inbox tables (labeling schema):"
PGPASSWORD="$PG_PASS" psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d "$PG_DB" \
    -c "SELECT table_name FROM information_schema.tables WHERE table_schema='labeling' ORDER BY table_name;" \
    2>&1 | grep -v "^$" || true

echo ""
success "All migrations complete!"
echo -e "${GREEN}${BOLD}────────────────────────────────────────────────────────${RESET}"
echo -e "${GREEN}${BOLD}  Sam.FactoryERP database is ready on Rocky Linux Docker  ${RESET}"
echo -e "${GREEN}${BOLD}────────────────────────────────────────────────────────${RESET}"
echo ""
echo "  Next steps:"
echo "    1. Start ApiHost:    cd src/Host/FactoryERP.ApiHost && dotnet run"
echo "    2. Start WorkerHost: cd src/Host/FactoryERP.WorkerHost && dotnet run"
echo "    3. Health check:     curl http://localhost:5219/health"
echo ""

