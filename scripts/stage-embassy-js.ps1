param(
    [string]$GuidersJsRoot = "D:\Experiments\PersonalCursorFolder\Financial\software\open\guiders-js"
)

$ErrorActionPreference = "Stop"
$dashSpecRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$bundle = Join-Path $GuidersJsRoot "packages\ccl-input\dist\browser\aiguiders-ccl-input.js"
$target = Join-Path $dashSpecRoot "src\DashSpec.Host\wwwroot\js\aiguiders-ccl-input.js"

if (-not (Test-Path $bundle)) {
    Push-Location $GuidersJsRoot
    try {
        npm run build -w @aiguiders/ccl-input
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path $bundle)) {
    throw "Embassy bundle not found: $bundle"
}

Copy-Item $bundle $target -Force
Write-Host "Staged $target"
