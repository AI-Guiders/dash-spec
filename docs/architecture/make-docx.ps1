#Requires -Version 7
<#
.SYNOPSIS
    Собирает DOCX из docs/ARCHITECTURE_RU.md: pandoc + нативное оглавление Word.

.DESCRIPTION
    1. pandoc превращает Markdown в DOCX, вставляя изображения диаграмм (PNG).
       Нумерация разделов в Markdown уже проставлена вручную, поэтому
       --number-sections НЕ используется (иначе номера удвоятся).
    2. Microsoft Word (COM) добавляет настоящее обновляемое оглавление после
       абзаца «Содержание» и сохраняет документ: оглавление сразу содержит
       заголовки и номера страниц, а не пустое поле.

    Если Word не установлен, документ всё равно создаётся — без оглавления.

.EXAMPLE
    pwsh -File docs/architecture/make-docx.ps1
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
if (-not $Source) { $Source = Join-Path $here '..\ARCHITECTURE_RU.md' }
if (-not $Output) { $Output = Join-Path $here 'DashSpec-Architecture-RU.docx' }
$docsRoot = (Resolve-Path (Join-Path $here '..')).Path

# --- 1. pandoc --------------------------------------------------------------
$pandoc = (Get-Command pandoc -ErrorAction SilentlyContinue).Source
if (-not $pandoc) {
    $fallback = Join-Path $env:ProgramFiles 'Pandoc\pandoc.exe'
    if (Test-Path $fallback) { $pandoc = $fallback }
}
if (-not $pandoc) { throw 'Не найден pandoc. Установка: winget install JohnMacFarlane.Pandoc' }

Write-Host "pandoc: $pandoc"
& $pandoc $Source -o $Output --resource-path=$docsRoot --toc-depth=3
if ($LASTEXITCODE -ne 0) { throw "pandoc вернул код $LASTEXITCODE" }

# --- 2. оглавление Word -----------------------------------------------------
try {
    $word = New-Object -ComObject Word.Application
}
catch {
    Write-Warning 'Microsoft Word недоступен — оглавление не добавлено.'
    exit 0
}

$word.Visible = $false
$word.DisplayAlerts = 0
$fullPath = (Resolve-Path $Output).Path
$document = $word.Documents.Open($fullPath)

try {
    $anchor = $document.Content
    $anchor.Find.ClearFormatting()
    $anchor.Find.Text = 'Содержание'
    if ($anchor.Find.Execute()) {
        $insertAt = $document.Range($anchor.End, $anchor.End)
        if ($document.TablesOfContents.Count -eq 0) {
            $null = $document.TablesOfContents.Add($insertAt, $true, 1, 3, $false, '', $true, $true)
        }
        $document.TablesOfContents.Item(1).Update()
        Write-Host 'Оглавление добавлено и обновлено.'
    }
    else {
        Write-Warning 'Абзац «Содержание» не найден — оглавление не добавлено.'
    }

    $document.Fields.Update() | Out-Null
    $document.Save()
}
finally {
    $document.Close(0)
    $word.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($word)
}

Write-Host "Готово: $fullPath ($([math]::Round((Get-Item $Output).Length / 1KB)) КБ)"
