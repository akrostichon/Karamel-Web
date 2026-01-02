#!/usr/bin/env bash
set -euo pipefail

if [ -z "${CONNECTION_STRING:-}" ]; then
  echo "CONNECTION_STRING must be set"
  exit 1
fi

echo "Running EF Core migrations against: $CONNECTION_STRING"
export ConnectionStrings__DefaultConnection="$CONNECTION_STRING"
dotnet tool restore || true
dotnet ef database update --project Karamel.Backend --startup-project Karamel.Backend

echo "Migrations applied successfully"
