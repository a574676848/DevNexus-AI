param(
    [ValidateSet("All", "Staged", "Unstaged")]
    [string]$Scope = "All",
    [int]$MaxLines = 700
)

$ErrorActionPreference = "Stop"

$TextExtensions = @(".cs", ".razor", ".md", ".json", ".css", ".js", ".ts", ".xaml", ".ps1")

function Get-TrackedChangedFileList {
    return @(git -c core.autocrlf=false diff --name-only) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
}

function Get-StagedChangedFileList {
    return @(git -c core.autocrlf=false diff --cached --name-only) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
}

function Get-UntrackedFileList {
    return @(git -c core.autocrlf=false ls-files --others --exclude-standard) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
}

function Get-ChangedFileList {
    if ($Scope -eq "Staged") {
        return @(Get-StagedChangedFileList) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    }

    if ($Scope -eq "Unstaged") {
        $unstagedTracked = Get-TrackedChangedFileList
        $untracked = Get-UntrackedFileList

        return @($unstagedTracked; $untracked) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    }

    $staged = Get-StagedChangedFileList
    $unstagedTracked = Get-TrackedChangedFileList
    $untracked = Get-UntrackedFileList

    return @($staged; $unstagedTracked; $untracked) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
}

function Get-UntrackedFilesForScope {
    if ($Scope -eq "Staged") {
        return @()
    }

    return @(Get-UntrackedFileList)
}

function Test-IsTextFile {
    param([string]$File)

    return $TextExtensions -contains [System.IO.Path]::GetExtension($File)
}

function Test-Utf8Bom {
    param([string[]]$Files)

    $failed = @()
    foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }

        $bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $file))
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            $failed += $file
        }
    }

    return $failed
}

function Test-LineLimit {
    param(
        [string[]]$Files,
        [int]$Limit
    )

    $failed = @()

    foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }

        if (-not (Test-IsTextFile -File $file)) {
            continue
        }

        $lineCount = (Get-Content -LiteralPath $file | Measure-Object -Line).Lines
        if ($lineCount -gt $Limit) {
            $failed += "$file`t$lineCount"
        }
    }

    return $failed
}

function Test-TrailingWhitespace {
    param([string[]]$Files)

    $failed = @()
    foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }

        if (-not (Test-IsTextFile -File $file)) {
            continue
        }

        $lineNumber = 0
        foreach ($line in Get-Content -LiteralPath $file) {
            $lineNumber++
            if ($line -match "[ `t]+$") {
                $failed += "$file`:$lineNumber"
            }
        }
    }

    return $failed
}

function Test-ConflictMarkers {
    param([string[]]$Files)

    $failed = @()
    foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }

        if (-not (Test-IsTextFile -File $file)) {
            continue
        }

        $lineNumber = 0
        foreach ($line in Get-Content -LiteralPath $file) {
            $lineNumber++
            if ($line -match "^(<{7}|={7}|>{7})(\s|$)") {
                $failed += "$file`:$lineNumber"
            }
        }
    }

    return $failed
}

function Invoke-GitDiffCheck {
    param([string[]]$Arguments)

    $diffCheckOutput = @(git -c core.autocrlf=false -c core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol @Arguments 2>&1)
    $diffCheckExitCode = $LASTEXITCODE
    $filteredDiffCheckOutput = $diffCheckOutput |
        Where-Object { $_ -notmatch "^warning: in the working copy of '.+', LF will be replaced by CRLF the next time Git touches it$" }
    if ($filteredDiffCheckOutput.Count -gt 0) {
        $filteredDiffCheckOutput | ForEach-Object { Write-Host $_ }
    }
    if ($diffCheckExitCode -ne 0) {
        exit $diffCheckExitCode
    }
}

Write-Host "Running git diff --check..."
if ($Scope -eq "All" -or $Scope -eq "Unstaged") {
    Invoke-GitDiffCheck -Arguments @("diff", "--check")
}

if ($Scope -eq "All" -or $Scope -eq "Staged") {
    Invoke-GitDiffCheck -Arguments @("diff", "--cached", "--check")
}

$changedFiles = @(Get-ChangedFileList)
$untrackedFiles = @(Get-UntrackedFilesForScope)
Write-Host "Scope: $Scope"
Write-Host "Changed file count: $($changedFiles.Count)"

$bomFailures = @(Test-Utf8Bom -Files $changedFiles)
if ($bomFailures.Count -gt 0) {
    Write-Error ("UTF-8 BOM detected:`n" + ($bomFailures -join "`n"))
    exit 1
}

$lineFailures = @(Test-LineLimit -Files $changedFiles -Limit $MaxLines)
if ($lineFailures.Count -gt 0) {
    Write-Error ("Line limit exceeded:`n" + ($lineFailures -join "`n"))
    exit 1
}

$trailingWhitespaceFailures = @(Test-TrailingWhitespace -Files $untrackedFiles)
if ($trailingWhitespaceFailures.Count -gt 0) {
    Write-Error ("Trailing whitespace detected in untracked files:`n" + ($trailingWhitespaceFailures -join "`n"))
    exit 1
}

$conflictMarkerFailures = @(Test-ConflictMarkers -Files $changedFiles)
if ($conflictMarkerFailures.Count -gt 0) {
    Write-Error ("Conflict markers detected:`n" + ($conflictMarkerFailures -join "`n"))
    exit 1
}

Write-Host "Worktree verification OK"
