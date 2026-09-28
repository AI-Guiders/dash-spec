# DashSpec Host E2E (Playwright)

Browser smoke tests catch **JavaScript parse/runtime errors**, failed script loads, and (when SQL demo is available) matrix canvas paint.

## Prerequisites

- **Node.js** on `PATH` (build runs `node --check` on `wwwroot/js/*.js` in Host and viz plugin).
- **Playwright Chromium** (once per machine):

```powershell
cd tests\DashSpec.Host.E2E
dotnet build
pwsh bin\Debug\net10.0\playwright.ps1 install chromium
```

E2E tests need outbound HTTPS for Chart.js CDN scripts on the host shell.

## Run

```powershell
dotnet test tests\DashSpec.Host.E2E\DashSpec.Host.E2E.csproj
```

Tests use `WebApplicationFactory` with content root `src/DashSpec.Host` (same `dash-spec.toml` → `samples/demo`).
