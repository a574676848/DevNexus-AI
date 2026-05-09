using System.Text;
using System.Text.Json;
using DevNexus.Core.Models.Execution;
using DevNexus.Domain.Entities;

namespace DevNexus.Infrastructure.Services.Files;

internal static class FileTaskExecutionScriptBuilder
{
    public static async Task<string> WriteExecutionScriptAsync(FileTask task, string taskDirectoryPath)
    {
        var outputsPath = Path.Combine(taskDirectoryPath, "outputs");
        var contractPath = Path.Combine(taskDirectoryPath, "task-execution-contract.json");
        var scriptPath = Path.Combine(taskDirectoryPath, "execute-file-task.ps1");

        var contract = new
        {
            FileTaskId = task.Id,
            task.TaskType,
            Instructions = task.Instructions ?? string.Empty,
            TaskDirectoryPath = taskDirectoryPath,
            InputsPath = Path.Combine(taskDirectoryPath, "inputs"),
            TemplatesPath = Path.Combine(taskDirectoryPath, "templates"),
            OutputsPath = outputsPath,
            ManifestPath = Path.Combine(taskDirectoryPath, "task-manifest.json"),
            PreferredRunners = new[] { "runner.ps1", "runner.py" },
            GeneratedAt = DateTime.UtcNow
        };

        var contractJson = JsonSerializer.Serialize(contract, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(contractPath, contractJson, Encoding.UTF8);

        var successOutput = TaggedExecutionText.Success(
            "File task finished. Runner=$runnerUsed; Outputs=$($generatedFiles.Count); Fallback=$fallbackUsed");

        var script = """
$ErrorActionPreference = 'Stop'
    $taskRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $inputsPath = Join-Path $taskRoot 'inputs'
    $templatesPath = Join-Path $taskRoot 'templates'
    $outputsPath = Join-Path $taskRoot 'outputs'
    $manifestPath = Join-Path $taskRoot 'task-manifest.json'
    $instructionsPath = Join-Path $taskRoot 'instructions.txt'
    $contractPath = Join-Path $taskRoot 'task-execution-contract.json'
    $runnerPwsh = Join-Path $taskRoot 'runner.ps1'
    $runnerPython = Join-Path $taskRoot 'runner.py'
$runnerUsed = ''

New-Item -ItemType Directory -Path $outputsPath -Force | Out-Null

function Copy-StagedDirectory([string]$sourcePath, [string]$targetPath) {
    if (-not (Test-Path $sourcePath)) {
        return
    }

    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    Copy-Item -Path (Join-Path $sourcePath '*') -Destination $targetPath -Recurse -Force -ErrorAction SilentlyContinue
}

function Get-PythonCommand() {
    foreach ($candidate in @('python', 'py')) {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $command) {
            return $candidate
        }
    }

    return $null
}

function Invoke-ExternalRunner() {
    if (Test-Path $runnerPwsh) {
        $script:runnerUsed = 'runner.ps1'
        & $runnerPwsh -ContractPath $contractPath -TaskDirectoryPath $taskRoot
        return $LASTEXITCODE
    }

    if (Test-Path $runnerPython) {
        $pythonCommand = Get-PythonCommand
        if (-not [string]::IsNullOrWhiteSpace($pythonCommand)) {
            $script:runnerUsed = 'runner.py'
            & $pythonCommand $runnerPython --contract-path $contractPath --task-directory-path $taskRoot
            return $LASTEXITCODE
        }
    }

    return 0
}

function Write-FallbackOutputs() {
    $referenceRoot = Join-Path $outputsPath 'references'
    Copy-StagedDirectory -sourcePath $inputsPath -targetPath (Join-Path $referenceRoot 'inputs')
    Copy-StagedDirectory -sourcePath $templatesPath -targetPath (Join-Path $referenceRoot 'templates')

    $inputNames = @()
    if (Test-Path $inputsPath) {
        $inputNames = @(Get-ChildItem -Path $inputsPath -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    }

    $templateNames = @()
    if (Test-Path $templatesPath) {
        $templateNames = @(Get-ChildItem -Path $templatesPath -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    }

    $instructions = if (Test-Path $instructionsPath) { Get-Content -Path $instructionsPath -Raw } else { '' }
    $executionMode = if ([string]::IsNullOrWhiteSpace($runnerUsed)) {
        '未检测到外部执行器，已输出任务摘要与参考文件。'
    }
    else {
        "已通过外部执行器处理，执行器: $runnerUsed。"
    }

    $summaryLines = @(
        '# File Task Summary',
        '',
        '## Execution Mode',
        $executionMode,
        '',
        '## Inputs',
        $(if ($inputNames.Count -gt 0) { $inputNames | ForEach-Object { "- $_" } } else { '- 无输入文件' }),
        '',
        '## Templates',
        $(if ($templateNames.Count -gt 0) { $templateNames | ForEach-Object { "- $_" } } else { '- 无模板文件' }),
        '',
        '## Task Files',
        "- Manifest: $manifestPath",
        "- Contract: $contractPath",
        '',
        '## Instructions',
        $(if ([string]::IsNullOrWhiteSpace($instructions)) { '无附加指令' } else { $instructions.TrimEnd() })
    )

    $summaryPath = Join-Path $outputsPath 'task-summary.md'
    Set-Content -Path $summaryPath -Value ($summaryLines -join [Environment]::NewLine) -Encoding UTF8
}

$exitCode = Invoke-ExternalRunner
if ($exitCode -ne 0) {
    throw "外部执行器退出码异常: $exitCode"
}

$fallbackUsed = $false
$generatedFiles = @(Get-ChildItem -Path $outputsPath -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Extension -ne '.ps1' })
if ($generatedFiles.Count -eq 0) {
    Write-FallbackOutputs
    $fallbackUsed = $true
    $generatedFiles = @(Get-ChildItem -Path $outputsPath -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Extension -ne '.ps1' })
}

if ($generatedFiles.Count -eq 0) {
    throw '任务执行完成，但未生成任何输出文件'
}

Write-Output "__SUCCESS_OUTPUT__"
""".Replace("__SUCCESS_OUTPUT__", successOutput);

        await File.WriteAllTextAsync(scriptPath, script, Encoding.UTF8);
        return scriptPath;
    }
}
