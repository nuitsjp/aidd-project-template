param(
    [Parameter(Mandatory = $true)][ValidateSet('all', 'unit', 'integration', 'e2e', 'checks')][string]$Suite,
    [Parameter(Mandatory = $true)][string]$ResultsPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$appProject = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'app') -Filter '*.csproj' -File)[0]
$assemblyName = ([xml](Get-Content -LiteralPath $appProject.FullName -Raw)).Project.PropertyGroup.AssemblyName | Where-Object { $_ }
$env:WPF_APP_PATH = Join-Path $projectRoot "app/bin/Release/net481/$assemblyName.exe"
$env:WPF_MOCK_APP_PATH = Join-Path $projectRoot "app/bin/Mock/net481/$assemblyName.exe"
$checkScripts = [ordered]@{
    'xaml-checks' = 'tests/xaml/check-xaml.tests.ps1'
    'coverage-checks' = 'tests/runner/check-coverage.tests.ps1'
    'runner-checks' = 'tests/runner/run-tests.tests.ps1'
}
$groups = if ($Suite -eq 'checks') { @($checkScripts.Keys) }
    elseif ($Suite -eq 'all') { @('unit', 'integration', 'e2e') } else { @($Suite) }
$runs = @()
$failed = $false

# 同じバイナリを計装・再ビルドせず、各グループを起動する。E2E の並列度はテストアセンブリで設定する。
try {
    foreach ($group in $groups) {
        if ($Suite -eq 'checks') {
            $testPath = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
            $scriptPath = Join-Path $projectRoot $checkScripts[$group]
            $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $scriptPath))
            $workingDirectory = $projectRoot
        } else {
            $testProject = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot "tests/$group") -Filter '*.csproj' -File)[0]
            $testAssembly = ([xml](Get-Content -LiteralPath $testProject.FullName -Raw)).Project.PropertyGroup.AssemblyName | Where-Object { $_ }
            $testPath = Join-Path $projectRoot "tests/$group/bin/Release/net481/$testAssembly.exe"
            $arguments = @('-noColor')
            $workingDirectory = Split-Path -Parent $testPath
        }
        $stdout = Join-Path $ResultsPath "$group.stdout.log"
        $stderr = Join-Path $ResultsPath "$group.stderr.log"
        $process = Start-Process -FilePath $testPath -ArgumentList $arguments -WorkingDirectory $workingDirectory `
            -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        # Windows PowerShell 5.1 では終了前にハンドルを保持しないと ExitCode が取得できない。
        $null = $process.Handle
        $runs += [PSCustomObject]@{ Group = $group; Process = $process; Stdout = $stdout; Stderr = $stderr }
        Write-Output "Started $group (PID $($process.Id))"
    }
} finally {
    # 失敗したグループがあっても全テストを待ち、出力と計測結果を残す。
    foreach ($run in $runs) {
        $run.Process.WaitForExit()
        Write-Output "Test results: $($run.Group)"
        Get-Content -LiteralPath $run.Stdout
        Get-Content -LiteralPath $run.Stderr
        if ($run.Process.ExitCode -ne 0) {
            Write-Output "$($run.Group) exited with code $($run.Process.ExitCode)"
            $failed = $true
        }
        $run.Process.Dispose()
    }
}
if ($failed) { exit 1 }
