# DashSpec for VS Code / Cursor

Syntax highlighting, snippets, and **LSP** (diagnostics, completion, go-to-definition) for DashSpec DSL files.

> TextMate grammar is **best-effort**. Trust **LSP diagnostics** (same parser as Host).

## Files

| Extension | Role |
|-----------|------|
| `.dashspec` | tab / dashboard modules |
| `.dashdiagram` | diagram presets |
| `.dashpresentation` | chart chrome presets |
| `.dashlayout` | layout boards |
| `.dashcatalog` | catalog entries |
| `.dashpalette` | color palettes |
| `.dashinclude` | include bundles |

## Install (VSIX — обычное расширение)

1. Собрать и установить одной командой:

   ```powershell
   ./scripts/install-vscode-extension.ps1 -Build -Force
   ```

   По умолчанию ставит в **Cursor** (если `cursor` в PATH), иначе в **VS Code**.

   ```powershell
   ./scripts/install-vscode-extension.ps1 -Build -Force -Target Code
   ./scripts/install-vscode-extension.ps1 -Build -Force -Target Cursor
   ```

   Только установить уже собранный VSIX (без rebuild):

   ```powershell
   ./scripts/install-vscode-extension.ps1 -Force
   ```

2. Или вручную: Cursor / VS Code → **Extensions** → **⋯** → **Install from VSIX…**

3. После обновления: **Developer: Reload Window**.

4. Нужен **dotnet** в PATH (для bundled `server/DashSpec.LanguageServer.dll`). Других настроек не требуется.

## Install (dev — F5)

1. Publish language server into extension bundle:

   ```powershell
   ./scripts/publish-language-server.ps1 -Configuration Release
   ```

2. Install npm deps:

   ```powershell
   cd editor/vscode-dashspec
   npm install
   ```

3. In VS Code / Cursor: open `editor/vscode-dashspec` → **F5** (Extension Development Host).

   LSP starts automatically (`dashspec.languageServerEnabled`, default `true`). Bundled server: `server/DashSpec.LanguageServer.dll`.

4. Optional override: `dashspec.languageServerPath` — path to custom `DashSpec.LanguageServer.dll`.

5. Optional CLI validate (redundant with LSP): set `dashspec.hostDll` to `DashSpec.Host.dll` and enable `dashspec.validateOnSave`.

## LSP features

| Feature | Notes |
|---------|--------|
| **Diagnostics** | on open / change / save; line/column from parser offsets |
| **Completion** | diagram / chrome preset ids, `!include` paths, keywords |
| **Go to definition** | `diagram foo`, `chrome use bar`, `!include "path"` |

Command: **DashSpec: Restart Language Server**.

## Commands (CLI fallback)

| Command | Action |
|---------|--------|
| **DashSpec: Validate Current File** | `dotnet DashSpec.Host.dll validate <file>` |
| **DashSpec: Validate All Spec Files in Workspace** | all `*.dashspec`, `*.dashdiagram`, … |

## Snippets

| Prefix | Inserts |
|--------|---------|
| `dstab` | `@tab` module shell |
| `dscard` | structured `card` |
| `dsdiagram` | `@diagram` + `chrome use` |
| `dsfilter` | `filter` bind/show |
| `dschrome` | `chrome use` block |
| `dspresentation` | `@presentation` file |

## Roadmap

- [ ] Hover / signature help
- [ ] Notepad++ UDL export from shared keyword list
