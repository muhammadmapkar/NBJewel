#!/usr/bin/env bash
# Role: One command to get the local SQL Server ready for Sql mode.
# It starts the SQL Server container, waits until it is healthy, then runs the
# .NET setup tool: create the database, apply docs/item_catalog_schema.sql,
# and seed sample-data/*.json. Safe to run again (it skips what already exists).
# Use "./scripts/setup-db.sh --reset" to drop the database and load fresh sample data.
# It needs a .env file (copy .env.example) with MSSQL_SA_PASSWORD and NBJEWEL_SQL_CONNECTION set.

set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "Missing .env. Copy .env.example to .env and fill in MSSQL_SA_PASSWORD and NBJEWEL_SQL_CONNECTION." >&2
  exit 1
fi

# Load .env into this shell so dotnet can read NBJEWEL_SQL_CONNECTION.
set -a
# shellcheck disable=SC1091
source .env
set +a

: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is not set in .env}"
: "${NBJEWEL_SQL_CONNECTION:?NBJEWEL_SQL_CONNECTION is not set in .env}"

docker compose up -d --wait
dotnet run --project backend/NbJewel.Api -- --setup-db "$@"
