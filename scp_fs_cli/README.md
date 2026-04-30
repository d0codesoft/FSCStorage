# scp_fs_cli

Console client for uploading files to `scp.filestorage`.

## What it does

- reads server settings from `client.config`
- uploads files smaller than `200 MB` through `POST /api/file/upload`
- uploads larger files through multipart API
- retries failed multipart parts
- uses HTTPS without certificate validation
- shows progress with percent, current speed, and ETA
- computes full-file SHA-256 before `multipart/init`
- supports checking multipart status by `uploadId`

## client.config

Place `client.config` next to the built executable, or pass a custom path with `--config`.

```json
{
  "serviceUrl": "https://localhost:5770",
  "apiToken": "put-your-api-token-here",
  "tenantId": "00000000-0000-0000-0000-000000000000"
}
```

Fields:

- `serviceUrl` - base URL of `scp.filestorage`
- `apiToken` - API token for the request
- `tenantId` - tenant id used by the service and required by `multipart/init`

## Commands

### Upload

```powershell
dotnet run --project scp_fs_cli -- upload "C:\data\report.pdf"
```

With options:

```powershell
dotnet run --project scp_fs_cli -- upload "C:\data\big.zip" --threads 4 --retries 5 --category docs --external-key order-123
```

Custom config:

```powershell
dotnet run --project scp_fs_cli -- upload "C:\data\big.zip" --config "C:\cfg\client.config"
```

### Multipart status

```powershell
dotnet run --project scp_fs_cli -- status "4f9ed5f4-8fe2-4b70-b5b6-bcb7b7040536"
```

With custom config:

```powershell
dotnet run --project scp_fs_cli -- status "4f9ed5f4-8fe2-4b70-b5b6-bcb7b7040536" --config "C:\cfg\client.config"
```

## Build

```powershell
dotnet build scp_fs_cli/scp_fs_cli.csproj
```

## Publish

Windows x64:

```powershell
dotnet publish scp_fs_cli/scp_fs_cli.csproj -c Release -r win-x64 --self-contained false -o publish/scp_fs_cli/win-x64
```

Linux x64:

```powershell
dotnet publish scp_fs_cli/scp_fs_cli.csproj -c Release -r linux-x64 --self-contained false -o publish/scp_fs_cli/linux-x64
```

After publish, copy `client.config` to the output directory and fill in real values.
