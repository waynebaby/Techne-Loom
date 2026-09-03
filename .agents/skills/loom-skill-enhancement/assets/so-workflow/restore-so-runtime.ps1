[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageLockPath,

    [string]$PackageCacheRoot,

    [string]$RuntimeIdentifier,

    [string]$RuntimeDescriptorPath,

    [switch]$DotNetCliMode,

    [switch]$NoDownload
)

$ErrorActionPreference = 'Stop'
$nodeScript = Join-Path $PSScriptRoot 'scripts\restore-so-runtime.js'
if (-not (Test-Path -LiteralPath $nodeScript -PathType Leaf)) {
    throw "Runtime restore helper '$nodeScript' was not found."
}

$lockPath = (Resolve-Path -LiteralPath $PackageLockPath -ErrorAction Stop).Path
$arguments = @(
    $nodeScript,
    '--package-lock-file',
    $lockPath
)

if (-not [string]::IsNullOrWhiteSpace($PackageCacheRoot)) {
    $arguments += @('--package-cache-root', $PackageCacheRoot)
}

if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $arguments += @('--runtime-identifier', $RuntimeIdentifier)
}

if (-not [string]::IsNullOrWhiteSpace($RuntimeDescriptorPath)) {
    $arguments += @('--runtime-descriptor-file', (Resolve-Path -LiteralPath $RuntimeDescriptorPath -ErrorAction Stop).Path)
}

if ($DotNetCliMode) {
    $arguments += @('--mode', 'dotnet-cli')
} else {
    $arguments += @('--mode', 'self-contained')
}

if ($NoDownload) {
    $arguments += '--no-download'
}

& node @arguments
exit $LASTEXITCODE
