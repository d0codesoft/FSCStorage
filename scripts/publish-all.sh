#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_ROOT="${OUTPUT_ROOT:-publish}"
SELF_CONTAINED="${SELF_CONTAINED:-true}"
RUNTIME_IDENTIFIERS="${RUNTIME_IDENTIFIERS:-win-x64 linux-x64}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SERVICE_PROJECT="$REPO_ROOT/SCP.StorageFSC/scp.filestorage.csproj"
ADMIN_CLI_PROJECT="$REPO_ROOT/fsc_adm_cli/fsc_adm_cli.csproj"
OUTPUT_ROOT_PATH="$REPO_ROOT/$OUTPUT_ROOT"

for rid in $RUNTIME_IDENTIFIERS; do
    output_path="$OUTPUT_ROOT_PATH/$rid"
    rm -rf "$output_path"
    mkdir -p "$output_path"

    dotnet publish "$SERVICE_PROJECT" \
        --configuration "$CONFIGURATION" \
        --runtime "$rid" \
        --self-contained "$SELF_CONTAINED" \
        --output "$output_path" \
        /p:PublishSingleFile=false

    dotnet publish "$ADMIN_CLI_PROJECT" \
        --configuration "$CONFIGURATION" \
        --runtime "$rid" \
        --self-contained "$SELF_CONTAINED" \
        --output "$output_path" \
        /p:PublishSingleFile=false
done

echo "Published service and admin CLI to: $OUTPUT_ROOT_PATH"
