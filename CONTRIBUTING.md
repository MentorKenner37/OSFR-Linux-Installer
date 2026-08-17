# Contributing

Contributions are welcome. Keep changes focused on the Linux x64 installer/launcher path and avoid reintroducing platform-specific build systems that are not used by this repository.

## Before committing

Run the installer safety smoke tests and build the installer/launcher paths you changed.

```bash
dotnet run --project src/Installer.SmokeTests/Installer.SmokeTests.csproj -c Release
```

## Commit email privacy

If you do not want a personal email address stored in public Git commit metadata, configure this repository to use your GitHub noreply address before committing:

```bash
git config user.name "MentorKenner"
git config user.email "315211878+MentorKenner37@users.noreply.github.com"
```

The repository also includes a `.mailmap` that normalizes older contributor attribution where Git tools support mailmaps. A mailmap does not erase raw historical commit metadata, so contributors should still configure a noreply address before making new commits.
