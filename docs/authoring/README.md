# DashSpec — авторинг

Справочник для авторов `.dashspec` и модулей.

## Канон

| Документ | Роль |
|----------|------|
| [generated/AUTHORING.md](generated/AUTHORING.md) | **Сгенерировано** из `AuthoringCatalog` (XML-doc в `DashSpec.Core`) |
| [design/DASHSPEC-ADR-0024-document-authoring-layers.md](../design/DASHSPEC-ADR-0024-document-authoring-layers.md) | ADR: слои document grammar |
| [editor/vscode-dashspec/README.md](../editor/vscode-dashspec/README.md) | VSIX / LSP |

## Обновить справочник

```powershell
cd D:\Experiments\PersonalCursorFolder\Financial\software\open\dash-spec
dotnet build src/DashSpec.Core/DashSpec.Core.csproj -c Release
dotnet run --project src/DashSpec.DocGen -- .
```

Править тексты в `src/DashSpec.Core/Authoring/AuthoringCatalog.cs` (XML `///` на nested types).
Парсер-специфичные детали — в `///` на классах в `DashSpec.Core/Parsing/*.cs` (подтягиваются в IDE, при необходимости дублируй кратко в catalog).

## LUS

Примеры: `URSA.LicenseUsage/docs/dashspec/` (soak shell + tab modules).
