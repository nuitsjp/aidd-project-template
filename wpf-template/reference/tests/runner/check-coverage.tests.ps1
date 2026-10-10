$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'scripts/check-coverage.ps1'
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('wpf-coverage-tests-' + [Guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$script:passed = 0

function New-Class([string]$Name, [string]$Filename, [int[]]$Hits) {
    $lines = for ($index = 0; $index -lt $Hits.Count; $index++) {
        '<line number="{0}" hits="{1}" />' -f ($index + 1), $Hits[$index]
    }
    return '<class name="{0}" filename="{1}"><lines>{2}</lines></class>' -f $Name, $Filename, ($lines -join '')
}

function New-Report([string]$Assembly = 'WpfNotesSample', [int]$DomainCovered = 3,
    [int]$InfrastructureCovered = 19, [switch]$Duplicate, [switch]$MissingDomain, [switch]$MissingInfrastructure) {
    $classes = @()
    if (-not $MissingDomain) {
        $domainHits = @(1..4 | ForEach-Object { if ($_ -le $DomainCovered) { 1 } else { 0 } })
        if ($Duplicate) {
            $domainHits[0] = 0
            $classes += New-Class "$Assembly.ViewModel.NoteEditViewModel" 'ViewModel/NoteEditViewModel.cs' $domainHits
            $classes += '<class name="' + $Assembly + '.ViewModel.NoteEditViewModel/&lt;SaveAsync&gt;d__1" filename="ViewModel/NoteEditViewModel.cs"><lines><line number="1" hits="1"/><line number="2" hits="0"/></lines></class>'
        }
        else {
            $classes += New-Class "$Assembly.Domain.Notes.Note" 'Domain/Notes/Note.cs' $domainHits
        }
    }
    if (-not $MissingInfrastructure) {
        $infrastructureHits = @(1..20 | ForEach-Object { if ($_ -le $InfrastructureCovered) { 1 } else { 0 } })
        $classes += New-Class "$Assembly.Infrastructure.Sqlite.Notes.NotesService" 'Infrastructure/NotesService.cs' $infrastructureHits
    }
    return '<coverage lines-covered="22" lines-valid="24"><packages><package><classes>{0}</classes></package></packages></coverage>' -f ($classes -join '')
}

function Test-Report([string]$Name, [string]$Xml, [int]$ExpectedExit,
    [string[]]$Contains = @(), [string]$Assembly = 'WpfNotesSample', [switch]$WithoutCheck, [switch]$MissingFile) {
    $path = Join-Path $temporaryRoot ($Name + '.xml')
    if (-not $MissingFile) { [IO.File]::WriteAllText($path, $Xml, $utf8) }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    # 日本語の出力を OS のコードページに依存せず比較するため、子プロセスに UTF-8 で出力させて UTF-8 で読む。
    $command = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new(`$false); & '{0}' -ReportPath '{1}' -AssemblyName '{2}'" -f
        $scriptPath.Replace("'", "''"), $path.Replace("'", "''"), $Assembly.Replace("'", "''")
    if (-not $WithoutCheck) { $command += ' -Check' }
    $command += '; exit $LASTEXITCODE'
    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -Command "' + $command + '"'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = $utf8
    $start.StandardErrorEncoding = $utf8
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        $process.Start() | Out-Null
        $output = $process.StandardOutput.ReadToEnd() + $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne $ExpectedExit) {
            throw "$Name : 期待終了コード $ExpectedExit、実際 $($process.ExitCode)`n$output"
        }
        foreach ($text in $Contains) {
            if (-not $output.Contains($text)) { throw "$Name : 出力に '$text' がありません。`n$output" }
        }
        if (-not $output.Contains($path)) { throw "$Name : レポートのパスが表示されません。`n$output" }
        $script:passed++
        Write-Host "PASS: $Name"
    }
    finally { $process.Dispose() }
}

try {
    Test-Report 'threshold-boundaries' (New-Report) 0 @('3/4 lines (75.00%)', '19/20 lines (95.00%)', '全製品: 22/24 lines (91.67%)')
    Test-Report 'duplicate-line-or-and-generated-class' (New-Report -Duplicate) 0 @('3/4 lines (75.00%)', '19/20 lines (95.00%)')
    Test-Report 'domain-below-threshold' (New-Report -DomainCovered 2) 1 @('2/4 lines (50.00%)', '必須 75%: NG', '必須 95%: OK')
    Test-Report 'infrastructure-below-threshold' (New-Report -InfrastructureCovered 18) 1 @('必須 75%: OK', '18/20 lines (90.00%)', '必須 95%: NG')
    Test-Report 'all-uncovered' (New-Report -DomainCovered 0 -InfrastructureCovered 0) 1 @('0/4 lines (0.00%)', '0/20 lines (0.00%)')
    Test-Report 'missing-domain' (New-Report -MissingDomain) 1 @('対象行なし', '必須 75%: NG', '必須 95%: OK')
    Test-Report 'missing-infrastructure' (New-Report -MissingInfrastructure) 1 @('必須 75%: OK', '対象行なし', '必須 95%: NG')
    Test-Report 'missing-file' '' 1 -MissingFile
    Test-Report 'malformed-xml' '<coverage' 1
    Test-Report 'zero-root-denominator' '<coverage lines-covered="0" lines-valid="0"/>' 1 -WithoutCheck
    Test-Report 'empty-xml' '' 1
    Test-Report 'missing-root-count' '<coverage/>' 1
    Test-Report 'missing-coverage-root' '<report lines-covered="1" lines-valid="1"/>' 1
    Test-Report 'summary-only-low-rate' '<coverage lines-covered="1" lines-valid="100"/>' 0 @('全製品: 1/100 lines (1.00%)') -WithoutCheck
    Test-Report 'renamed-assembly' (New-Report -Assembly 'Renamed.Product') 0 @('必須 75%: OK', '必須 95%: OK') -Assembly 'Renamed.Product'
    Test-Report 'assembly-mismatch' (New-Report -Assembly 'Other.Product') 1 @('必須 75%: NG', '必須 95%: NG')
    Write-Host "カバレッジ検査の回帰テスト: $script:passed 件成功"
}
finally {
    $resolved = [IO.Path]::GetFullPath($temporaryRoot)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $expectedParent -or
        [IO.Path]::GetFileName($resolved) -notlike 'wpf-coverage-tests-*') {
        throw 'テストの一時保存領域以外は削除できません。'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
