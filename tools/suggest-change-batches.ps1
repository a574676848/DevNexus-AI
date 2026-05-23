param(
    [ValidateSet("All", "Staged", "Unstaged")]
    [string]$Scope = "All",
    [string]$BatchName,
    [switch]$ShowGitAddCommands,
    [switch]$OutputMarkdown,
    [switch]$RequireSingleBatch
)

$ErrorActionPreference = "Stop"

$CoreTestsCommand = "dotnet test .\src\tests\DevNexus.Core.Tests\DevNexus.Core.Tests.csproj --no-build"
$WorktreeGateCommand = ".\tools\verify-worktree.ps1 -Scope All"
$StagedBatchGateCommand = ".\tools\verify-staged-batch.ps1"

$BatchDefinitions = @(
    [pscustomobject]@{
        Name = "Agent Loop stability"
        Description = "Tool validation, deterministic recovery, stop or continuation prompts, and chat runtime recovery."
        Patterns = @(
            "src/backend/DevNexus.Core/Services/Chat/AgentLoopExecutor.cs",
            "src/backend/DevNexus.Core/Services/Chat/AgentRuntimeRecoveryPromptBuilder.cs",
            "src/tests/DevNexus.Core.Tests/Services/Chat/AgentLoopExecutorTests.cs",
            "src/tests/DevNexus.Core.Tests/Services/Chat/ChatAgentLoopCoordinatorTests.cs",
            "docs/02-architecture/06-agent-runtime-architecture.md"
        )
        VerificationCommands = @(
            'dotnet test .\src\tests\DevNexus.Core.Tests\DevNexus.Core.Tests.csproj --no-build --filter "FullyQualifiedName~AgentLoopExecutorTests|FullyQualifiedName~ChatAgentLoopCoordinatorTests"',
            $CoreTestsCommand,
            $StagedBatchGateCommand,
            $WorktreeGateCommand
        )
        ImpactTargets = @(
            "AgentLoopExecutor",
            "AgentRuntimeRecoveryPromptBuilder"
        )
        ScenarioEvidenceHints = @(
            "Long goal with mixed tool failures chooses deterministic recovery or low-noise stop.",
            "Provider timeout, CLI wait, stdin continuation, and unfinished stop are covered by tests or session notes."
        )
    },
    [pscustomobject]@{
        Name = "CLI runtime stability"
        Description = "Terminal output slicing, live or archived log chunks, runtime summaries, and process host behavior."
        Patterns = @(
            "src/backend/DevNexus.Core/Services/Cli/*",
            "src/backend/DevNexus.Core/Services/Terminal/*",
            "src/backend/DevNexus.Infrastructure/Services/CliTerminal/*",
            "src/tests/DevNexus.Core.Tests/Services/Cli/*",
            "src/tests/DevNexus.Core.Tests/Services/Terminal/*",
            "docs/02-architecture/07-cli-runtime-stability-architecture.md",
            "docs/03-guides/agent-cli-operations.md"
        )
        VerificationCommands = @(
            'dotnet test .\src\tests\DevNexus.Core.Tests\DevNexus.Core.Tests.csproj --no-build --filter "FullyQualifiedName~CliRuntimeStatusSummaryBuilderTests|FullyQualifiedName~TerminalOutputTextSanitizerTests|FullyQualifiedName~TerminalLogChunkOutputSliceTests|FullyQualifiedName~TerminalRetainedOutputSliceTests"',
            $CoreTestsCommand,
            $StagedBatchGateCommand,
            $WorktreeGateCommand
        )
        ImpactTargets = @(
            "CliRuntimeStatusSummaryBuilder",
            "TerminalOutputTextSanitizer",
            "CliRuntimeCoordinator",
            "ProcessCliRuntimeHost"
        )
        ScenarioEvidenceHints = @(
            "Long-running command, interactive stdin, abnormal exit, Chinese output, large output, and concurrent sessions are covered.",
            "Live and archived log chunk sources stay isolated, with GetCliExecLog risk attribution recorded."
        )
    },
    [pscustomobject]@{
        Name = "Prompt cache diagnostics"
        Description = "Prompt cost facts, cache diagnostics, token audit logging, and related tests."
        Patterns = @(
            "src/backend/DevNexus.Core/Services/Chat/PromptCostDiagnostics.cs",
            "src/backend/DevNexus.Infrastructure/Services/LLM/TokenAuditFilter.cs",
            "src/tests/DevNexus.Core.Tests/Services/Chat/PromptCostDiagnosticsTests.cs"
        )
        VerificationCommands = @(
            'dotnet test .\src\tests\DevNexus.Core.Tests\DevNexus.Core.Tests.csproj --no-build --filter "FullyQualifiedName~PromptCostDiagnosticsTests"',
            $CoreTestsCommand,
            $StagedBatchGateCommand,
            $WorktreeGateCommand
        )
        ImpactTargets = @(
            "PromptCostDiagnostics",
            "TokenAuditFilter",
            "TokenAuditService"
        )
        ScenarioEvidenceHints = @(
            "Function-call and streaming-completion paths write NonCachedInputTokens, CacheHitRatio, DynamicContextRatio, and HistoryRatio.",
            "Prompt/cache diagnostics stay out of public audit tables, DTOs, APIs, database fields, and PromptCacheKey."
        )
    },
    [pscustomobject]@{
        Name = "Memory replay evaluation"
        Description = "System experience replay usefulness, pollution risk, traceability, and operations guidance."
        Patterns = @(
            "src/backend/DevNexus.Core/Services/Chat/SystemExperienceReplayEvaluation.cs",
            "src/backend/DevNexus.Core/Services/ChatService.TaskOrchestration.cs",
            "src/tests/DevNexus.Core.Tests/Services/Chat/SystemExperienceReplayEvaluationTests.cs",
            "docs/03-guides/memory-governance-operations.md"
        )
        VerificationCommands = @(
            'dotnet test .\src\tests\DevNexus.Core.Tests\DevNexus.Core.Tests.csproj --no-build --filter "FullyQualifiedName~SystemExperienceReplayEvaluationTests"',
            $CoreTestsCommand,
            $StagedBatchGateCommand,
            $WorktreeGateCommand
        )
        ImpactTargets = @(
            "SystemExperienceReplayEvaluation",
            "LogTaskOrchestrationSnapshot"
        )
        ScenarioEvidenceHints = @(
            "Useful recall, low similarity, context pollution risk, untraceable reuse risk, and empty replay are covered.",
            "Direct replay and dynamic context replay write [AI.Memory.ReplayEvaluation] facts without changing replay decisions.",
            "Sample session notes explain whether memory reuse improves answer quality without saving raw QA or temporary logs."
        )
    },
    [pscustomobject]@{
        Name = "Swarm readiness"
        Description = "Swarm presentation, session review policy, architecture, and low-noise UI guidance."
        Patterns = @(
            "src/backend/DevNexus.Core/Services/Swarm/*",
            "src/backend/DevNexus.Core/Services/ChatService.Swarm.cs",
            "src/backend/DevNexus.Core/Services/Swarm/SwarmChatPresentation.cs",
            "src/backend/DevNexus.Core/Services/Swarm/SwarmSessionReviewPolicy.cs",
            "src/backend/DevNexus.Core/Services/Swarm/Planning/ContextDrivenSwarmOrchestrator.cs",
            "src/tests/DevNexus.Core.Tests/Services/Swarm/SwarmChatPresentationTests.cs",
            "src/tests/DevNexus.Core.Tests/Services/Swarm/SwarmSessionReviewPolicyTests.cs",
            "docs/02-architecture/01-swarm-architecture.md",
            "docs/05-design/01-client-ui-design.md",
            "docs/05-design/README.md"
        )
        VerificationCommands = @(
            'dotnet test .\src\tests\DevNexus.Core.Tests\DevNexus.Core.Tests.csproj --no-build --filter "FullyQualifiedName~SwarmChatPresentationTests|FullyQualifiedName~SwarmSessionReviewPolicyTests"',
            $CoreTestsCommand,
            $StagedBatchGateCommand,
            $WorktreeGateCommand
        )
        ImpactTargets = @(
            "ExecuteSwarmExecutionAsync",
            "SwarmChatPresentation",
            "SwarmSessionReviewPolicy",
            "OrchestrateAsync"
        )
        ScenarioEvidenceHints = @(
            "Swarm startup is low-noise and restore-friendly without exposing internal complexity scores in main chat.",
            "Failed packages, blocking packages, terminal results, execution reports, and missing evidence are covered.",
            "Completed, cancelled, and failed orchestration paths write [AI.Swarm.Review] facts without changing scheduling decisions."
        )
    },
    [pscustomobject]@{
        Name = "Product documentation and release gates"
        Description = "Repository navigation, release checks, change batching, and product readiness planning."
        Patterns = @(
            "README.md",
            "DevNexus-AI.txt",
            "docs/README.md",
            "docs/03-guides/README.md",
            "docs/03-guides/prompt-cache-diagnostics.md",
            "docs/03-guides/user-guide.md",
            "docs/06-development/*",
            "docs/07-faq/readme.md",
            "docs/07-faq/usage.md",
            "tools/*"
        )
        VerificationCommands = @(
            $StagedBatchGateCommand,
            $WorktreeGateCommand
        )
        ImpactTargets = @()
        ScenarioEvidenceHints = @(
            "User guide, FAQ, docs navigation, release gates, and product readiness plan all point to the same validation path.",
            "Documentation-only batches explain why runtime scenario evidence is not applicable, or link to the scenario matrix when it is."
        )
    },
    [pscustomobject]@{
        Name = "Shared chat runtime"
        Description = "Shared chat service entry points that should be reviewed with the narrow batch that touched them."
        Patterns = @(
            "src/backend/DevNexus.Core/Services/ChatService.cs"
        )
        VerificationCommands = @(
            $CoreTestsCommand,
            $StagedBatchGateCommand,
            $WorktreeGateCommand
        )
        ImpactTargets = @(
            "StreamMessageAsync",
            "ChatService"
        )
        ScenarioEvidenceHints = @(
            "Shared chat runtime changes record StreamMessageAsync process risk and affected chat recovery behavior.",
            "Refresh, reconnect, Swarm startup state, or low-noise chat status behavior is covered by tests or session notes."
        )
    }
)

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

function ConvertTo-RepoPath {
    param([string]$File)

    return $File.Replace("\", "/")
}

function Test-MatchesPattern {
    param(
        [string]$File,
        [string]$Pattern
    )

    $normalizedFile = ConvertTo-RepoPath -File $File
    $normalizedPattern = ConvertTo-RepoPath -File $Pattern
    return $normalizedFile -like $normalizedPattern
}

function Find-Batch {
    param([string]$File)

    foreach ($batch in $BatchDefinitions) {
        foreach ($pattern in $batch.Patterns) {
            if (Test-MatchesPattern -File $File -Pattern $pattern) {
                return $batch
            }
        }
    }

    return $null
}

function Write-FileList {
    param([string[]]$Files)

    foreach ($file in $Files) {
        Write-Host "  - $file"
    }
}

function Write-MarkdownFileList {
    param([string[]]$Files)

    foreach ($file in $Files) {
        Write-Host "  - $file"
    }
}

function Get-GitAddCommand {
    param([string[]]$Files)

    $quotedFiles = $Files | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
    return "git add -- $($quotedFiles -join ' ')"
}

function Get-TargetedStagedBatchGateCommand {
    param([string]$Name)

    $escapedName = $Name.Replace('"', '\"')
    return ".\tools\verify-staged-batch.ps1 -BatchName ""$escapedName"""
}

function Resolve-BatchVerificationCommands {
    param([object]$Batch)

    return @(
        foreach ($command in $Batch.VerificationCommands) {
            if ($command -eq $StagedBatchGateCommand) {
                Get-TargetedStagedBatchGateCommand -Name $Batch.Name
                continue
            }

            $command
        }
    )
}

function Write-MarkdownCommandBlock {
    param([string[]]$Commands)

    Write-Host '```powershell'
    foreach ($command in $Commands) {
        Write-Host $command
    }
    Write-Host '```'
}

function Get-MatchedFileCount {
    param(
        [string[]]$Files,
        [string[]]$Candidates
    )

    return @($Files | Where-Object { $Candidates -contains $_ }).Count
}

function Get-FileSourceSummary {
    param(
        [string[]]$Files,
        [string[]]$StagedFiles,
        [string[]]$UnstagedTrackedFiles,
        [string[]]$UntrackedFiles
    )

    return [pscustomobject]@{
        StagedTracked = Get-MatchedFileCount -Files $Files -Candidates $StagedFiles
        UnstagedTracked = Get-MatchedFileCount -Files $Files -Candidates $UnstagedTrackedFiles
        Untracked = Get-MatchedFileCount -Files $Files -Candidates $UntrackedFiles
    }
}

function Get-BatchReleaseDecision {
    param(
        [int]$BatchCount,
        [string]$BatchName
    )

    if ($BatchCount -le 1) {
        return [pscustomobject]@{
            Value = "Ready"
            Reason = "Scope contains one product batch."
        }
    }

    if ($BatchName -eq "Shared chat runtime") {
        return [pscustomobject]@{
            Value = "SplitRequired"
            Reason = "Shared chat runtime must be reviewed with the narrow batch that touched it."
        }
    }

    return [pscustomobject]@{
        Value = "SplitRequired"
        Reason = "Scope still contains multiple product batches; stage and verify this batch separately."
    }
}

function Get-ReadinessBlockers {
    param(
        [object]$Decision,
        [object]$SourceSummary,
        [string]$ScopeName
    )

    $blockers = @()

    if ($Decision.Value -ne "Ready") {
        $blockers += $Decision.Reason
    }

    if ($SourceSummary.StagedTracked -eq 0) {
        $blockers += "Batch files are not staged; run the git add command and verify staged scope."
    }

    if ($ScopeName -eq "Staged" -and $SourceSummary.UnstagedTracked -gt 0) {
        $blockers += "Staged batch has unstaged changes in the same file scope; stage them or record the deliberate hunk split."
    }

    if ($SourceSummary.Untracked -gt 0) {
        $blockers += "Untracked files must be staged or covered by verify-worktree.ps1 checks before commit."
    }

    $blockers += "Complete Pre-change impact, detect_changes, verification results, scenario evidence, untracked coverage, and rollback plan."

    return $blockers
}

function Write-MarkdownAcceptanceRecord {
    param(
        [object]$Batch,
        [string[]]$Files,
        [int]$BatchCount,
        [bool]$IncludeGitAddCommand,
        [object]$SourceSummary
    )

    $decision = Get-BatchReleaseDecision -BatchCount $BatchCount -BatchName $Batch.Name
    $readinessBlockers = @(Get-ReadinessBlockers -Decision $decision -SourceSummary $SourceSummary -ScopeName $Scope)

    Write-Host "## Batch acceptance record"
    Write-Host "- Batch name: $($Batch.Name)"
    Write-Host "- Batch goal: $($Batch.Description)"
    Write-Host "- File scope:"
    Write-MarkdownFileList -Files $Files
    Write-Host ("- File source summary: staged tracked={0}; unstaged tracked={1}; untracked={2}" -f $SourceSummary.StagedTracked, $SourceSummary.UnstagedTracked, $SourceSummary.Untracked)
    if ($Scope -eq "Staged" -and $SourceSummary.UnstagedTracked -gt 0) {
        Write-Host "- Staged boundary: incomplete; the same file scope still has unstaged changes."
    }
    if ($IncludeGitAddCommand) {
        Write-Host "- Git add command:"
        Write-MarkdownCommandBlock -Commands @((Get-GitAddCommand -Files $Files))
    }
    if ($Batch.VerificationCommands.Count -gt 0) {
        Write-Host "- Suggested verification commands:"
        Write-MarkdownCommandBlock -Commands (Resolve-BatchVerificationCommands -Batch $Batch)
    }
    Write-Host "- Targeted staged gate command:"
    Write-MarkdownCommandBlock -Commands @((Get-TargetedStagedBatchGateCommand -Name $Batch.Name))
    if ($Batch.ImpactTargets.Count -gt 0) {
        Write-Host "- Suggested impact targets:"
        Write-MarkdownFileList -Files $Batch.ImpactTargets
    }
    if ($Batch.ScenarioEvidenceHints.Count -gt 0) {
        Write-Host "- Suggested scenario evidence:"
        Write-MarkdownFileList -Files $Batch.ScenarioEvidenceHints
    }
    Write-Host "- Pre-change impact: TODO. Record target symbols, risk level, direct callers, and whether the edit should proceed."
    Write-Host "- detect_changes: TODO. If the worktree is HIGH or CRITICAL, list affected processes that are not introduced by this batch."
    Write-Host "- Untracked coverage: TODO. If untracked > 0, confirm verify-worktree.ps1 covered BOM, line count, trailing whitespace, and conflict markers."
    Write-Host "- Verification results: TODO. Paste command results or explain skipped commands."
    Write-Host "- Scenario evidence: TODO. Record applicable product scenario evidence from docs/06-development/testing.md and the suggested scenario evidence above."
    Write-Host "- Rollback plan: Revert implementation, tests, and docs in this batch. List runtime call sites when touched."
    Write-Host "- Release decision: $($decision.Value)"
    Write-Host "- Release decision reason: $($decision.Reason)"
    if ($readinessBlockers.Count -gt 0) {
        Write-Host "- Readiness blockers:"
        Write-MarkdownFileList -Files $readinessBlockers
    }
}

function Test-BatchNameMatches {
    param(
        [string]$ActualName,
        [string]$ExpectedName
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedName)) {
        return $true
    }

    return $ActualName -like "*$ExpectedName*"
}

function Test-SingleBatchRequirement {
    param(
        [object[]]$AllActiveBatches,
        [object[]]$FilteredActiveBatches,
        [string[]]$UnclassifiedFiles
    )

    if (-not $RequireSingleBatch) {
        return
    }

    if ($UnclassifiedFiles.Count -gt 0) {
        Write-Host "ERROR: Single batch requirement failed: unclassified files are present."
        exit 1
    }

    if ($AllActiveBatches.Count -eq 0) {
        Write-Host "ERROR: Single batch requirement failed: no changed files were found in scope '$Scope'."
        exit 1
    }

    if ($FilteredActiveBatches.Count -eq 0) {
        Write-Host "ERROR: Single batch requirement failed: no batch matched the current filter."
        exit 1
    }

    if ($AllActiveBatches.Count -ne 1) {
        $batchNames = $AllActiveBatches | ForEach-Object { $_.Batch.Name }
        Write-Host ("ERROR: Single batch requirement failed: scope '{0}' contains {1} batches: {2}" -f $Scope, $AllActiveBatches.Count, ($batchNames -join ", "))
        exit 1
    }
}

$stagedFiles = @(Get-StagedChangedFileList)
$unstagedTrackedFiles = @(Get-TrackedChangedFileList)
$untrackedFiles = @(Get-UntrackedFileList)
$changedFiles = @(Get-ChangedFileList)
$groupedFiles = [ordered]@{}
$unclassifiedFiles = @()

foreach ($batch in $BatchDefinitions) {
    $groupedFiles[$batch.Name] = @()
}

foreach ($file in $changedFiles) {
    $batch = Find-Batch -File $file
    if ($null -eq $batch) {
        $unclassifiedFiles += $file
        continue
    }

    $groupedFiles[$batch.Name] = @($groupedFiles[$batch.Name]; $file)
}

$allActiveBatches = @(
    foreach ($batch in $BatchDefinitions) {
        $files = @($groupedFiles[$batch.Name])
        if ($files.Count -gt 0) {
            [pscustomobject]@{
                Batch = $batch
                Files = $files
            }
        }
    }
)

$activeBatches = @(
    foreach ($entry in $allActiveBatches) {
        if (Test-BatchNameMatches -ActualName $entry.Batch.Name -ExpectedName $BatchName) {
            $entry
        }
    }
)

Test-SingleBatchRequirement -AllActiveBatches $allActiveBatches -FilteredActiveBatches $activeBatches -UnclassifiedFiles $unclassifiedFiles

Write-Host "Changed file count: $($changedFiles.Count)"
Write-Host "Scope: $Scope"
if ($RequireSingleBatch) {
    Write-Host "Require single batch: true"
}
if (-not [string]::IsNullOrWhiteSpace($BatchName)) {
    Write-Host "Batch filter: $BatchName"
}

if ($OutputMarkdown) {
    Write-Host "Markdown acceptance records:"

    foreach ($entry in $activeBatches) {
        Write-Host ""
        $sourceSummary = Get-FileSourceSummary -Files $entry.Files -StagedFiles $stagedFiles -UnstagedTrackedFiles $unstagedTrackedFiles -UntrackedFiles $untrackedFiles
        Write-MarkdownAcceptanceRecord -Batch $entry.Batch -Files $entry.Files -BatchCount $allActiveBatches.Count -IncludeGitAddCommand $ShowGitAddCommands -SourceSummary $sourceSummary
    }

    if ($unclassifiedFiles.Count -gt 0) {
        Write-Host ""
        Write-Host "## Unclassified files"
        Write-Host "Review these files manually or update script rules before preparing a batch."
        Write-MarkdownFileList -Files $unclassifiedFiles
    }

    if ($changedFiles.Count -eq 0) {
        Write-Host "No changed files detected."
    }

    if ($changedFiles.Count -gt 0 -and $activeBatches.Count -eq 0) {
        Write-Host "No changed files matched the batch filter."
    }

    return
}

Write-Host "Suggested product batches:"

foreach ($batch in $BatchDefinitions) {
    $files = @($groupedFiles[$batch.Name])
    if ($files.Count -eq 0) {
        continue
    }

    if (-not (Test-BatchNameMatches -ActualName $batch.Name -ExpectedName $BatchName)) {
        continue
    }

    Write-Host ""
    Write-Host "[$($batch.Name)]"
    Write-Host $batch.Description
    Write-FileList -Files $files

    if ($ShowGitAddCommands) {
        Write-Host "  $(Get-GitAddCommand -Files $files)"
    }
}

if ($unclassifiedFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "[Unclassified]"
    Write-Host "Review these files manually before preparing a batch."
    Write-FileList -Files $unclassifiedFiles
}

if ($changedFiles.Count -eq 0) {
    Write-Host "No changed files detected."
}

if ($changedFiles.Count -gt 0 -and $activeBatches.Count -eq 0) {
    Write-Host "No changed files matched the batch filter."
}
