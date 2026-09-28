# DashSpec Host E2E (Playwright)

Browser smoke tests catch **JavaScript parse/runtime errors**, failed script loads, and (when SQL demo is available) matrix canvas paint.

## Prerequisites

- **Node.js** on `PATH` (build runs `node --check` on Host `wwwroot/js` and `wwwroot/lib/**/*.js`, plus viz plugin JS).
- **Playwright Chromium** (once per machine):

```powershell
cd tests\DashSpec.Host.E2E
dotnet build
pwsh bin\Debug\net10.0\playwright.ps1 install chromium
```

Chart.js and the boxplot plugin are **vendored** under `src/DashSpec.Host/wwwroot/lib/chartjs` — no CDN or outbound HTTPS required at runtime or in E2E.

## Run

```powershell
dotnet test tests\DashSpec.Host.E2E\DashSpec.Host.E2E.csproj
```

Tests use `WebApplicationFactory` with content root `src/DashSpec.Host` (same `dash-spec.toml` → `samples/demo`).
