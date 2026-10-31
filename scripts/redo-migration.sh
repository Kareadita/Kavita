#!/usr/bin/env bash
# Redo the latest EF Core migration, keeping the same name.
# Can be run from anywhere; the script cd's to the repo root (git toplevel,
# falling back to the parent of the directory this script lives in).
#
# Usage:
#   ./redo-migration.sh              # do it
#   ./redo-migration.sh -n|--dry-run # print what would happen, change nothing
#
# Steps:
#   1. Roll the database back to the second-to-last migration
#   2. Remove the last migration
#   3. Re-add a migration with the same name

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if ! REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null)"; then
  REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
fi
cd "$REPO_ROOT"

DRY_RUN=false
for arg in "$@"; do
  case "$arg" in
    -n|--dry-run) DRY_RUN=true ;;
    -h|--help)
      sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "Unknown argument: $arg (use --dry-run or --help)" >&2
      exit 1
      ;;
  esac
done

MIGRATIONS_DIR="Kavita.Database/Migrations"
DB_PROJECT="Kavita.Database/Kavita.Database.csproj"
STARTUP_PROJECT="Kavita.Server/Kavita.Server.csproj"
CONTEXT="Kavita.Database.DataContext"
CONFIGURATION="Debug"

# Run a command, or just print it in dry-run mode.
run() {
  if $DRY_RUN; then
    printf '[dry-run] '
    printf '%q ' "$@"
    printf '\n'
  else
    "$@"
  fi
}

# List migration IDs (e.g. 20261010202733_ScannerAndMediaErrors), oldest -> newest.
# Excludes *.Designer.cs and the model snapshot.
list_migrations() {
  ls "$MIGRATIONS_DIR" \
    | grep -E '^[0-9]{14}_.+\.cs$' \
    | grep -v '\.Designer\.cs$' \
    | sed 's/\.cs$//' \
    | sort
}

count=$(list_migrations | wc -l | tr -d ' ')

if [ "$count" -lt 1 ]; then
  echo "No migrations found in $MIGRATIONS_DIR" >&2
  exit 1
fi

last_migration=$(list_migrations | tail -n 1)
migration_name="${last_migration#*_}"   # strip the timestamp prefix

if [ "$count" -ge 2 ]; then
  rollback_target=$(list_migrations | tail -n 2 | head -n 1)
else
  # Only one migration exists: roll back to the empty database
  rollback_target="0"
fi

echo "Last migration:   $last_migration"
echo "Rolling back to:  $rollback_target"
echo "Re-adding as:     $migration_name"
$DRY_RUN && echo "Mode:             DRY RUN (nothing will be changed)"
echo

COMMON_ARGS=(
  --project "$DB_PROJECT"
  --startup-project "$STARTUP_PROJECT"
  --context "$CONTEXT"
  --configuration "$CONFIGURATION"
)

echo ">>> Rolling back database..."
run dotnet ef database update "${COMMON_ARGS[@]}" "$rollback_target"

echo ">>> Removing last migration..."
run dotnet ef migrations remove "${COMMON_ARGS[@]}" --force

echo ">>> Re-adding migration '$migration_name'..."
run dotnet ef migrations add "${COMMON_ARGS[@]}" "$migration_name" --output-dir Migrations

echo
if $DRY_RUN; then
  echo "Dry run complete. No changes were made."
else
  echo "Done. Migration '$migration_name' has been redone."
  echo "Run 'dotnet ef database update' (or start the app) to apply it."
fi
