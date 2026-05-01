# FSC Storage WiX Installer

This folder contains a WiX Toolset MSI project for installing FSC Storage as a Windows service.

## Prerequisites

- .NET SDK compatible with the service target framework.
- WiX Toolset SDK restored by `dotnet build`.

If you also want the WiX command-line tool available globally:

```powershell
dotnet tool install --global wix
```

## Build

From the repository root:

```powershell
dotnet build .\installer\FSCStorage.Installer.wixproj -c Release
```

The installer project publishes the service and admin CLI for `win-x64` before building the MSI. The default publish mode is self-contained, so the target server does not need a separate .NET runtime installation.

The MSI output is written under:

```text
bin\x64\Release\en-US\
bin\x64\Release\uk-UA\
```

The installer UI is localized in English and Ukrainian and uses the standard flow:

- welcome dialog with FSC Storage description and product artwork;
- license agreement dialog using `assets\license.rtf`;
- installation directory and confirmation dialogs.

## Service Details

- Install directory: `C:\Program Files\FSCStorage`
- Service name: `fsc-storage`
- Display name: `FSC Storage Service`
- Service account: `LocalSystem`
- Startup type: automatic
- Failure policy: restart on first and second failure

The service uses `appsettings.Windows.json` from the published application, which stores data and logs under `C:\ProgramData\filestorage` by default.

## Useful Commands

Install silently:

```powershell
msiexec /i .\bin\x64\Release\en-US\FSCStorage.msi /qn /l*v install.log
```

Uninstall silently:

```powershell
msiexec /x .\bin\x64\Release\en-US\FSCStorage.msi /qn /l*v uninstall.log
```
