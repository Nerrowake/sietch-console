# Tests

Sietch Console does not currently have a dedicated automated test project in this
repository.

Until test projects are added, changes should be verified with:

- `dotnet restore`
- `dotnet build`
- manual smoke testing of affected views
- setup wizard checks when installation behavior changes
- release packaging checks when installer behavior changes

Future automated tests should focus on Core services, Data repositories,
configuration parsing, diagnostics checks, and safety-critical workflows.
