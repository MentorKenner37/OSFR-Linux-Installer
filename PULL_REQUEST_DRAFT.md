Draft PR: Add xUnit unit tests + run existing smoke tests

This draft adds a focused xUnit test project and an accompanying workflow for the feature branch.

What it contains:
- tests/Installer.UnitTests/ (xUnit tests and TempDirFixture)
- .github/workflows/run-unit-and-smoke-tests.yml to run the unit suite + existing smoke tests on this branch

Notes:
- No production behavior changed except narrow InternalsVisibleTo in AssemblyInfo to allow tests to exercise internals.
- Tests use real temporary directories; avoid mocking filesystem operations.

Trigger: CI run requested at 2026-08-17T19:58:00Z UTC

Run instructions and expectations are in the PR description.
