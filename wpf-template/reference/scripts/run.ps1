param([Parameter(Mandatory = $true)][string]$Task)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$solution = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.slnx' -File)[0].FullName
$appProject = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'app') -Filter '*.csproj' -File)[0]
$assemblyName = ([xml](Get-Content -LiteralPath $appProject.FullName -Raw)).Project.PropertyGroup.AssemblyName | Where-Object { $_ }

function Invoke-Native([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command exited with code $LASTEXITCODE" }
}

function Build-App([string]$Configuration) {
    Invoke-Native 'dotnet' @('build', $appProject.FullName, '-c', $Configuration, '-p:RestoreLockedMode=true')
}

# ビルドを完了してから、動的計測の同一セッションでテストとその子アプリを収集する。
function Test-Projects([string]$Suite, [switch]$CheckCoverage) {
    if ($Suite -eq 'all') {
        Invoke-Native 'dotnet' @('build', $solution, '-c', 'Release', '-p:RestoreLockedMode=true')
    } else {
        $testProject = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot "tests/$Suite") -Filter '*.csproj' -File)[0].FullName
        Invoke-Native 'dotnet' @('build', $testProject, '-c', 'Release', '-p:RestoreLockedMode=true')
    }
    if ($Suite -in @('all', 'e2e')) { Build-App 'Mock' }
    Invoke-Native 'dotnet' @('tool', 'restore')
    $results = Join-Path $projectRoot ('TestResults/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $results | Out-Null
    $settingsPath = Join-Path $results 'coverage.settings.xml'
    $reportPath = Join-Path $results 'coverage.cobertura.xml'
    $modulePattern = [Security.SecurityElement]::Escape('.*[\\/]' + [regex]::Escape($assemblyName) + '\.exe$')
    $settings = @"
<Configuration><CodeCoverage>
  <ModulePaths><Include><ModulePath>$modulePattern</ModulePath></Include>
    <Exclude><ModulePath>.*[\\/]Mock[\\/].*</ModulePath></Exclude></ModulePaths>
  <Sources><Exclude><Source>.*[\\/]obj[\\/].*</Source><Source>.*[\\/]InMemoryNotesService\.cs$</Source></Exclude></Sources>
  <Attributes><Exclude><Attribute>^System.CodeDom.Compiler.GeneratedCodeAttribute$</Attribute></Exclude></Attributes>
  <CollectFromChildProcesses>True</CollectFromChildProcesses>
  <EnableDynamicManagedInstrumentation>True</EnableDynamicManagedInstrumentation>
  <EnableStaticManagedInstrumentation>False</EnableStaticManagedInstrumentation>
  <EnableDynamicNativeInstrumentation>False</EnableDynamicNativeInstrumentation>
  <EnableStaticNativeInstrumentation>False</EnableStaticNativeInstrumentation>
  <SkipAutoProperties>False</SkipAutoProperties>
</CodeCoverage></Configuration>
"@
    [IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))
    & dotnet coverage collect --settings $settingsPath --output $reportPath --output-format cobertura `
        powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-tests.ps1') -Suite $Suite -ResultsPath $results
    $testExitCode = $LASTEXITCODE
    $checkArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'check-coverage.ps1'),
        '-ReportPath', $reportPath, '-AssemblyName', $assemblyName)
    if ($CheckCoverage) { $checkArguments += '-Check' }
    & powershell @checkArguments
    $coverageExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) { throw "Test collection exited with code $testExitCode. Results: $results" }
    if ($coverageExitCode -ne 0) { throw "Coverage check exited with code $coverageExitCode. Results: $results" }
}

# C# は dotnet format、XAML は XamlStyler で整形する。-Check は変更せず、整形漏れがあれば失敗する。
function Format-Code([switch]$Check) {
    Invoke-Native 'dotnet' @('tool', 'restore')
    $codeArguments = @('format', $solution)
    if ($Check) { $codeArguments += '--verify-no-changes' }
    Invoke-Native 'dotnet' $codeArguments
    $xamlArguments = @('xstyler', '-c', (Join-Path $projectRoot 'Settings.XamlStyler'), '-d', (Join-Path $projectRoot 'app'), '-r', '-l', 'Minimal')
    if ($Check) { $xamlArguments += '--passive' }
    Invoke-Native 'dotnet' $xamlArguments
}

function Verify {
    & (Join-Path $PSScriptRoot 'check-docs.ps1')
    Format-Code -Check
    Invoke-Native 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'check-xaml.ps1'))
    $checksResults = Join-Path $projectRoot ('TestResults/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $checksResults | Out-Null
    $stdout = Join-Path $checksResults 'checks.stdout.log'
    $stderr = Join-Path $checksResults 'checks.stderr.log'
    $checksProcess = $null
    $testFailure = $null
    $checksExitCode = 0
    try {
        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
            ('"{0}"' -f (Join-Path $PSScriptRoot 'run-tests.ps1')), '-Suite', 'checks', '-ResultsPath', ('"{0}"' -f $checksResults))
        $powershellPath = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
        $checksProcess = Start-Process -FilePath $powershellPath -ArgumentList $arguments -WorkingDirectory $projectRoot `
            -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        $null = $checksProcess.Handle
        # 回帰検証は専用の一時領域だけを使い、製品のビルド・テストと並走する。
        Test-Projects 'all' -CheckCoverage
    } catch {
        $testFailure = $_
    } finally {
        if ($null -ne $checksProcess) {
            $checksProcess.WaitForExit()
            Get-Content -LiteralPath $stdout
            Get-Content -LiteralPath $stderr
            $checksExitCode = $checksProcess.ExitCode
            $checksProcess.Dispose()
        }
    }
    if ($checksExitCode -ne 0) {
        $message = "Regression checks exited with code $checksExitCode. Results: $checksResults"
        if ($null -ne $testFailure) { Write-Output $message } else { throw $message }
    }
    if ($null -ne $testFailure) { throw $testFailure }
}

switch ($Task) {
    'setup' {
        $frameworkRelease = Get-ItemPropertyValue -LiteralPath 'HKLM:/SOFTWARE/Microsoft/NET Framework Setup/NDP/v4/Full' -Name Release
        if ($frameworkRelease -lt 533320) { throw '.NET Framework 4.8.1 runtime is required.' }
        if (-not (Test-Path -LiteralPath "${env:ProgramFiles(x86)}/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8.1")) {
            throw '.NET Framework 4.8.1 Developer Pack is required.'
        }
        Invoke-Native 'mise' @('install')
        Invoke-Native 'dotnet' @('restore', $solution, '--locked-mode')
        Invoke-Native 'dotnet' @('tool', 'restore')
    }
    'dev' {
        Build-App 'Debug'
        Invoke-Native (Join-Path $projectRoot "app/bin/Debug/net481/$assemblyName.exe") @('--data-dir', (Join-Path $projectRoot 'dev-data'))
    }
    'dev:mock' {
        Build-App 'Mock'
        Invoke-Native (Join-Path $projectRoot "app/bin/Mock/net481/$assemblyName.exe") @('--mock')
    }
    'build' { Build-App 'Release' }
    'format' { Format-Code }
    'check:format' { Format-Code -Check }
    'check:xaml' { Invoke-Native 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'check-xaml.ps1')) }
    'test:xaml' { Invoke-Native 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $projectRoot 'tests/xaml/check-xaml.tests.ps1')) }
    'test:unit' { Test-Projects 'unit' }
    'test:integration' { Test-Projects 'integration' }
    'test:e2e' { Test-Projects 'e2e' }
    'verify' { Verify }
    'package' {
        $releaseRoot = Join-Path $projectRoot 'release'
        if (Test-Path -LiteralPath $releaseRoot) { throw 'release already exists. Move it before creating a new package.' }
        Verify
        $appOutput = Join-Path $projectRoot "app/bin/Release/net481"
        $packageDirectory = Join-Path $releaseRoot 'app'
        New-Item -ItemType Directory -Path $packageDirectory | Out-Null
        Get-ChildItem -LiteralPath $appOutput -Force | Where-Object { $_.Extension -ne '.pdb' } |
            Copy-Item -Destination $packageDirectory -Recurse
        $isSource = (Split-Path -Leaf (Split-Path -Parent $projectRoot)) -eq 'wpf-template'
        $licensePath = if ($isSource) { Join-Path $projectRoot '../../LICENSE' }
            elseif ((Split-Path -Leaf $projectRoot) -eq 'reference') { Join-Path $projectRoot '../LICENSE' }
            else { Join-Path $projectRoot 'LICENSE' }
        Copy-Item -LiteralPath $licensePath -Destination (Join-Path $packageDirectory 'LICENSE')
        Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Destination $packageDirectory
        Compress-Archive -LiteralPath $packageDirectory -DestinationPath (Join-Path $releaseRoot "$assemblyName.zip")
        Write-Output "Package: $releaseRoot"
    }
    default { throw "Unknown task: $Task" }
}
