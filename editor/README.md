# DashSpec editor tooling

Authoring plugins for the DashSpec DSL (not Host runtime plugins).

| Tool | Path | Status |
|------|------|--------|
| VS Code / Cursor | [`vscode-dashspec/`](vscode-dashspec/) | v0.2 — syntax, snippets, **LSP** (diagnostics, completion, go-to-definition) |
| Notepad++ | [`notepad++/`](notepad%2B%2B/) | UDL stub (manual sync from VS Code keywords) |

## Grammar drift

TextMate / UDL подсветка — **best-effort**. Источник правды — `DashSpec.Core` parser + `DashSpec.Host validate`.

При изменении DSL: обновить `syntaxes/dashspec.tmLanguage.json` (и опционально Notepad++ UDL). Семантика — **LSP** (`DashSpec.LanguageServer`) и `DashSpec.Host validate`.

## Language server

```powershell
./scripts/publish-language-server.ps1 -Configuration Release
```

Копирует `DashSpec.LanguageServer.dll` (+ deps) в `editor/vscode-dashspec/server/`. Extension поднимает LSP через `dotnet` + bundled DLL.

## Validate (CLI fallback)

```powershell
dotnet run --project src/DashSpec.Host -- validate path/to/file.dashspec
```

VS Code extension вызывает тот же entry point через `dashspec.hostDll` (путь к собранному `DashSpec.Host.dll`).
