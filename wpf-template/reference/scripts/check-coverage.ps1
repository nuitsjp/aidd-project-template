param(
    [Parameter(Mandatory = $true)][string]$ReportPath
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
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
