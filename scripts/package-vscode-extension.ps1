param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$extensionDir = Join-Path $RepoRoot 'editor/vscode-dashspec'
$publishScript = Join-Path $RepoRoot 'scripts/publish-language-server.ps1'

Write-Host 'Step 1/3: publish language server...'
& $publishScript -Configuration $Configuration -RepoRoot $RepoRoot

Write-Host 'Step 2/3: npm install...'
Push-Location $extensionDir
try {
    npm install --omit=dev
    if ($LASTEXITCODE -ne 0) { throw "npm install failed with exit code $LASTEXITCODE" }

    Write-Host 'Step 3/3: vsce package...'
    npx --yes @vscode/vsce@latest package --allow-missing-repository
    if ($LASTEXITCODE -ne 0) { throw "vsce package failed with exit code $LASTEXITCODE" }

    $vsix = Get-ChildItem -Path $extensionDir -Filter '*.vsix' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $vsix) {
        throw 'VSIX file was not created.'
    }

    Write-Host "Done: $($vsix.FullName)"
}
finally {
    Pop-Location
}
