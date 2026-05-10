# fsc.mob.client Android development plan

## Current state

- `fsc.mob.client` is a .NET MAUI project already included in `FSCStorage.slnx`.
- Android workload is installed and the project builds for `net10.0-android`.
- Android SDK exists at `%LOCALAPPDATA%\Android\Sdk`.
- `adb` is available by full path, but not currently in `PATH`.
- No Android emulator AVD is configured yet.
- The FSC Storage server is running locally on `http://127.0.0.1:5770`.

## Goal

Build `fsc.mob.client` as an Android administration client for FSC Storage:

- Sign in to a server with admin credentials or API token.
- Manage users.
- Manage tenants.
- Manage API keys.
- View storage statistics and background tasks.
- Trigger maintenance tasks such as consistency check and deleted-tenant cleanup.

## Architecture

Use a small MAUI MVVM structure:

- `Api`: typed HTTP clients, DTOs, error handling.
- `Auth`: login/session state, secure token storage.
- `Models`: mobile view models/DTO copies for API payloads.
- `Pages`: XAML pages for each workflow.
- `ViewModels`: observable page state and commands.
- `Services`: navigation, alerts, settings, connectivity.

Recommended packages:

- `CommunityToolkit.Mvvm` for `ObservableObject`, commands, and simple MVVM.
- `Microsoft.Extensions.Http` for typed `HttpClient`.
- `Microsoft.Maui.Storage.SecureStorage` for storing server URL and token.

## API coverage

Base UI API endpoints already exposed by the server:

- `GET ui-api/storage/statistics`
- `GET ui-api/users`
- `POST ui-api/users`
- `PUT ui-api/users/{userId}`
- `POST ui-api/users/{userId}/block`
- `POST ui-api/users/{userId}/unblock`
- `DELETE ui-api/users/{userId}`
- `GET ui-api/tenants`
- `GET ui-api/tenants/{tenantId}`
- `POST ui-api/tenants`
- `PUT ui-api/tenants/{tenantId}`
- `DELETE ui-api/tenants/{tenantId}`
- `GET ui-api/tenants/{tenantId}/api-tokens`
- `POST ui-api/api-tokens`
- `PUT ui-api/api-tokens/{tokenId}`
- `DELETE ui-api/api-tokens/{tokenId}`
- `GET ui-api/storage/tasks/active`
- `GET ui-api/storage/tasks/completed`
- `POST ui-api/storage/check-consistency`
- `POST ui-api/storage/cleanup-deleted-tenants`

The mobile client must never log plain API tokens or generated admin credentials.

## Screens

1. Connection setup
   - Server URL input.
   - API token or login fields.
   - Connection test.
   - Save connection profile to secure storage.

2. Dashboard
   - Total files, total size, active tenants, largest files.
   - Quick refresh.
   - Status if server is unreachable or token is invalid.

3. Users
   - User list with status, role, tenants count, API keys count.
   - Create/edit user.
   - Block/unblock user.
   - Delete user with confirmation that tenants, keys, and files are affected.

4. User details
   - User profile.
   - User tenants.
   - User API keys grouped by tenant.
   - Quick actions: add tenant, add API key.

5. Tenants
   - Tenant list.
   - Tenant details.
   - Create/edit/delete tenant.
   - API keys for selected tenant.

6. API keys
   - Create API key with permissions.
   - Edit active state and permissions.
   - Delete key.
   - One-time display of generated token.

7. Maintenance
   - Active/completed background tasks.
   - Trigger consistency check.
   - Trigger deleted-tenant file cleanup.

## Security

- Store API token in `SecureStorage`, never in plain preferences.
- Use `HttpClient.DefaultRequestHeaders` only at request time.
- Do not write tokens to logs, exceptions, analytics, or UI history.
- Show generated API tokens once and then discard them from memory when leaving the page.
- Prefer HTTP only for local development; production should use HTTPS.
- For Android emulator, use `http://10.0.2.2:<port>` to reach the host machine.

## Development phases

### Phase 1: Foundation

- Replace MAUI template page with Shell navigation.
- Add `CommunityToolkit.Mvvm`.
- Add `FscAdminApiClient` typed service in the mobile project.
- Add DTOs matching WebUI `AdminApiClient` models.
- Add connection settings and secure token storage.
- Add dashboard page with statistics.

### Phase 2: Users and tenants

- Implement users list and details.
- Implement create/edit/block/delete users.
- Implement tenants list and tenant details.
- Add create/edit/delete tenant flows.

### Phase 3: API keys

- Implement tenant API key list.
- Implement create/edit/delete API key.
- Add one-time generated token display.
- Add permission toggles.

### Phase 4: Maintenance and polish

- Add background tasks screen.
- Add consistency check and deleted-tenant cleanup actions.
- Add refresh, empty, loading, unauthorized, and offline states.
- Add Android adaptive icon/splash aligned with FSC Storage.

### Phase 5: Verification

- Build Android debug target.
- Run against local server.
- Test emulator URL `http://10.0.2.2:5770`.
- Test physical-device URL using the workstation LAN IP.
- Add focused tests for API client serialization and ViewModel state transitions where practical.

## Local development commands

Build Android target:

```powershell
dotnet build fsc.mob.client\fsc.mob.client.csproj -f net10.0-android -c Debug
```

Start server:

```powershell
dotnet run --project SCP.StorageFSC\scp.filestorage.csproj
```

Check attached Android devices:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" devices
```

List Android virtual devices:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -list-avds
```

## Open setup tasks

- Add Android SDK `platform-tools` and `emulator` directories to `PATH`.
- Install at least one Android system image.
- Create an Android Virtual Device through Visual Studio Android Device Manager or Android Studio.
- Decide whether mobile auth should use API token only, username/password, or both.
