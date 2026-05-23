param(
    [int]$MaxLines = 700,
    [string]$BatchName,
    [switch]$AllowPartialFileScope
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$suggestScript = Join-Path $scriptRoot "suggest-change-batches.ps1"
$verifyScript = Join-Path $scriptRoot "verify-worktree.ps1"

Write-Host "Verifying staged batch scope..."
$suggestArguments = @{
    Scope = "Staged"
    RequireSingleBatch = $true
    OutputMarkdown = $true
}
if (-not [string]::IsNullOrWhiteSpace($BatchName)) {
    $suggestArguments.BatchName = $BatchName
}

$suggestOutput = @(& $suggestScript @suggestArguments *>&1)
$suggestExitCode = $LASTEXITCODE
$suggestOutput | ForEach-Object { Write-Host $_ }
if ($suggestExitCode -ne 0) {
    exit $suggestExitCode
}

$hasPartialFileScope = @($suggestOutput | Where-Object { $_ -match "^- Staged boundary: incomplete" }).Count -gt 0
if ($hasPartialFileScope -and -not $AllowPartialFileScope) {
    Write-Host ""
    Write-Host "ERROR: Staged batch has unstaged changes in the same file scope."
    Write-Host "Stage the remaining changes, or rerun with -AllowPartialFileScope and record the deliberate hunk split."
    exit 1
}

Write-Host ""
Write-Host "Verifying staged worktree files..."
& $verifyScript -Scope Staged -MaxLines $MaxLines
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Staged candidate summary:"
$stagedFileCount = ($suggestOutput | Where-Object { $_ -match "^Changed file count: " } | Select-Object -First 1) -replace "^Changed file count: ", ""
$detectedBatchName = ($suggestOutput | Where-Object { $_ -match "^- Batch name: " } | Select-Object -First 1) -replace "^- Batch name: ", ""
Write-Host "  Staged file count: $stagedFileCount"
Write-Host "  Batch name: $detectedBatchName"
if (-not [string]::IsNullOrWhiteSpace($BatchName)) {
    Write-Host "  Expected batch filter: $BatchName"
}
Write-Host "  Verification: single batch and staged worktree checks passed"
if ($AllowPartialFileScope) {
    Write-Host "  Partial file scope: explicitly allowed; record the hunk split rationale"
}
Write-Host "  Next: record GitNexus impact, detect_changes, scenario evidence, verification results, and rollback plan before commit"

Write-Host ""
Write-Host "Staged batch verification OK"
