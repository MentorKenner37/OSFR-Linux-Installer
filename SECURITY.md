# Security policy

## Supported versions

Security fixes are provided for the newest published release or prerelease.

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability that could expose credentials, allow unintended file deletion or replacement, escape an installation directory, or execute untrusted input.

Use GitHub's **Report a vulnerability** private reporting feature on the repository Security page when it is available. If the private form is unavailable, open a public issue containing no exploit details and ask the maintainer for a private contact channel. Include the affected version, Linux distribution, reproduction steps, redacted relevant paths, and potential impact only in the private report.

Ordinary compatibility problems that do not expose sensitive information can use the public bug-report form.

## Scope

High-priority areas include ownership and uninstall authorization, archive/path traversal, symbolic links, transaction recovery, release integrity, credential storage, remote manifests, and client-download verification.
