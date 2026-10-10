param([string]$AppRoot = (Join-Path $PSScriptRoot '../app'))

$ErrorActionPreference = 'Stop'
$presentationNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
$errorCount = 0
$checkedCount = 0

try {
    $root = Get-Item -LiteralPath $AppRoot
    if (-not $root.PSIsContainer) { throw 'AppRoot must be a directory.' }
    $rootPrefix = $root.FullName.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar
    $files = @(Get-ChildItem -LiteralPath $root.FullName -Recurse -File -Filter '*.xaml' -Force | Sort-Object FullName)
} catch {
    Write-Output ('{0}(1,1): error WPFXAML003: {1}' -f $AppRoot, $_.Exception.Message)
    exit 1
}

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($rootPrefix.Length).Replace('\', '/')
    if ($relativePath -match '(^|/)(bin|obj)/') { continue }

    $checkedCount++
    $allowsDefinitions = $relativePath -match '^View/Styles/.+\.xaml$'
    $reader = $null
    try {
        $settings = [System.Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [System.Xml.XmlReader]::Create($file.FullName, $settings)
        $lineInfo = [System.Xml.IXmlLineInfo]$reader

        while ($reader.Read()) {
            if ($reader.NodeType -ne [System.Xml.XmlNodeType]::Element -or
                $reader.NamespaceURI -cne $presentationNamespace -or $allowsDefinitions) { continue }

            $code = switch -CaseSensitive ($reader.LocalName) {
                'Style' { 'WPFXAML001' }
                'ControlTemplate' { 'WPFXAML002' }
            }
            if ($code) {
                Write-Output ('{0}({1},{2}): error {3}: {4} definitions are allowed only in View/Styles/**/*.xaml.' -f
                    $relativePath, $lineInfo.LineNumber, $lineInfo.LinePosition, $code, $reader.LocalName)
                $errorCount++
            }
        }
    } catch {
        $exception = $_.Exception
        while ($exception -isnot [System.Xml.XmlException] -and $exception.InnerException) { $exception = $exception.InnerException }
        $line = 1
        $column = 1
        if ($exception -is [System.Xml.XmlException]) {
            $line = [Math]::Max(1, $exception.LineNumber)
            $column = [Math]::Max(1, $exception.LinePosition)
        }
        $message = $exception.Message.Replace("`r", ' ').Replace("`n", ' ')
        Write-Output ('{0}({1},{2}): error WPFXAML003: {3}' -f $relativePath, $line, $column, $message)
        $errorCount++
    } finally {
        if ($null -ne $reader) { $reader.Dispose() }
    }
}

if ($errorCount -gt 0) { exit 1 }
Write-Output ('XAML check passed ({0} files).' -f $checkedCount)
exit 0
