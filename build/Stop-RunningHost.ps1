# Stops a running DashSpec.Host instance that locks this build's output directory.
# Called by DashSpec.Host.csproj before Build (MSB3027 "file is locked" class):
# a rebuild while the host is up used to fail on retry-copy of the DLLs.
# Path-filtered: only the instance launched from THIS TargetDir dies — other
# checkouts/builds keep running. Paths resolve to their final form (junctions
# like PersonalCursorFolder -> "Personal Cursor Folder" must not break the match).
param(
    [Parameter(Mandatory = $true)]
    [string]$HostExePath
)

$ErrorActionPreference = 'SilentlyContinue'

Add-Type -Namespace Win32 -Name PathUtils -MemberDefinition @'
[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
public static extern uint GetFinalPathNameByHandleW(
    Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
    System.Text.StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);
'@

function Get-RealPath([string]$path) {
    try {
        $fs = [System.IO.File]::Open($path, 'Open', 'Read', 'ReadWrite, Delete')
        try {
            $sb = New-Object System.Text.StringBuilder 1024
            [Win32.PathUtils]::GetFinalPathNameByHandleW($fs.SafeFileHandle, $sb, 1024, 0) | Out-Null
            $final = $sb.ToString()
            if ($final.StartsWith('\\?\')) { $final = $final.Substring(4) }
            if ($final) { return $final }
        } finally { $fs.Dispose() }
    } catch { }
    return [System.IO.Path]::GetFullPath($path)
}

$target = Get-RealPath $HostExePath

Get-Process -Name 'DashSpec.Host' |
    Where-Object { $_.Path -and ((Get-RealPath $_.Path) -ieq $target) } |
    ForEach-Object {
        Write-Host "Stopping DashSpec.Host (pid $($_.Id)) holding $target"
        Stop-Process -Id $_.Id -Force
    }
