$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$isSource = (Split-Path -Leaf (Split-Path -Parent $projectRoot)) -eq 'wpf-template' -and
    (Test-Path -LiteralPath (Join-Path $projectRoot '../../scripts/init-template.mjs'))

function Check-Documents([string]$Checker, [string]$Root) {
    & python $Checker $Root
    if ($LASTEXITCODE -ne 0) { throw "Document check failed: $Root" }
}

if ($isSource) {
    $temporaryBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $temporary = Join-Path $temporaryBase ('aidd-wpf-docs-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporary | Out-Null
    try {
        $generatedProject = Join-Path $temporary 'app'
        & node (Join-Path $projectRoot '../../scripts/init-template.mjs') wpf $generatedProject --name Template.Project
        if ($LASTEXITCODE -ne 0) { throw 'Template generation failed.' }
        $checker = Join-Path $generatedProject 'scripts/doc_check.py'
        Check-Documents $checker $generatedProject
        Check-Documents $checker (Join-Path $generatedProject 'reference')
    } finally {
        $resolvedTemporary = [IO.Path]::GetFullPath($temporary)
        if (-not $resolvedTemporary.StartsWith($temporaryBase, [StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolvedTemporary) -notlike 'aidd-wpf-docs-*') {
            throw 'Temporary directory is outside the expected location.'
        }
        Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
    }
} elseif ((Split-Path -Leaf $projectRoot) -eq 'reference') {
    Check-Documents (Join-Path $projectRoot '../scripts/doc_check.py') $projectRoot
} else {
    $checker = Join-Path $projectRoot 'scripts/doc_check.py'
    Check-Documents $checker $projectRoot
    Check-Documents $checker (Join-Path $projectRoot 'reference')
}
