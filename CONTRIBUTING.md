# Contributing

Thanks for helping improve Sanctuary Linux Installer.

## Before opening a change

- Search existing issues and pull requests.
- Use the bug-report form for reproducible compatibility problems.
- Never include passwords, session identifiers, access tokens, or unredacted personal paths in public logs.
- Keep the installer Linux-first, Proton-based, and conservative about modifying or deleting user data.

## Development

Install the .NET SDK selected by `OSFR-Launcher/src/global.json`, then run:

```bash
dotnet restore tests/Installer.UnitTests/Installer.UnitTests.csproj
dotnet test tests/Installer.UnitTests/Installer.UnitTests.csproj -c Release --no-restore
dotnet run --project src/Installer.SmokeTests/Installer.SmokeTests.csproj -c Release
dotnet run --project src/Launcher.SmokeTests/Launcher.SmokeTests.csproj -c Release
```

Changes to filesystem operations, downloads, manifests, process launching, credentials, Proton selection, or release automation must include regression coverage for successful and rejected behavior.

## Pull requests

Explain the user-visible change, safety impact, validation performed, and remaining compatibility uncertainty. All required CI checks must pass. Versioned releases are built only from matching immutable tags.
