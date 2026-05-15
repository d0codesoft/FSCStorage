# FSC Storage Service

FSC Storage is a simple HTTP service for storing files and deleting them by identifiers. It accepts uploaded files, saves the binary content on disk, keeps file metadata in a local SQLite database, and returns stable file identifiers that clients can use later to download, inspect, list, or delete files.

The service is built with ASP.NET Core and is intended to be easy to run as a small standalone file storage component. It supports tenant-aware access, API-token authentication, content deduplication, and administrative endpoints for tenant and token management.

## Core Principles

- **Tenant isolation**: every file belongs to a tenant and is accessed through the tenant context resolved from request headers.
- **API-token authentication**: each request is authenticated by an API token and tenant id.
- **Filesystem-backed storage**: file content is stored under the configured data directory, while metadata is stored in SQLite.
- **Content deduplication**: uploaded content is hashed with SHA-256 and CRC32. If the same physical file already exists, the service reuses it and increments the reference counter.
- **Soft tenant deletion, orphan cleanup**: tenant file links are soft-deleted first. Physical files can be removed when no active tenant links remain.
- **Self-initializing database**: on startup, the service initializes the SQLite schema and bootstraps the first administrative token when needed.

## Storage Layout

The service uses the `Paths` configuration section:

```json
{
  "Paths": {
    "BasePath": "/var/lib/fsc.storage",
    "LogsPath": "/var/log/fsc.storage",
    "DataPath": "/var/lib/fsc.storage/data"
  }
}
```

By default:

- Windows: `C:\ProgramData\filestorage`
- Linux: `/var/lib/fsc.storage`
- SQLite database: `<BasePath>/storage.db`
- Physical files: `<DataPath>/<sha-prefix>/<sha-prefix>/<sha256>.<ext>`

## Authorization

Requests are authenticated with two headers:

```http
X-Api-Token: <plain-text-api-token>
X-Tenant-Id: <tenant-guid>
```

The service stores only SHA-256 hashes of API tokens. Plain-text tokens are returned only when they are created.

Token permissions:

- `CanRead`: list, inspect, and download files.
- `CanWrite`: upload files and create tenant tokens.
- `CanDelete`: delete tenant file links.
- `IsAdmin`: manage tenants, manage all tokens, and run administrative operations.

On first startup, if no admin token exists, the service creates `admin.conf` next to the application binaries. This file contains the bootstrap administrator name and token:

```json
{
  "Name": "Administrator",
  "Key": "<generated-admin-token>"
}
```

Keep this file and token secure.

## Main API Endpoints

File storage:

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/api/FileStorage/upload` | Upload a file for the current tenant. Uses multipart form data. |
| `GET` | `/api/FileStorage` | List current tenant files. |
| `GET` | `/api/FileStorage/{fileGuid}` | Get file metadata. |
| `GET` | `/api/FileStorage/{fileGuid}/download` | Download file content. |
| `DELETE` | `/api/FileStorage/{fileGuid}` | Delete tenant file link. |
| `POST` | `/api/FileStorage/cleanup-orphans` | Remove orphan physical files. Admin only. |

Administration:

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/admin/tenants` | List tenants. Admin only. |
| `POST` | `/api/admin/tenants` | Create tenant. Admin only. |
| `GET` | `/api/admin/tenant/me` | Get current tenant. |
| `GET` | `/api/admin/tenants/{tenantId}` | Get tenant by id. Admin only. |
| `POST` | `/api/admin/tenants/{tenantId}/disable` | Disable tenant. Admin only. |
| `GET` | `/api/admin/tenants/{tenantId}/tokens` | List tenant tokens. |
| `POST` | `/api/admin/tokens` | Create API token. |
| `POST` | `/api/admin/tokens/{tokenId}/revoke` | Revoke API token. |

## Installation

### Requirements

- .NET SDK or Runtime compatible with `net10.0`
- Write access to the configured `BasePath`, `DataPath`, and `LogsPath`

### Build

```powershell
dotnet restore FSCStorage.slnx
dotnet build FSCStorage.slnx -c Release
```

### Run Locally

```powershell
dotnet run --project SCP.StorageFSC/scp.filestorage.csproj
```

For development builds, OpenAPI is exposed by the ASP.NET Core OpenAPI endpoint.

### Publish

```powershell
dotnet publish SCP.StorageFSC/scp.filestorage.csproj -c Release -o publish
```

Copy the published output to the target server and configure:

- `appsettings.json`
- `appsettings.Windows.json` or `appsettings.Linux.json`
- environment variables, if preferred

The service reads configuration in this order:

1. `appsettings.json`
2. OS-specific settings file
3. environment variables

### Windows MSI HTTP/HTTPS Configuration

The Windows MSI configures a single installed `appsettings.json` during installation.
It does not install separate HTTP/HTTPS appsettings variants. Instead, the installer
asks for one transport mode and updates the `Kestrel:Endpoints` section:

- `HTTP only`: keeps only the HTTP endpoint, usually `http://0.0.0.0:5770`.
- `HTTPS with existing PFX`: adds an HTTPS endpoint, usually `https://0.0.0.0:5771`,
  using the PFX path and password entered during installation.
- `Generate self-signed certificate`: creates a local PFX under the installation
  directory and configures Kestrel to use it.

For production deployments, prefer a DNS name and a certificate issued by a trusted
corporate CA or a public CA such as Let's Encrypt. Self-signed certificates are
best kept for test environments or isolated deployments where the generated
certificate can be explicitly trusted by all clients.

### Run as a Linux Service

The repository includes scripts for installing FSC Storage as a Linux `systemd` service:

- `scripts/install-fsc-storage.sh`: creates the service user and group, prepares application/data/log directories, installs the systemd unit and environment file, reloads systemd, and enables the service.
- `scripts/fsc-storage.service`: systemd unit file that runs the published ASP.NET Core application from `/opt/fsc-storage`.
- `scripts/fsc-storage-env`: environment file used by the service to configure storage and log paths.

Typical Linux deployment flow:

```bash
./scripts/publish-all.sh
sudo ./scripts/install-fsc-storage.sh
sudo cp -r publish/linux-x64/* /opt/fsc-storage/
sudo systemctl start fsc-storage.service
sudo systemctl status fsc-storage.service
```

The installer configures:

- service name: `fsc-storage.service`
- service user/group: `fstore`
- application directory: `/opt/fsc-storage`
- metadata/data directory: `/var/lib/fsc-storage`
- file content directory: `/var/lib/fsc-storage/data`
- log directory: `/var/log/fsc-storage`

After installation, the service can be managed with standard systemd commands:

```bash
sudo systemctl restart fsc-storage.service
sudo systemctl stop fsc-storage.service
sudo journalctl -u fsc-storage.service -f
```

## Basic Usage

Create a tenant:

```bash
curl -X POST "https://localhost:5001/api/admin/tenants" \
  -H "Content-Type: application/json" \
  -H "X-Api-Token: <admin-token>" \
  -H "X-Tenant-Id: <admin-tenant-guid>" \
  -d "{\"name\":\"Customer A\"}"
```

Create a token for a tenant:

```bash
curl -X POST "https://localhost:5001/api/admin/tokens" \
  -H "Content-Type: application/json" \
  -H "X-Api-Token: <admin-token>" \
  -H "X-Tenant-Id: <admin-tenant-guid>" \
  -d "{\"tenantId\":2,\"name\":\"Customer A writer\",\"canRead\":true,\"canWrite\":true,\"canDelete\":true}"
```

Upload a file:

```bash
curl -X POST "https://localhost:5001/api/FileStorage/upload" \
  -H "X-Api-Token: <tenant-token>" \
  -H "X-Tenant-Id: <tenant-guid>" \
  -F "file=@./document.pdf" \
  -F "category=documents" \
  -F "externalKey=doc-001"
```

Download a file:

```bash
curl -L "https://localhost:5001/api/FileStorage/<file-guid>/download" \
  -H "X-Api-Token: <tenant-token>" \
  -H "X-Tenant-Id: <tenant-guid>" \
  -o document.pdf
```

## Testing

Run the test suite:

```powershell
dotnet test FSCStorage.slnx
```

## License

This project is licensed under the MIT License.
See the LICENSE file for details.
