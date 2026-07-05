# One-off: legacy dashboard "T" { -> @dashboard t { report "T" {
$testDir = Join-Path $PSScriptRoot "..\tests\DashSpec.Core.Tests"
$files = Get-ChildItem $testDir -Filter "*.cs" -Recurse

foreach ($file in $files) {
    if ($file.Name -eq "BlockSpecTestHelper.cs") { continue }

    $content = [IO.File]::ReadAllText($file.FullName)
    $original = $content

    # @dashboard id\n dashboard "Title" { -> @dashboard id {\n  report "Title" {
    $content = [regex]::Replace(
        $content,
        '@dashboard\s+(\w+)\s*\r?\n\s*dashboard\s+"([^"]+)"\s*\{',
        '@dashboard $1 {`n  report "$2" {')

    # @tab id\n (no brace) legacy tab modules -> skip (manual)

    # Flat @runtime before @dashboard -> runtime block inside (simple case)
    $content = [regex]::Replace(
        $content,
        '@runtime\s+"([^"]+)"\s*\r?\n\s*@sqldialect\s+\w+\s*\r?\n\s*@dashboard',
        '@dashboard')

    if ($content -ne $original) {
        # Close extra brace before final dashboard close: heuristic add one } before last }
        # Only if we opened report { - count braces in changed regions is hard; run tests after
        [IO.File]::WriteAllText($file.FullName, $content)
        Write-Host "Updated $($file.Name)"
    }
}

Write-Host "Done. Run dotnet test and fix remaining legacy by hand or BlockSpecTestHelper."
