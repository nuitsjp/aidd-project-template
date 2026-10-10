$ErrorActionPreference = 'Stop'
$checkerPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../scripts/check-xaml.ps1')).ProviderPath
$powershellPath = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixtureRoot = [IO.Path]::GetFullPath((Join-Path $tempRoot ('wpf-xaml-lint-' + [Guid]::NewGuid().ToString('N'))))
$utf8 = [Text.UTF8Encoding]::new($true)
$passed = 0

function New-Fixture([string]$Name, [hashtable]$Files) {
    $appRoot = Join-Path $fixtureRoot "$Name/app"
    New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
    foreach ($relativePath in $Files.Keys) {
        $path = Join-Path $appRoot $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
        [IO.File]::WriteAllText($path, $Files[$relativePath], $utf8)
    }
    return $appRoot
}

# 日本語の診断を OS のコードページに依存せず比較するため、子プロセスに UTF-8 で出力させて UTF-8 で読む。
function Invoke-Checker([string]$AppRoot) {
    $command = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new(`$false); " +
        "& '$($checkerPath.Replace("'", "''"))' -AppRoot '$($AppRoot.Replace("'", "''"))'; exit `$LASTEXITCODE"
    $start = [Diagnostics.ProcessStartInfo]::new($powershellPath)
    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -Command "' + $command + '"'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $start.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    $process = [Diagnostics.Process]::Start($start)
    try {
        $errorRead = $process.StandardError.ReadToEndAsync()
        $output = $process.StandardOutput.ReadToEnd() + $errorRead.Result
        $process.WaitForExit()
        return [PSCustomObject]@{ ExitCode = $process.ExitCode; Output = $output.Replace("`r`n", "`n").TrimEnd("`n") }
    }
    finally { $process.Dispose() }
}

function Assert-Result($Result, [int]$ExitCode, [string[]]$Diagnostics = @()) {
    if ($Result.ExitCode -ne $ExitCode) {
        throw "Expected exit $ExitCode, got $($Result.ExitCode). Output: $($Result.Output)"
    }
    $actualDiagnostics = @($Result.Output -split "`n" | Where-Object { $_ -match ': error WPFXAML\d{3}:' })
    if ($actualDiagnostics.Count -ne $Diagnostics.Count) {
        throw "Expected $($Diagnostics.Count) diagnostics, got $($actualDiagnostics.Count). Output: $($Result.Output)"
    }
    for ($index = 0; $index -lt $Diagnostics.Count; $index++) {
        if (-not $actualDiagnostics[$index].StartsWith($Diagnostics[$index], [StringComparison]::Ordinal)) {
            throw "Expected diagnostic '$($Diagnostics[$index])', got '$($actualDiagnostics[$index])'."
        }
    }
}

function Pass([string]$Name) {
    $script:passed++
    Write-Output "PASS $Name"
}

try {
    $app = New-Fixture 'prefix with spaces' @{ 'View/画面 Prefix.xaml' = @'
<ResourceDictionary xmlns:w="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <w:Style TargetType="Button" />
</ResourceDictionary>
'@ }
    Assert-Result (Invoke-Checker $app) 1 @('View/画面 Prefix.xaml(2,4): error WPFXAML001:')
    Pass 'prefixed WPF Style and relative path with spaces/Japanese'

    $app = New-Fixture 'inline-style' @{ 'View/Note.xaml' = @'
<Button xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <Button.Style>
    <Style TargetType="Button" />
  </Button.Style>
</Button>
'@ }
    Assert-Result (Invoke-Checker $app) 1 @('View/Note.xaml(3,6): error WPFXAML001:')
    Pass 'inline Button.Style definition'

    $app = New-Fixture 'inline-template' @{ 'View/Note.xaml' = @'
<Control xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <Control.Template>
    <ControlTemplate TargetType="Control" />
  </Control.Template>
</Control>
'@ }
    Assert-Result (Invoke-Checker $app) 1 @('View/Note.xaml(3,6): error WPFXAML002:')
    Pass 'inline Control.Template definition'

    $app = New-Fixture 'app-resources' @{ 'App.xaml' = @'
<Application xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <Application.Resources>
    <ResourceDictionary>
      <Style TargetType="Button" />
      <ControlTemplate TargetType="Control" />
    </ResourceDictionary>
  </Application.Resources>
</Application>
'@ }
    Assert-Result (Invoke-Checker $app) 1 @('App.xaml(4,8): error WPFXAML001:', 'App.xaml(5,8): error WPFXAML002:')
    Pass 'definitions inside App ResourceDictionary remain forbidden'

    $app = New-Fixture 'other-namespaces' @{ 'View/Note.xaml' = @'
<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:other="urn:test" xmlns:lookalike="http://schemas.microsoft.com/winfx/2006/xaml/Presentation">
  <other:Style /><other:ControlTemplate />
  <Style xmlns="" /><ControlTemplate xmlns="" />
  <lookalike:Style /><lookalike:ControlTemplate />
  <style /><controlTemplate />
</UserControl>
'@ }
    Assert-Result (Invoke-Checker $app) 0
    Pass 'namespace and element names are matched exactly'

    $app = New-Fixture 'comments' @{ 'View/Note.xaml' = @'
<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <!-- <Style /><ControlTemplate /> -->
  <TextBlock Text="&lt;Style /&gt; 日本語" />
</UserControl>
'@ }
    Assert-Result (Invoke-Checker $app) 0
    Pass 'comments and escaped text are ignored'

    $common = @'
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <Style TargetType="Button" />
  <ControlTemplate TargetType="Control" />
</ResourceDictionary>
'@
    $app = New-Fixture 'common-styles' @{ 'View/Styles/Base.xaml' = $common; 'View/Styles/Controls/Button.xaml' = $common }
    Assert-Result (Invoke-Checker $app) 0
    Pass 'common definitions and nested style files are allowed'

    $app = New-Fixture 'similar-directory' @{ 'View/Styles-copy/Button.xaml' = @'
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button" />
'@ }
    Assert-Result (Invoke-Checker $app) 1 @('View/Styles-copy/Button.xaml(1,2): error WPFXAML001:')
    Pass 'similarly named directories do not bypass the restriction'

    $app = New-Fixture 'references' @{ 'View/Note.xaml' = @'
<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <Button Style="{StaticResource ButtonStyle}" Template="{StaticResource ButtonTemplate}" />
  <Control>
    <Control.Template><StaticResource ResourceKey="ControlTemplate" /></Control.Template>
  </Control>
</UserControl>
'@ }
    Assert-Result (Invoke-Checker $app) 0
    Pass 'Style and Template resource references are allowed'

    $broken = @'
<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
  <Grid>
</UserControl>
'@
    $app = New-Fixture 'invalid-xml' @{ 'View/Broken.xaml' = $broken }
    Assert-Result (Invoke-Checker $app) 1 @('View/Broken.xaml(3,3): error WPFXAML003:')
    Pass 'malformed XML reports its relative path and parser position'

    $app = New-Fixture 'invalid-common-xml' @{ 'View/Styles/Broken.xaml' = $broken }
    Assert-Result (Invoke-Checker $app) 1 @('View/Styles/Broken.xaml(3,3): error WPFXAML003:')
    Pass 'common files must still be valid XML'

    $app = New-Fixture 'generated-files' @{
        'View/Note.xaml' = '<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" />'
        'bin/Debug/Generated.xaml' = $broken
        'obj/Generated.xaml' = $common
        'View/bin/Generated.xaml' = $common
    }
    $result = Invoke-Checker $app
    Assert-Result $result 0
    if ($result.Output -ne 'XAML check passed (1 files).') { throw "Unexpected generated-file count: $($result.Output)" }
    Pass 'bin and obj files are excluded at every directory depth'

    Write-Output "XAML checker regression tests passed ($passed cases)."
} finally {
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
