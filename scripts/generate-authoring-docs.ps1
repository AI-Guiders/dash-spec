param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$core = Join-Path $RepoRoot 'src/DashSpec.Core/DashSpec.Core.csproj'
$docGen = Join-Path $RepoRoot 'src/DashSpec.DocGen/DashSpec.DocGen.csproj'

Write-Host 'Building DashSpec.Core (Release)...'
dotnet build $core -c Release | Out-Null

Write-Host 'Generating docs/authoring/generated/AUTHORING.md...'
dotnet run --project $docGen -- $RepoRoot
