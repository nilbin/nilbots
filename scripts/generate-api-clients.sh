#!/usr/bin/env bash
# Regenerate every API client from the server's own OpenAPI document.
#
# The document is a BUILD artifact (Microsoft.Extensions.ApiDescription.Server writes
# contracts/BotArena.App.json during `dotnet build`), so this needs no running server
# and no database — which is what lets CI run it on every push.
#
# Consumers, all generated, none hand-maintained:
#   web/src/api/schema.d.ts        TypeScript  (site)
#   mobile/src/api/schema.d.ts     TypeScript  (Expo app)
#   src/BotArena.Cli/Generated/    C# DTOs     (CLI; DTOs only — the CLI keeps its own HttpClient)
#
# Run this whenever you change an endpoint's route, request, or response. CI runs it too
# and fails if the committed output differs, so forgetting is a red build, not a silent
# drift (see .github/workflows/ci.yml → contract-drift).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

DOCUMENT="contracts/BotArena.App.json"
# Pinned so every machine and CI emit byte-identical output; a floating version would
# make the drift check fail on unrelated tooling upgrades.
OPENAPI_TS_VERSION="7.13.0"

echo "==> Building BotArena.App (emits $DOCUMENT)"
dotnet build src/BotArena.App/BotArena.App.csproj --nologo -v quiet

if [[ ! -f "$DOCUMENT" ]]; then
  echo "ERROR: $DOCUMENT was not produced by the build." >&2
  echo "       Check OpenApiDocumentsDirectory in src/BotArena.App/BotArena.App.csproj." >&2
  exit 1
fi

echo "==> TypeScript: web"
mkdir -p web/src/api
npx --yes "openapi-typescript@${OPENAPI_TS_VERSION}" "$DOCUMENT" -o web/src/api/schema.d.ts

echo "==> TypeScript: mobile"
mkdir -p mobile/src/api
npx --yes "openapi-typescript@${OPENAPI_TS_VERSION}" "$DOCUMENT" -o mobile/src/api/schema.d.ts

echo "==> C#: CLI data contracts"
mkdir -p src/BotArena.Cli/Generated
dotnet tool restore >/dev/null
# DTOs only. The CLI already owns its HttpClient, auth, and token refresh; generating a
# client class on top would duplicate that and drag in NSwag's runtime.
dotnet nswag openapi2csclient \
  /input:"$DOCUMENT" \
  /output:src/BotArena.Cli/Generated/ApiContracts.cs \
  /namespace:BotArena.Cli.Generated \
  /generateClientClasses:false \
  /generateDtoTypes:true \
  /jsonLibrary:SystemTextJson \
  /generateOptionalPropertiesAsNullable:true \
  /generateNullableReferenceTypes:true \
  /arrayType:System.Collections.Generic.IReadOnlyList \
  /arrayInstanceType:System.Collections.Generic.List

echo
echo "Done. If 'git status' is dirty, commit the regenerated output alongside your change."
