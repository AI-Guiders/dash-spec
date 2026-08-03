param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $RepoRoot 'src/DashSpec.LanguageServer/DashSpec.LanguageServer.csproj'
$outDir = Join-Path $RepoRoot "src/DashSpec.LanguageServer/bin/$Configuration/net10.0"
$serverDir = Join-Path $RepoRoot 'editor/vscode-dashspec/server'

Write-Host "Building DashSpec.LanguageServer ($Configuration)..."
dotnet publish $project -c $Configuration -o $outDir --no-self-contained

if (-not (Test-Path $outDir)) {
    throw "Publish output not found: $outDir"
}

Write-Host "Copying to $serverDir ..."
if (Test-Path $serverDir) {
    Remove-Item -Recurse -Force $serverDir
}
New-Item -ItemType Directory -Path $serverDir | Out-Null
Copy-Item -Path (Join-Path $outDir '*') -Destination $serverDir -Recurse -Force

Write-Host "Done. Server DLL: $(Join-Path $serverDir 'DashSpec.LanguageServer.dll')"
