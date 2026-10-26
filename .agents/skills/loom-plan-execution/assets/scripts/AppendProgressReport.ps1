[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TargetFile,
    [Parameter(Mandatory = $true)]
    [string]$EntryFile
)

$ErrorActionPreference = 'Stop'

try {
    $targetPath = [System.IO.Path]::GetFullPath($TargetFile)
    $entryPath = [System.IO.Path]::GetFullPath($EntryFile)

    if ([System.IO.Path]::GetFileName($targetPath) -cne 'progress-report.md') {
        throw 'Target must be named progress-report.md.'
    }
    if ([System.StringComparer]::OrdinalIgnoreCase.Equals($targetPath, $entryPath)) {
        throw 'The entry file and target file must be different.'
    }

    $entryBytes = [System.IO.File]::ReadAllBytes($entryPath)
    if ($entryBytes.Length -eq 0) {
        throw 'Entry file is empty.'
    }

    $utf8 = New-Object -TypeName System.Text.UTF8Encoding -ArgumentList @($false, $true)
    $entryText = $utf8.GetString($entryBytes)
    if (-not $entryText.EndsWith("`n", [System.StringComparison]::Ordinal)) {
        throw 'Entry file must end with a line feed.'
    }

    $targetDirectory = [System.IO.Path]::GetDirectoryName($targetPath)
    if (-not [System.IO.Directory]::Exists($targetDirectory)) {
        throw 'Target directory does not exist.'
    }
    if ([System.IO.File]::Exists($targetPath)) {
        $attributes = [System.IO.File]::GetAttributes($targetPath)
        if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Target cannot be a symbolic link or reparse point.'
        }
    }

    $appendBytes = New-Object byte[] ($entryBytes.Length + 1)
    $appendBytes[0] = 10
    [System.Array]::Copy($entryBytes, 0, $appendBytes, 1, $entryBytes.Length)

    $stream = [System.IO.File]::Open(
        $targetPath,
        [System.IO.FileMode]::Append,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::Read
    )
    try {
        $stream.Write($appendBytes, 0, $appendBytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }

    $urlPath = $targetPath.Replace('%', '%25').Replace(' ', '%20').Replace('#', '%23').Replace('?', '%3F').Replace('\', '/')
    if (-not $urlPath.StartsWith('/')) {
        $urlPath = '/' + $urlPath
    }
    Write-Output ('file://' + $urlPath)
}
catch {
    [System.Console]::Error.WriteLine('AppendProgressReport.ps1: ' + $_.Exception.Message)
    exit 1
}
