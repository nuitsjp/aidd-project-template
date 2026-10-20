$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'scripts/check-coverage.ps1'
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('wpf-coverage-tests-' + [Guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$script:passed = 0

function Test-Report([string]$Name, [string]$Xml, [int]$ExpectedExit,
    [string[]]$Contains = @(), [switch]$MissingFile) {
    $path = Join-Path $temporaryRoot ($Name + '.xml')
    if (-not $MissingFile) { [IO.File]::WriteAllText($path, $Xml, $utf8) }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    # 日本語の出力を OS のコードページに依存せず比較するため、子プロセスに UTF-8 で出力させて UTF-8 で読む。
    $command = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new(`$false); & '{0}' -ReportPath '{1}'" -f
        $scriptPath.Replace("'", "''"), $path.Replace("'", "''")
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
    # 網羅率は合否に使わず表示だけを行う。レポートの形式不正は計測の失敗として扱う。
    Test-Report 'summary' '<coverage lines-covered="22" lines-valid="24"/>' 0 @('全製品: 22/24 lines (91.67%)')
    Test-Report 'low-rate-is-not-failure' '<coverage lines-covered="1" lines-valid="100"/>' 0 @('全製品: 1/100 lines (1.00%)')
    Test-Report 'missing-file' '' 1 -MissingFile
    Test-Report 'malformed-xml' '<coverage' 1
    Test-Report 'zero-root-denominator' '<coverage lines-covered="0" lines-valid="0"/>' 1
    Test-Report 'covered-above-valid' '<coverage lines-covered="2" lines-valid="1"/>' 1
    Test-Report 'empty-xml' '' 1
    Test-Report 'missing-root-count' '<coverage/>' 1
    Test-Report 'missing-coverage-root' '<report lines-covered="1" lines-valid="1"/>' 1
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
