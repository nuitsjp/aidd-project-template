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

function Test-Project([string]$Directory) {
    $testProject = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot "tests/$Directory") -Filter '*.csproj' -File)[0].FullName
    Invoke-Native 'dotnet' @('test', '--project', $testProject, '-c', 'Release', '-p:RestoreLockedMode=true')
}

function Test-Ui {
    Build-App 'Mock'
    $previousAppPath = $env:WPF_APP_PATH
    $previousMockAppPath = $env:WPF_MOCK_APP_PATH
    try {
        $env:WPF_APP_PATH = Join-Path $projectRoot "app/bin/Release/net481/$assemblyName.exe"
        $env:WPF_MOCK_APP_PATH = Join-Path $projectRoot "app/bin/Mock/net481/$assemblyName.exe"
        Test-Project 'e2e'
    } finally {
        $env:WPF_APP_PATH = $previousAppPath
        $env:WPF_MOCK_APP_PATH = $previousMockAppPath
    }
}

function Verify {
    & (Join-Path $PSScriptRoot 'check-docs.ps1')
    Invoke-Native 'dotnet' @('build', $solution, '-c', 'Release', '-p:RestoreLockedMode=true')
    Test-Project 'unit'
    Test-Project 'integration'
    Test-Ui
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
    }
    'dev' {
        Build-App 'Debug'
        Invoke-Native (Join-Path $projectRoot "app/bin/Debug/net481/$assemblyName.exe") @()
    }
    'dev:mock' {
        Build-App 'Mock'
        Invoke-Native (Join-Path $projectRoot "app/bin/Mock/net481/$assemblyName.exe") @('--mock')
    }
    'build' { Build-App 'Release' }
    'test:unit' { Test-Project 'unit' }
    'test:integration' { Test-Project 'integration' }
    'test:e2e' {
        Build-App 'Release'
        Test-Ui
    }
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
        Compress-Archive -LiteralPath $packageDirectory -DestinationPath (Join-Path $releaseRoot "$assemblyName.zip")
        Write-Output "Package: $releaseRoot"
    }
    default { throw "Unknown task: $Task" }
}
