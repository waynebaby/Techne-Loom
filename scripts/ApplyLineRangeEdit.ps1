Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Parse-Arguments {
    param([object[]]$Arguments)

    $options = @{}
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        $argument = [string]$Arguments[$index]
        if ($argument -eq '--') {
            continue
        }
        if (-not $argument.StartsWith('--') -or $index + 1 -ge $Arguments.Count) {
            throw "Expected an option followed by a value, got '$argument'."
        }
        $name = $argument.Substring(2)
        $value = [string]$Arguments[$index + 1]
        $index++
        $normalizedName = $name.ToLowerInvariant()
        if ([string]::IsNullOrEmpty($name) -or $value.StartsWith('--') -or $options.ContainsKey($normalizedName)) {
            throw "Invalid or duplicated option '--$name'."
        }
        $options[$normalizedName] = $value
    }
    return $options
}

function Require-Path {
    param([hashtable]$Options, [string]$Name)

    if (-not $Options.ContainsKey($Name) -or [string]::IsNullOrWhiteSpace([string]$Options[$Name])) {
        throw "Missing required option '--$Name'."
    }
    return [IO.Path]::GetFullPath([string]$Options[$Name])
}

function Has-Utf8Bom {
    param([byte[]]$Bytes)
    return $Bytes.Length -ge 3 -and $Bytes[0] -eq 0xef -and $Bytes[1] -eq 0xbb -and $Bytes[2] -eq 0xbf
}

function Decode-Utf8 {
    param([byte[]]$Bytes, [string]$FilePath)

    $offset = if (Has-Utf8Bom $Bytes) { 3 } else { 0 }
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        return $encoding.GetString($Bytes, $offset, $Bytes.Length - $offset)
    }
    catch {
        throw "File '$FilePath' is not valid UTF-8: $($_.Exception.Message)"
    }
}

function Read-Utf8Bytes {
    param([string]$FilePath)
    return ,([IO.File]::ReadAllBytes($FilePath))
}

function Read-Utf8Text {
    param([string]$FilePath)
    $bytes = Read-Utf8Bytes $FilePath
    return Decode-Utf8 $bytes $FilePath
}

function Normalize-Text {
    param([string]$Text)
    return $Text.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Split-Lines {
    param([string]$Text)

        $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    if ($normalized.Length -eq 0) {
        return @()
    }
    if ($normalized.EndsWith("`n")) {
        $normalized = $normalized.Substring(0, $normalized.Length - 1)
    }
    return @($normalized.Split([char]10))
}

function Detect-Newline {
    param([string]$Text)
    if ($Text.Contains("`r`n")) {
        return "`r`n"
    }
    if ($Text.Contains("`r")) {
        return "`r"
    }
    return "`n"
}

function Get-Trailing-Newline {
    param([string]$Text)
    if ($Text.EndsWith("`r`n")) {
        return "`r`n"
    }
    if ($Text.EndsWith("`n")) {
        return "`n"
    }
    if ($Text.EndsWith("`r")) {
        return "`r"
    }
    return ""
}

function Convert-Newlines {
    param([string]$Text, [string]$Newline)
    if ($Newline -eq "`r`n") {
        return $Text.Replace("`n", "`r`n")
    }
    if ($Newline -eq "`r") {
        return $Text.Replace("`n", "`r")
    }
    return $Text
}

function Boundary-Equals {
    param([string]$Actual, [string]$Expected)
    return $Actual.Trim().ToLowerInvariant() -ceq $Expected.Trim().ToLowerInvariant()
}

function Get-Required-Property {
    param([object]$Object, [string]$PropertyName)
    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        throw "Edit property '$PropertyName' is required."
    }
    return $property.Value
}

function Get-Positive-Integer {
    param([object]$Edit, [string]$PropertyName)

    $value = Get-Required-Property $Edit $PropertyName
    if ($value -is [bool] -or ($value -isnot [int] -and $value -isnot [long] -and $value -isnot [double] -and $value -isnot [decimal])) {
        throw "Edit property '$PropertyName' must be a positive integer."
    }
    $number = [int64]$value
    if ($number -lt 1 -or $number -gt [int32]::MaxValue -or ([double]$value -ne [math]::Truncate([double]$value))) {
        throw "Edit property '$PropertyName' must be a positive integer."
    }
    return [int]$number
}

function Get-Required-Path {
    param([object]$Edit, [string]$PropertyName)

    $value = Get-Required-Property $Edit $PropertyName
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        throw "Edit property '$PropertyName' must be a non-empty path."
    }
    return [IO.Path]::GetFullPath($value)
}

function Read-Boundary {
    param([string]$FilePath)
    $content = Read-Utf8Text $FilePath
    $lines = @(Split-Lines $content)
    if ($lines.Count -eq 0 -and (Normalize-Text $content).Length -eq 0) {
        return ''
    }
    if ($lines.Count -ne 1) {
        throw "Boundary file '$FilePath' must contain exactly one logical line."
    }
    return [string]$lines[0]
}

function Read-Edits {
    param([string]$ManifestPath)

    $manifest = ConvertFrom-Json -InputObject (Read-Utf8Text $ManifestPath)
    if ($null -eq $manifest) {
        throw "The edits manifest must contain a non-empty 'edits' array."
    }
    $editsProperty = $manifest.PSObject.Properties['edits']
    if ($null -eq $editsProperty -or $null -eq $editsProperty.Value) {
        throw "The edits manifest must contain a non-empty 'edits' array."
    }
    $editItems = @($editsProperty.Value)
    if ($editItems.Count -eq 0) {
        throw "The edits manifest must contain a non-empty 'edits' array."
    }

    $edits = @()
    foreach ($edit in $editItems) {
        $startLine = Get-Positive-Integer $edit 'start_line'
        $endLine = Get-Positive-Integer $edit 'end_line'
        if ($endLine -lt $startLine) {
            throw "Invalid edit range $startLine-$endLine."
        }
        $expectedStartPath = Get-Required-Path $edit 'expected_start_file'
        $expectedEndPath = Get-Required-Path $edit 'expected_end_file'
        $replacementPath = Get-Required-Path $edit 'replacement_file'
        $edits += [pscustomobject]@{
            StartLine = $startLine
            EndLine = $endLine
            ExpectedStart = Read-Boundary $expectedStartPath
            ExpectedEnd = Read-Boundary $expectedEndPath
            ReplacementLines = @(Split-Lines (Read-Utf8Text $replacementPath))
        }
    }
    return @($edits)
}

function Validate-Ranges {
    param([object[]]$Edits, [object[]]$OriginalLines, [string]$TargetPath)

    $ordered = @($Edits | Sort-Object -Property StartLine)
    for ($index = 0; $index -lt $ordered.Count; $index++) {
        $edit = $ordered[$index]
        if ($edit.StartLine -gt $OriginalLines.Count -or $edit.EndLine -gt $OriginalLines.Count) {
            throw "Edit range $($edit.StartLine)-$($edit.EndLine) exceeds '$TargetPath' ($($OriginalLines.Count) lines)."
        }
        if (-not (Boundary-Equals $OriginalLines[$edit.StartLine - 1] $edit.ExpectedStart) -or -not (Boundary-Equals $OriginalLines[$edit.EndLine - 1] $edit.ExpectedEnd)) {
            throw "Edit range $($edit.StartLine)-$($edit.EndLine) has mismatched boundary content."
        }
        if ($index -gt 0 -and $ordered[$index - 1].EndLine -ge $edit.StartLine) {
            throw "Edit ranges $($ordered[$index - 1].StartLine)-$($ordered[$index - 1].EndLine) and $($edit.StartLine)-$($edit.EndLine) overlap."
        }
    }
}

function Write-Utf8File {
    param([string]$FilePath, [string]$Text, [bool]$WithBom)

    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    $content = $encoding.GetBytes($Text)
    if ($WithBom) {
        $bytes = New-Object byte[] ($content.Length + 3)
        $bytes[0] = 0xef
        $bytes[1] = 0xbb
        $bytes[2] = 0xbf
        [Array]::Copy($content, 0, $bytes, 3, $content.Length)
    }
    else {
        $bytes = $content
    }
    [IO.File]::WriteAllBytes($FilePath, $bytes)
}

function Validate-Result {
    param([string]$FilePath, [object[]]$ExpectedLines, [string]$ExpectedTrailingNewline, [bool]$ExpectedBom)

    $bytes = Read-Utf8Bytes $FilePath
    $text = Decode-Utf8 $bytes $FilePath
    $actualLines = @(Split-Lines $text)
    if ($actualLines.Count -ne $ExpectedLines.Count -or (Has-Utf8Bom $bytes) -ne $ExpectedBom -or (Get-Trailing-Newline $text) -cne $ExpectedTrailingNewline) {
        throw "Post-write validation failed for '$FilePath'."
    }
    for ($index = 0; $index -lt $ExpectedLines.Count; $index++) {
        if ($actualLines[$index] -cne [string]$ExpectedLines[$index]) {
            throw "Post-write validation failed for '$FilePath'."
        }
    }
}

function Replace-Target {
    param([string]$TemporaryPath, [string]$TargetPath)

    try {
        [IO.File]::Replace($TemporaryPath, $TargetPath, $null)
    }
    catch {
            Move-Item -LiteralPath $TemporaryPath -Destination $TargetPath -Force
    }
}

function Invoke-Main {
    param([object[]]$Arguments)

    $options = Parse-Arguments $Arguments
    $targetPath = Require-Path $options 'target-file'
    $editsManifestPath = Require-Path $options 'edits-file'
    $originalBytes = Read-Utf8Bytes $targetPath
    $original = Decode-Utf8 $originalBytes $targetPath
    $newline = Detect-Newline $original
    $trailingNewline = Get-Trailing-Newline $original
    $originalLines = @(Split-Lines $original)
    $edits = @(Read-Edits $editsManifestPath)
    Validate-Ranges $edits $originalLines $targetPath

    $expectedLines = New-Object 'System.Collections.Generic.List[string]'
    foreach ($line in $originalLines) {
        [void]$expectedLines.Add([string]$line)
    }
    foreach ($edit in @($edits | Sort-Object -Property StartLine -Descending)) {
        $removeCount = $edit.EndLine - $edit.StartLine + 1
        $expectedLines.RemoveRange($edit.StartLine - 1, $removeCount)
        $replacementLines = @($edit.ReplacementLines)
        for ($replacementIndex = 0; $replacementIndex -lt $replacementLines.Count; $replacementIndex++) {
            $expectedLines.Insert($edit.StartLine - 1 + $replacementIndex, [string]$replacementLines[$replacementIndex])
        }
    }

    $updated = Convert-Newlines ([string]::Join("`n", [string[]]$expectedLines)) $newline
    $updated += $trailingNewline
    $temporaryPath = "$targetPath.$([Guid]::NewGuid().ToString('N')).range-edit.tmp"
    try {
        Write-Utf8File $temporaryPath $updated (Has-Utf8Bom $originalBytes)
        Validate-Result $temporaryPath @($expectedLines) $trailingNewline (Has-Utf8Bom $originalBytes)
        Replace-Target $temporaryPath $targetPath
        Validate-Result $targetPath @($expectedLines) $trailingNewline (Has-Utf8Bom $originalBytes)
        Write-Output "Applied $($edits.Count) line-range edits to '$targetPath' from one original read."
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

try {
    Invoke-Main $args
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
