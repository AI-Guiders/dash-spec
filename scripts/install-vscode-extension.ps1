param(
    [ValidateSet('Auto', 'Cursor', 'Code')]
    [string]$Target = 'Auto',

    [switch]$Build,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$VsixPath,

    [switch]$Force,

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$extensionDir = Join-Path $RepoRoot 'editor/vscode-dashspec'
$packageScript = Join-Path $RepoRoot 'scripts/package-vscode-extension.ps1'

function Resolve-Cli {
    param([string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        return $null
    }

    return $command.Source
}

function Install-Vsix {
    param(
        [string]$CliPath,
        [string]$Label,
        [string]$Vsix
    )

    $args = @('--install-extension', $Vsix)
    if ($Force) {
        $args += '--force'
    }

    Write-Host "Installing into $Label via $CliPath ..."
    & $CliPath @args
    if ($LASTEXITCODE -ne 0) {
        throw "$Label install failed with exit code $LASTEXITCODE"
    }

    Write-Host "OK: $Label"
}

if ($Build) {
    Write-Host 'Building VSIX...'
    & $packageScript -Configuration $Configuration -RepoRoot $RepoRoot
}

if ([string]::IsNullOrWhiteSpace($VsixPath)) {
    $vsix = Get-ChildItem -Path $extensionDir -Filter '*.vsix' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $vsix) {
        throw "No VSIX in $extensionDir. Run with -Build or ./scripts/package-vscode-extension.ps1 first."
    }

    $VsixPath = $vsix.FullName
}
elseif (-not (Test-Path -LiteralPath $VsixPath)) {
    throw "VSIX not found: $VsixPath"
}

$VsixPath = (Resolve-Path -LiteralPath $VsixPath).Path
Write-Host "VSIX: $VsixPath"

$cursorCli = Resolve-Cli 'cursor'
$codeCli = Resolve-Cli 'code'

switch ($Target) {
    'Cursor' {
        if (-not $cursorCli) {
            throw 'cursor CLI not found in PATH.'
        }

        Install-Vsix -CliPath $cursorCli -Label 'Cursor' -Vsix $VsixPath
    }
    'Code' {
        if (-not $codeCli) {
            throw 'code CLI not found in PATH.'
        }

        Install-Vsix -CliPath $codeCli -Label 'VS Code' -Vsix $VsixPath
    }
    default {
        if ($cursorCli) {
            Install-Vsix -CliPath $cursorCli -Label 'Cursor' -Vsix $VsixPath
        }
        elseif ($codeCli) {
            Install-Vsix -CliPath $codeCli -Label 'VS Code' -Vsix $VsixPath
        }
        else {
            throw 'Neither cursor nor code CLI found in PATH.'
        }
    }
}

Write-Host 'Reload the editor window (Developer: Reload Window) if the extension was already running.'
