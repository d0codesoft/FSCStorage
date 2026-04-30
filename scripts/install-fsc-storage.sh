#!/usr/bin/env sh
set -eu

SERVICE_NAME="fsc-storage"
SERVICE_USER="fstore"
SERVICE_GROUP="fstore"

APP_DIR="/opt/fsc-storage"
BASE_DIR="/var/lib/fsc-storage"
DATA_DIR="/var/lib/fsc-storage/data"
LOG_DIR="/var/log/fsc-storage"

SYSTEMD_DIR="/etc/systemd/system"
ENV_DIR="/etc/default"
SERVICE_FILE="${SERVICE_NAME}.service"
ENV_FILE="${SERVICE_NAME}-env"

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SOURCE_SERVICE="${SCRIPT_DIR}/${SERVICE_FILE}"
SOURCE_ENV="${SCRIPT_DIR}/${ENV_FILE}"

log() {
    printf '%s\n' "$1"
}

fail() {
    printf 'ERROR: %s\n' "$1" >&2
    exit 1
}

require_root() {
    if [ "$(id -u)" -ne 0 ]; then
        fail "This script must be run as root."
    fi
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "Required command not found: $1"
}

create_group() {
    if getent group "${SERVICE_GROUP}" >/dev/null 2>&1; then
        log "Group '${SERVICE_GROUP}' already exists."
    else
        log "Creating group '${SERVICE_GROUP}'."
        groupadd --system "${SERVICE_GROUP}"
    fi
}

create_user() {
    if id -u "${SERVICE_USER}" >/dev/null 2>&1; then
        log "User '${SERVICE_USER}' already exists."
    else
        log "Creating system user '${SERVICE_USER}'."
        useradd \
            --system \
            --gid "${SERVICE_GROUP}" \
            --home-dir "${APP_DIR}" \
            --shell /usr/sbin/nologin \
            "${SERVICE_USER}"
    fi
}

create_directories() {
    log "Creating application and data directories."
    install -d -m 0755 -o root -g root "${APP_DIR}"
    install -d -m 0750 -o "${SERVICE_USER}" -g "${SERVICE_GROUP}" "${BASE_DIR}"
    install -d -m 0750 -o "${SERVICE_USER}" -g "${SERVICE_GROUP}" "${DATA_DIR}"
    install -d -m 0750 -o "${SERVICE_USER}" -g "${SERVICE_GROUP}" "${LOG_DIR}"
}

install_files() {
    [ -f "${SOURCE_SERVICE}" ] || fail "Service file not found: ${SOURCE_SERVICE}"
    [ -f "${SOURCE_ENV}" ] || fail "Environment file not found: ${SOURCE_ENV}"

    log "Installing systemd service file."
    install -m 0644 -o root -g root "${SOURCE_SERVICE}" "${SYSTEMD_DIR}/${SERVICE_FILE}"

    log "Installing environment file."
    install -m 0640 -o root -g "${SERVICE_GROUP}" "${SOURCE_ENV}" "${ENV_DIR}/${ENV_FILE}"
}

validate_environment_file() {
    log "Validating environment file."
    grep -q '^Paths__BasePath=/var/lib/fsc-storage$' "${ENV_DIR}/${ENV_FILE}" \
        || fail "Paths__BasePath is not configured correctly."
    grep -q '^Paths__LogsPath=/var/log/fsc-storage$' "${ENV_DIR}/${ENV_FILE}" \
        || fail "Paths__LogsPath is not configured correctly."
    grep -q '^Paths__DataPath=/var/lib/fsc-storage/data$' "${ENV_DIR}/${ENV_FILE}" \
        || fail "Paths__DataPath is not configured correctly."
}

validate_service_file() {
    log "Validating systemd service file."
    grep -q '^User=fstore$' "${SYSTEMD_DIR}/${SERVICE_FILE}" \
        || fail "Service user is not configured as fstore."
    grep -q '^Group=fstore$' "${SYSTEMD_DIR}/${SERVICE_FILE}" \
        || fail "Service group is not configured as fstore."
    grep -q '^EnvironmentFile=/etc/default/fsc-storage-env$' "${SYSTEMD_DIR}/${SERVICE_FILE}" \
        || fail "Service environment file path is not configured correctly."
}

validate_permissions() {
    log "Validating directory ownership and permissions."
    [ -d "${APP_DIR}" ] || fail "Application directory was not created: ${APP_DIR}"
    [ -d "${BASE_DIR}" ] || fail "Base directory was not created: ${BASE_DIR}"
    [ -d "${DATA_DIR}" ] || fail "Data directory was not created: ${DATA_DIR}"
    [ -d "${LOG_DIR}" ] || fail "Log directory was not created: ${LOG_DIR}"

    su -s /bin/sh -c "test -w '${BASE_DIR}' && test -w '${DATA_DIR}' && test -w '${LOG_DIR}'" "${SERVICE_USER}" \
        || fail "Service user cannot write to one or more data directories."
}

configure_systemd() {
    log "Reloading systemd daemon."
    systemctl daemon-reload

    log "Enabling ${SERVICE_NAME}.service."
    systemctl enable "${SERVICE_NAME}.service"

    if systemctl is-enabled "${SERVICE_NAME}.service" >/dev/null 2>&1; then
        log "Service is enabled."
    else
        fail "Service was not enabled."
    fi
}

print_runtime_note() {
    if [ ! -f "${APP_DIR}/scp.filestorage.dll" ]; then
        log "WARNING: ${APP_DIR}/scp.filestorage.dll was not found."
        log "Copy the published application files to ${APP_DIR} before starting the service."
    fi

    log "Installation completed successfully."
    log "Start the service with: systemctl start ${SERVICE_NAME}.service"
    log "Check status with: systemctl status ${SERVICE_NAME}.service"
}

main() {
    log "Installing FSC Storage Linux service."

    require_root
    require_command getent
    require_command groupadd
    require_command useradd
    require_command install
    require_command grep
    require_command systemctl
    require_command su

    create_group
    create_user
    create_directories
    install_files
    validate_environment_file
    validate_service_file
    validate_permissions
    configure_systemd
    print_runtime_note
}

main "$@"
