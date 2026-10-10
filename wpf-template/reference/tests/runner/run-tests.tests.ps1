$ErrorActionPreference = 'Stop'
$runnerPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../scripts/run-tests.ps1')).ProviderPath
$powershellPath = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixtureRoot = [IO.Path]::GetFullPath((Join-Path $tempRoot ('wpf-run-tests-回帰 test-' + [Guid]::NewGuid().ToString('N'))))
$utf8 = [Text.UTF8Encoding]::new($true)
$previousProbeDirectory = $env:RUN_TESTS_PROBE_DIRECTORY
$previousFailGroup = $env:RUN_TESTS_PROBE_FAIL_GROUP
$previousExpectedGroups = $env:RUN_TESTS_PROBE_EXPECTED_GROUPS
$groups = @('unit', 'integration', 'e2e')
$checkGroups = @('xaml-checks', 'coverage-checks', 'runner-checks')
$passed = 0

function Invoke-Suite([string]$Name, [string]$Suite, [string]$FailGroup = '', [string[]]$ExpectedGroups = @()) {
    $caseRoot = Join-Path $fixtureRoot $Name
    $events = Join-Path $caseRoot 'events'
    $logs = Join-Path $caseRoot 'logs'
    New-Item -ItemType Directory -Path $events, $logs -Force | Out-Null
    $env:RUN_TESTS_PROBE_DIRECTORY = $events
    $env:RUN_TESTS_PROBE_FAIL_GROUP = $FailGroup
    if ($ExpectedGroups.Count -eq 0) {
        $ExpectedGroups = switch ($Suite) {
            'all' { $groups }
            'checks' { $checkGroups }
            default { @($Suite) }
        }
    }
    $env:RUN_TESTS_PROBE_EXPECTED_GROUPS = $ExpectedGroups -join '|'
    $existingResults = @()
    if ($Suite -eq 'verify') {
        $existingResults = @(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'TestResults') -Directory -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName)
    }
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # 起動失敗の標準エラーも、親スクリプトを停止せず検証結果に含める。
        $ErrorActionPreference = 'Continue'
        if ($Suite -eq 'verify') {
            $output = @(& $powershellPath -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixtureRoot 'scripts/verify-probe.ps1') 2>&1)
        } else {
            $output = @(& $powershellPath -NoProfile -ExecutionPolicy Bypass -File (Join-Path $fixtureRoot 'scripts/run-tests.ps1') -Suite $Suite -ResultsPath $logs 2>&1)
        }
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($Suite -eq 'verify') {
        $newResults = @(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'TestResults') -Directory |
            Where-Object { $existingResults -notcontains $_.FullName })
        if ($newResults.Count -ne 1) { throw 'Verify must create one checks results directory.' }
        $logs = $newResults[0].FullName
    }
    $errorOutput = (@($output | Where-Object { $_ -is [Management.Automation.ErrorRecord] }) -join "`n")
    return [PSCustomObject]@{ ExitCode = $exitCode; Output = ($output -join "`n"); ErrorOutput = $errorOutput; Events = $events; Logs = $logs }
}

function Assert-ExitCode($Result, [int]$Expected) {
    if ($Result.ExitCode -ne $Expected) {
        throw "Expected exit $Expected, got $($Result.ExitCode). Output: $($Result.Output)"
    }
}

function Assert-GroupCompleted($Result, [string]$Group) {
    foreach ($event in @('start', 'end')) {
        $path = Join-Path $Result.Events "$Group.$event"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing $Group $event event. Output: $($Result.Output)"
        }
    }
    $start = [long][IO.File]::ReadAllText((Join-Path $Result.Events "$Group.start"))
    $end = [long][IO.File]::ReadAllText((Join-Path $Result.Events "$Group.end"))
    if ($end -le $start) { throw "Invalid timestamps for $Group." }
    $stdoutPath = Join-Path $Result.Logs "$Group.stdout.log"
    $stderrPath = Join-Path $Result.Logs "$Group.stderr.log"
    if (-not (Test-Path -LiteralPath $stdoutPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $stderrPath -PathType Leaf)) {
        throw "Missing saved logs for $Group."
    }
    $stdout = [IO.File]::ReadAllText($stdoutPath)
    $stderr = [IO.File]::ReadAllText($stderrPath)
    if (-not $stdout.Contains("$Group start") -or -not $stdout.Contains("$Group end") -or
        -not $stderr.Contains("$Group stderr")) {
        throw "Incomplete saved logs for $Group. stdout: $stdout stderr: $stderr"
    }
}

function Assert-AllParallel($Result, [string[]]$ExpectedGroups = $groups) {
    foreach ($group in $ExpectedGroups) { Assert-GroupCompleted $Result $group }
    $starts = @($ExpectedGroups | ForEach-Object { [long][IO.File]::ReadAllText((Join-Path $Result.Events "$_.start")) })
    $ends = @($ExpectedGroups | ForEach-Object { [long][IO.File]::ReadAllText((Join-Path $Result.Events "$_.end")) })
    $lastStart = ($starts | Measure-Object -Maximum).Maximum
    $firstEnd = ($ends | Measure-Object -Minimum).Minimum
    if ($lastStart -ge $firstEnd) {
        throw "All groups must start before the first group ends. Output: $($Result.Output)"
    }
}

function Pass([string]$Name) {
    $script:passed++
    Write-Output "PASS $Name"
}

try {
    New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'scripts'), (Join-Path $fixtureRoot 'app') -Force | Out-Null
    Copy-Item -LiteralPath $runnerPath -Destination (Join-Path $fixtureRoot 'scripts/run-tests.ps1')
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'app/App.csproj'),
        '<Project><PropertyGroup><AssemblyName>FixtureApp</AssemblyName></PropertyGroup></Project>', $utf8)
    $probePath = Join-Path $fixtureRoot 'probe.exe'
    Add-Type -TypeDefinition @'
using System;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

public static class RunTestsProbe
{
    public static int Main(string[] args)
    {
        var group = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
        var directory = Environment.GetEnvironmentVariable("RUN_TESTS_PROBE_DIRECTORY");
        File.WriteAllText(Path.Combine(directory, group + ".start"), Stopwatch.GetTimestamp().ToString(CultureInfo.InvariantCulture));
        Console.WriteLine(group + " start");
        Console.Error.WriteLine(group + " stderr");
        var expectedGroups = Environment.GetEnvironmentVariable("RUN_TESTS_PROBE_EXPECTED_GROUPS").Split('|');
        var wait = Stopwatch.StartNew();
        while (!Array.TrueForAll(expectedGroups, expected => File.Exists(Path.Combine(directory, expected + ".start"))))
        {
            if (wait.ElapsedMilliseconds >= 30000)
            {
                Console.Error.WriteLine(group + " Timeout: waiting for all expected groups to start");
                return 1;
            }

            Thread.Sleep(10);
        }
        File.WriteAllText(Path.Combine(directory, group + ".end"), Stopwatch.GetTimestamp().ToString(CultureInfo.InvariantCulture));
        Console.WriteLine(group + " end");
        var failedGroups = (Environment.GetEnvironmentVariable("RUN_TESTS_PROBE_FAIL_GROUP") ?? "").Split('|');
        return Array.IndexOf(failedGroups, group) >= 0 ? 1 : 0;
    }
}
'@ -OutputAssembly $probePath -OutputType ConsoleApplication
    foreach ($group in $groups) {
        $groupRoot = Join-Path $fixtureRoot "tests/$group"
        $bin = Join-Path $groupRoot 'bin/Release/net481'
        New-Item -ItemType Directory -Path $bin -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $groupRoot "$group.csproj"),
            "<Project><PropertyGroup><AssemblyName>$group</AssemblyName></PropertyGroup></Project>", $utf8)
        Copy-Item -LiteralPath $probePath -Destination (Join-Path $bin "$group.exe")
    }
    foreach ($check in @(
        @{ Group = 'xaml-checks'; Script = 'tests/xaml/check-xaml.tests.ps1' },
        @{ Group = 'coverage-checks'; Script = 'tests/runner/check-coverage.tests.ps1' },
        @{ Group = 'runner-checks'; Script = 'tests/runner/run-tests.tests.ps1' }
    )) {
        $scriptPath = Join-Path $fixtureRoot $check.Script
        $scriptDirectory = Split-Path -Parent $scriptPath
        New-Item -ItemType Directory -Path $scriptDirectory -Force | Out-Null
        Copy-Item -LiteralPath $probePath -Destination (Join-Path $scriptDirectory ($check.Group + '.exe'))
        $script = "& (Join-Path `$PSScriptRoot '$($check.Group).exe')`nexit `$LASTEXITCODE`n"
        [IO.File]::WriteAllText($scriptPath, $script, $utf8)
    }
    Copy-Item -LiteralPath $probePath -Destination (Join-Path $fixtureRoot 'scripts/product.exe')
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'scripts/check-docs.ps1'), '', $utf8)
    $verifyTokens = $null
    $verifyErrors = $null
    $runAst = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path (Split-Path -Parent $runnerPath) 'run.ps1'), [ref]$verifyTokens, [ref]$verifyErrors)
    if ($verifyErrors.Count -ne 0) { throw ($verifyErrors -join "`n") }
    $verifyFunction = $runAst.Find({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Verify'
    }, $true)
    if ($null -eq $verifyFunction) { throw 'The production Verify function was not found.' }
    $verifyStub = @'
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Format-Code([switch]$Check) {}
function Invoke-Native([string]$Command, [string[]]$Arguments) {}
function Test-Projects([string]$Suite, [switch]$CheckCoverage) {
    $stdout = Join-Path $checksResults 'product.stdout.log'
    $stderr = Join-Path $checksResults 'product.stderr.log'
    $process = Start-Process -FilePath (Join-Path $PSScriptRoot 'product.exe') -WindowStyle Hidden `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $null = $process.Handle
    try {
        $process.WaitForExit()
        Get-Content -LiteralPath $stdout
        Get-Content -LiteralPath $stderr
        $exitCode = $process.ExitCode
    } finally {
        $process.Dispose()
    }
    if ($exitCode -ne 0) { throw "Product tests exited with code $exitCode" }
}
'@
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'scripts/verify-probe.ps1'),
        ($verifyStub + "`n" + $verifyFunction.Extent.Text + "`nVerify`n"), $utf8)

    $result = Invoke-Suite '全グループ成功' 'all'
    Assert-ExitCode $result 0
    Assert-AllParallel $result
    Pass 'all groups run in parallel and save stdout/stderr logs'

    $result = Invoke-Suite '単体失敗でも全グループ完走' 'all' 'unit'
    Assert-ExitCode $result 1
    Assert-AllParallel $result
    if (-not $result.Output.Contains('unit exited with code 1')) {
        throw "Failed group was not reported. Output: $($result.Output)"
    }
    Pass 'a failing group returns exit 1 after every group completes'

    $result = Invoke-Suite '単体だけを実行' 'unit'
    Assert-ExitCode $result 0
    Assert-GroupCompleted $result 'unit'
    foreach ($group in @('integration', 'e2e')) {
        foreach ($event in @('start', 'end')) {
            if (Test-Path -LiteralPath (Join-Path $result.Events "$group.$event")) {
                throw "Unexpected $group execution for the unit suite."
            }
        }
        foreach ($stream in @('stdout', 'stderr')) {
            if (Test-Path -LiteralPath (Join-Path $result.Logs "$group.$stream.log")) {
                throw "Unexpected $group log for the unit suite."
            }
        }
    }
    Pass 'the unit suite runs only unit tests and returns exit 0'

    $result = Invoke-Suite 'checks parallel success' 'checks'
    Assert-ExitCode $result 0
    Assert-AllParallel $result $checkGroups
    Pass 'checks groups run in parallel and save stdout/stderr logs'

    $result = Invoke-Suite 'checks failure completes every group' 'checks' 'coverage-checks'
    Assert-ExitCode $result 1
    Assert-AllParallel $result $checkGroups
    if (-not $result.Output.Contains('coverage-checks exited with code 1')) {
        throw "Failed checks group was not reported. Output: $($result.Output)"
    }
    Pass 'a failing checks group returns exit 1 after every checks group completes'

    $missingExecutable = Join-Path $fixtureRoot 'tests/integration/bin/Release/net481/integration.exe'
    Remove-Item -LiteralPath $missingExecutable
    $result = Invoke-Suite 'launch failure completes started group' 'all' '' @('unit')
    Assert-ExitCode $result 1
    Assert-GroupCompleted $result 'unit'
    if (-not $result.Output.Contains('Started unit') -or -not $result.Output.Contains('Start-Process')) {
        throw "The launch failure was not reported after starting unit. Output: $($result.Output)"
    }
    if (Test-Path -LiteralPath (Join-Path $result.Events 'e2e.start')) {
        throw 'The group after a launch failure must not execute.'
    }
    Pass 'a launch failure waits for the started group and saves its logs'

    $verifyGroups = $checkGroups + @('product')
    $result = Invoke-Suite 'verify parallel success' 'verify' '' $verifyGroups
    Assert-ExitCode $result 0
    Assert-AllParallel $result $verifyGroups
    Pass 'Verify runs product tests and all checks in parallel and completes every group'

    $result = Invoke-Suite 'verify checks failure' 'verify' 'coverage-checks' $verifyGroups
    Assert-ExitCode $result 1
    Assert-AllParallel $result $verifyGroups
    if (-not $result.Output.Contains('coverage-checks exited with code 1') -or
        -not $result.ErrorOutput.Contains('Regression checks exited with code 1')) {
        throw "Verify did not propagate the checks failure. Output: $($result.Output)"
    }
    Pass 'Verify waits for successful product tests and all checks before reporting a checks failure'

    $result = Invoke-Suite 'verify product and checks failure' 'verify' 'product|coverage-checks' $verifyGroups
    Assert-ExitCode $result 1
    Assert-AllParallel $result $verifyGroups
    if (-not $result.Output.Contains('coverage-checks exited with code 1') -or
        -not $result.Output.Contains('Regression checks exited with code 1') -or
        -not $result.ErrorOutput.Contains('Product tests exited with code 1') -or
        $result.ErrorOutput.Contains('Regression checks exited with code 1')) {
        throw "Verify did not retain the product failure while reporting the checks failure. Output: $($result.Output)"
    }
    Pass 'Verify completes every group and retains the product error when product and checks both fail'

    Write-Output "Test orchestrator regression tests passed ($passed cases)."
} finally {
    $env:RUN_TESTS_PROBE_DIRECTORY = $previousProbeDirectory
    $env:RUN_TESTS_PROBE_FAIL_GROUP = $previousFailGroup
    $env:RUN_TESTS_PROBE_EXPECTED_GROUPS = $previousExpectedGroups
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixture = (Resolve-Path -LiteralPath $fixtureRoot).ProviderPath
        $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).ProviderPath.TrimEnd([char[]]@('\', '/'))
        $tempPrefix = $resolvedTemp + [IO.Path]::DirectorySeparatorChar
        if (-not [IO.Path]::IsPathRooted($resolvedFixture) -or
            -not $resolvedFixture.Equals($fixtureRoot, [StringComparison]::OrdinalIgnoreCase) -or
            -not $resolvedFixture.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a fixture outside the expected temp directory: $resolvedFixture"
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
