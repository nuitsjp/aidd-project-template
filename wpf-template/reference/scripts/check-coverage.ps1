param(
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [Parameter(Mandatory = $true)][string]$AssemblyName,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

function Get-Count([System.Xml.XmlElement]$Element, [string]$Attribute) {
    [long]$value = 0
    if (-not [long]::TryParse($Element.GetAttribute($Attribute), [ref]$value) -or $value -lt 0) {
        throw "属性 $Attribute は0以上の整数である必要があります。"
    }
    return $value
}

function Format-Rate([long]$Covered, [long]$Valid) {
    return ('{0:F2}%' -f (100.0 * $Covered / $Valid))
}

try {
    Write-Host "Report: $ReportPath"
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        throw "Coberturaレポートがありません: $ReportPath"
    }
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    $document.Load((Resolve-Path -LiteralPath $ReportPath).ProviderPath)
    $coverage = $document.DocumentElement
    if ($coverage.Name -ne 'coverage') { throw 'Coberturaのcoverage要素がありません。' }
    $covered = Get-Count $coverage 'lines-covered'
    $valid = Get-Count $coverage 'lines-valid'
    if ($valid -eq 0 -or $covered -gt $valid) { throw '全製品の行数が不正です。分母は1以上である必要があります。' }
    Write-Host ("全製品: {0}/{1} lines ({2})" -f $covered, $valid, (Format-Rate $covered $valid))
    if (-not $Check) { exit 0 }

    $scopes = @(
        @{ Name = 'ドメイン・ViewModel'; Prefixes = @("$AssemblyName.Domain.", "$AssemblyName.ViewModel."); Minimum = 75; Lines = @{} },
        @{ Name = 'インフラストラクチャ'; Prefixes = @("$AssemblyName.Infrastructure."); Minimum = 95; Lines = @{} }
    )
    foreach ($class in $document.SelectNodes('/coverage/packages/package/classes/class')) {
        foreach ($scope in $scopes) {
            $matches = $false
            foreach ($prefix in $scope.Prefixes) {
                if ($class.GetAttribute('name').StartsWith($prefix, [StringComparison]::Ordinal)) { $matches = $true }
            }
            if (-not $matches) { continue }
            $filename = $class.GetAttribute('filename')
            if ([string]::IsNullOrWhiteSpace($filename)) { throw '対象クラスのfilenameがありません。' }
            foreach ($line in $class.SelectNodes('lines/line')) {
                $number = Get-Count $line 'number'
                $hits = Get-Count $line 'hits'
                if ($number -eq 0) { throw '行番号は1以上である必要があります。' }
                $key = $filename + [char]0 + $number
                $scope.Lines[$key] = ($scope.Lines[$key] -eq $true -or $hits -gt 0)
            }
        }
    }

    $failed = $false
    foreach ($scope in $scopes) {
        $total = $scope.Lines.Count
        $hit = @($scope.Lines.Values | Where-Object { $_ -eq $true }).Count
        $passed = $total -gt 0 -and (100.0 * $hit) -ge ($scope.Minimum * $total)
        $rate = if ($total -gt 0) { Format-Rate $hit $total } else { '対象行なし' }
        $result = if ($passed) { 'OK' } else { 'NG' }
        Write-Host ("{0}: {1}/{2} lines ({3}), 必須 {4}%: {5}" -f $scope.Name, $hit, $total, $rate, $scope.Minimum, $result)
        if (-not $passed) { $failed = $true }
    }
    if ($failed) { exit 1 }
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
