#!/usr/bin/env bash
set -Eeuo pipefail

REPO_URL="${FSC_STORAGE_REPO_URL:-https://github.com/d0codesoft/FSCStorage.git}"
REPO_REF="${FSC_STORAGE_REF:-}"

SERVICE_NAME="fsc_storage"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
SERVICE_USER="fsc-user"
SERVICE_GROUP="fsc-user"

APP_DIR="/opt/fsc.storage"
ENV_FILE="/etc/default/fscstorage-env"
DEFAULT_ROOT="/var/lib/fsc.storage"

log() {
    printf '%s\n' "$1"
}

fail() {
    printf 'ERROR: %s\n' "$1" >&2
    exit 1
}

require_root() {
    if [ "$(id -u)" -ne 0 ]; then
        fail "Run this script as root."
    fi
}

require_ubuntu() {
    if [ ! -r /etc/os-release ]; then
        fail "/etc/os-release was not found."
    fi

    # shellcheck disable=SC1091
    . /etc/os-release

    if [ "${ID:-}" != "ubuntu" ]; then
        fail "This installer supports Ubuntu only. Current OS: ${PRETTY_NAME:-unknown}."
    fi

    UBUNTU_VERSION_ID="${VERSION_ID:-}"
    if [ -z "$UBUNTU_VERSION_ID" ]; then
        fail "Could not detect Ubuntu VERSION_ID."
    fi
}

install_packages() {
    log "Installing required packages."
    export DEBIAN_FRONTEND=noninteractive

    apt-get update
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        git \
        python3

    if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -Eq '^10\.'; then
        log "Installing Microsoft package repository for Ubuntu ${UBUNTU_VERSION_ID}."
        local packages_deb="/tmp/packages-microsoft-prod.deb"

        curl -fsSL \
            "https://packages.microsoft.com/config/ubuntu/${UBUNTU_VERSION_ID}/packages-microsoft-prod.deb" \
            -o "$packages_deb"
        dpkg -i "$packages_deb"
        rm -f "$packages_deb"

        apt-get update
        apt-get install -y --no-install-recommends dotnet-sdk-10.0
    fi

    command -v dotnet >/dev/null 2>&1 || fail "dotnet was not installed."
    dotnet --list-sdks | grep -Eq '^10\.' || fail ".NET 10 SDK was not installed."
}

read_root_path() {
    local root_path

    printf 'Enter storage root path [{Root}] [%s]: ' "$DEFAULT_ROOT"
    read -r root_path
    root_path="${root_path:-$DEFAULT_ROOT}"

    case "$root_path" in
        /*) ;;
        *) fail "Storage root path must be absolute: ${root_path}" ;;
    esac

    case "$root_path" in
        *[[:space:]]*|*"'"*|*'"'*)
            fail "Storage root path must not contain spaces or quotes: ${root_path}"
            ;;
    esac

    ROOT_PATH="${root_path%/}"
    if [ -z "$ROOT_PATH" ]; then
        fail "Storage root path cannot be empty."
    fi
}

create_service_account() {
    if getent group "$SERVICE_GROUP" >/dev/null 2>&1; then
        log "Group ${SERVICE_GROUP} already exists."
    else
        log "Creating group ${SERVICE_GROUP}."
        groupadd --system "$SERVICE_GROUP"
    fi

    if id -u "$SERVICE_USER" >/dev/null 2>&1; then
        log "User ${SERVICE_USER} already exists."
    else
        log "Creating user ${SERVICE_USER}."
        useradd \
            --system \
            --gid "$SERVICE_GROUP" \
            --home-dir "$APP_DIR" \
            --shell /usr/sbin/nologin \
            "$SERVICE_USER"
    fi
}

create_directories() {
    log "Creating application and storage directories."
    install -d -m 0755 -o root -g root "$APP_DIR"
    install -d -m 0750 -o "$SERVICE_USER" -g "$SERVICE_GROUP" "$ROOT_PATH"
    install -d -m 0750 -o "$SERVICE_USER" -g "$SERVICE_GROUP" "$ROOT_PATH/logs"
    install -d -m 0750 -o "$SERVICE_USER" -g "$SERVICE_GROUP" "$ROOT_PATH/data"
    install -d -m 0750 -o "$SERVICE_USER" -g "$SERVICE_GROUP" "$ROOT_PATH/temp"
}

prepare_build_dir() {
    BUILD_DIR="$(mktemp -d /tmp/fsc-storage-build.XXXXXX)"
    PUBLISH_DIR="$BUILD_DIR/publish"

    cleanup() {
        if [ -n "${BUILD_DIR:-}" ] && [ -d "$BUILD_DIR" ]; then
            rm -rf "$BUILD_DIR"
        fi
    }

    trap cleanup EXIT
}

clone_sources() {
    log "Cloning sources from ${REPO_URL}."
    git clone "$REPO_URL" "$BUILD_DIR/src"

    if [ -n "$REPO_REF" ]; then
        log "Checking out ${REPO_REF}."
        git -C "$BUILD_DIR/src" checkout "$REPO_REF"
    fi
}

publish_service() {
    log "Publishing service build."
    dotnet publish "$BUILD_DIR/src/SCP.StorageFSC/scp.filestorage.csproj" \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained false \
        --output "$PUBLISH_DIR" \
        /p:PublishSingleFile=false
}

install_application() {
    log "Installing published files to ${APP_DIR}."

    if systemctl list-unit-files "${SERVICE_NAME}.service" >/dev/null 2>&1; then
        systemctl stop "${SERVICE_NAME}.service" >/dev/null 2>&1 || true
    fi

    find "$APP_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +
    cp -a "$PUBLISH_DIR/." "$APP_DIR/"
    chown -R root:root "$APP_DIR"
    find "$APP_DIR" -type d -exec chmod 0755 {} +
    find "$APP_DIR" -type f -exec chmod 0644 {} +
}

write_appsettings() {
    local appsettings="$APP_DIR/appsettings.json"

    [ -f "$appsettings" ] || fail "Published appsettings.json was not found: ${appsettings}"

    log "Writing {Root} path to appsettings.json."
    python3 - "$appsettings" "$ROOT_PATH" <<'PY'
import json
import sys
from pathlib import Path

appsettings = Path(sys.argv[1])
root_path = sys.argv[2]

with appsettings.open("r", encoding="utf-8") as stream:
    data = json.load(stream)

paths = data.setdefault("Paths", {})
paths["BasePath"] = root_path
paths["LogsPath"] = "{Root}/logs"
paths["DataPath"] = "{Root}/data"
paths["TempPath"] = "{Root}/temp"

with appsettings.open("w", encoding="utf-8") as stream:
    json.dump(data, stream, indent=2, ensure_ascii=False)
    stream.write("\n")
PY

    chown root:"$SERVICE_GROUP" "$appsettings"
    chmod 0640 "$appsettings"
}

write_environment_file() {
    log "Writing ${ENV_FILE}."
    cat >"$ENV_FILE" <<'EOF'
ASPNETCORE_ENVIRONMENT=Production
DOTNET_NOLOGO=true
DOTNET_CLI_TELEMETRY_OPTOUT=1
DOTNET_PRINT_TELEMETRY_MESSAGE=false
EOF

    chown root:"$SERVICE_GROUP" "$ENV_FILE"
    chmod 0640 "$ENV_FILE"
}

write_service_file() {
    log "Writing ${SERVICE_FILE}."
    cat >"$SERVICE_FILE" <<EOF
[Unit]
Description=File storage service
After=network.target

[Service]
WorkingDirectory=${APP_DIR}
ExecStart=/usr/bin/dotnet ${APP_DIR}/scp.filestorage.dll

SyslogIdentifier=fsc-storage
User=${SERVICE_USER}
Group=${SERVICE_GROUP}

Restart=always
RestartSec=10
StartLimitIntervalSec=300
StartLimitBurst=5
RestartForceExitStatus=SIGABRT SIGSEGV

Type=notify
WatchdogSec=30
NotifyAccess=main

StandardOutput=journal
StandardError=journal

ReadWritePaths=${ROOT_PATH} ${APP_DIR}

EnvironmentFile=${ENV_FILE}

KillSignal=SIGINT
TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
EOF

    chown root:root "$SERVICE_FILE"
    chmod 0644 "$SERVICE_FILE"
}

enable_service() {
    log "Reloading systemd and enabling ${SERVICE_NAME}.service."
    systemctl daemon-reload
    systemctl enable "${SERVICE_NAME}.service"
}

validate_installation() {
    log "Validating installation."

    [ -f "$APP_DIR/scp.filestorage.dll" ] || fail "Service dll was not installed."
    [ -f "$APP_DIR/appsettings.json" ] || fail "appsettings.json was not installed."
    [ -f "$SERVICE_FILE" ] || fail "systemd service file was not created."

    su -s /bin/sh -c "test -w '$ROOT_PATH' && test -w '$ROOT_PATH/logs' && test -w '$ROOT_PATH/data' && test -w '$ROOT_PATH/temp'" "$SERVICE_USER" \
        || fail "Service user cannot write to one or more storage directories."
}

main() {
    require_root
    require_ubuntu
    install_packages
    read_root_path
    create_service_account
    create_directories
    prepare_build_dir
    clone_sources
    publish_service
    install_application
    write_appsettings
    write_environment_file
    write_service_file
    enable_service
    validate_installation

    log "Installation completed."
    log "Start service: systemctl start ${SERVICE_NAME}.service"
    log "Check status: systemctl status ${SERVICE_NAME}.service"
    log "Storage root: ${ROOT_PATH}"
}

main "$@"
