[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageLockPath,

    [string]$PackageCacheRoot,

    [string]$NuGetBaseUrl = 'https://www.nuget.org/api/v2/package',

    [switch]$NoDownload
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$ExpectedPackageIds = @(
    'Techne.Loom.SkillOrchestrator',
    'Techne.Loom.Common',
    'Techne.Loom.Abstractions'
)

function Get-ExactCacheRoot {
    param([string]$RequestedRoot)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return [System.IO.Path]::GetFullPath($RequestedRoot)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        return [System.IO.Path]::GetFullPath($env:NUGET_PACKAGES)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $HOME '.nuget\packages'))
}

function Get-ExpectedPackagePath {
    param(
        [string]$CacheRoot,
        [string]$PackageId,
        [string]$Version
    )

    $packageDirectory = Join-Path (Join-Path $CacheRoot $PackageId.ToLowerInvariant()) $Version.ToLowerInvariant()
    return Join-Path $packageDirectory "$PackageId.$Version.nupkg"
}

function Get-ExactPackageCandidates {
    param(
        [string]$CacheRoot,
        [string]$PackageId,
        [string]$Version
    )

    $expectedFileName = "$PackageId.$Version.nupkg"
    $normalizedFileName = "$($PackageId.ToLowerInvariant()).$($Version.ToLowerInvariant()).nupkg"
    $packageDirectory = Join-Path (Join-Path $CacheRoot $PackageId.ToLowerInvariant()) $Version.ToLowerInvariant()
    $candidatePaths = [System.Collections.Generic.List[string]]::new()

    foreach ($path in @(
        (Join-Path $packageDirectory $expectedFileName),
        (Join-Path $packageDirectory $normalizedFileName),
        (Join-Path $CacheRoot $expectedFileName),
        (Join-Path $CacheRoot $normalizedFileName)
    )) {
        if (-not $candidatePaths.Contains($path) -and (Test-Path -LiteralPath $path -PathType Leaf)) {
            $candidatePaths.Add([System.IO.Path]::GetFullPath($path))
        }
    }

    if (Test-Path -LiteralPath $packageDirectory -PathType Container) {
        foreach ($file in @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nupkg' -File)) {
            if (-not $candidatePaths.Contains($file.FullName)) {
                $candidatePaths.Add($file.FullName)
            }
        }
    }

    return $candidatePaths.ToArray()
}

function Test-ExactNupkg {
    param(
        [string]$PackagePath,
        [string]$ExpectedPackageId,
        [string]$ExpectedVersion
    )

    if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        return [pscustomobject]@{
            valid = $false
            path = $PackagePath
            reason = 'file-not-found'
        }
    }

    $packageStream = $null
    $archive = $null
    try {
        $packageStream = [System.IO.File]::OpenRead($PackagePath)
        $archive = [System.IO.Compression.ZipArchive]::new(
            $packageStream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
        $nuspecEntry = $archive.Entries |
            Where-Object { $_.FullName -match '(^|/)[^/]+\.nuspec$' } |
            Select-Object -First 1
        if ($null -eq $nuspecEntry) {
            return [pscustomobject]@{
                valid = $false
                path = $PackagePath
                reason = 'nuspec-not-found'
            }
        }

        $reader = $null
        try {
            $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
            $document = [xml]$reader.ReadToEnd()
        }
        finally {
            if ($null -ne $reader) {
                $reader.Dispose()
            }
        }

        $metadata = $document.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadata) {
            return [pscustomobject]@{
                valid = $false
                path = $PackagePath
                reason = 'nuspec-metadata-not-found'
            }
        }

        $idNode = $metadata.SelectSingleNode("./*[local-name()='id']")
        $versionNode = $metadata.SelectSingleNode("./*[local-name()='version']")
        if ($null -eq $idNode -or $null -eq $versionNode) {
            return [pscustomobject]@{
                valid = $false
                path = $PackagePath
                reason = 'nuspec-id-or-version-not-found'
            }
        }

        $actualPackageId = $idNode.InnerText.Trim()
        $actualVersion = $versionNode.InnerText.Trim()
        if (-not [string]::Equals($actualPackageId, $ExpectedPackageId, [System.StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject]@{
                valid = $false
                path = $PackagePath
                reason = "package-id-mismatch:$actualPackageId"
            }
        }

        if (-not [string]::Equals($actualVersion, $ExpectedVersion, [System.StringComparison]::Ordinal)) {
            return [pscustomobject]@{
                valid = $false
                path = $PackagePath
                reason = "version-mismatch:$actualVersion"
            }
        }

        return [pscustomobject]@{
            valid = $true
            path = [System.IO.Path]::GetFullPath($PackagePath)
            package_id = $actualPackageId
            version = $actualVersion
            reason = 'package-id-version-and-nuspec-valid'
        }
    }
    catch {
        return [pscustomobject]@{
            valid = $false
            path = $PackagePath
            reason = "invalid-zip:$($_.Exception.Message)"
        }
    }
    finally {
        if ($null -ne $archive) {
            $archive.Dispose()
        }
        if ($null -ne $packageStream) {
            $packageStream.Dispose()
        }
    }
}

function Find-ValidExactPackage {
    param(
        [string]$CacheRoot,
        [string]$PackageId,
        [string]$Version
    )

    $invalidCandidates = [System.Collections.Generic.List[object]]::new()
    foreach ($candidate in @(Get-ExactPackageCandidates $CacheRoot $PackageId $Version)) {
        $validation = Test-ExactNupkg $candidate $PackageId $Version
        if ($validation.valid) {
            return [pscustomobject]@{
                valid = $true
                package = $validation
                invalid_candidates = @($invalidCandidates)
            }
        }

        $invalidCandidates.Add($validation)
    }

    return [pscustomobject]@{
        valid = $false
        package = $null
        invalid_candidates = @($invalidCandidates)
    }
}

function Write-CacheInvalidResult {
    param(
        [string]$ResolvedVersion,
        [object[]]$Bundle,
        [object[]]$CacheMisses,
        [object[]]$PackageResults
    )

    $result = [pscustomobject]@{
        status = 'package_cache_invalid'
        resolved_runtime_version = $ResolvedVersion
        runtime_bundle_packages = @($Bundle | ForEach-Object { [string]$_.package_id })
        cache_hit = $false
        downloaded_packages = @()
        cache_validation = [pscustomobject]@{
            status = 'failed'
            reason = 'dotnet-cli-runtime-bundle-missing-or-invalid'
            misses = @($CacheMisses)
        }
        package_results = @($PackageResults)
    }
    Write-Output ($result | ConvertTo-Json -Depth 10)
}

if (-not (Test-Path -LiteralPath $PackageLockPath -PathType Leaf)) {
    throw "Package lock file '$PackageLockPath' was not found."
}

$lock = Get-Content -LiteralPath $PackageLockPath -Raw | ConvertFrom-Json
$resolvedVersion = [string]$lock.resolved_version
if ([string]::IsNullOrWhiteSpace($resolvedVersion)) {
    throw 'Package lock must declare a non-empty resolved_version.'
}

$runtimeRestore = $lock.runtime_restore
if ($null -eq $runtimeRestore) {
    throw 'Package lock must declare runtime_restore cache policy.'
}

if ($runtimeRestore.reuse_exact_local_bundle_when_valid -ne $true -or
    $runtimeRestore.download_exact_locked_version_when_missing_or_invalid -ne $true -or
    $runtimeRestore.never_float_to_latest -ne $true) {
    throw 'Package lock runtime_restore must enable exact-cache reuse, exact-version fallback download, and never-float-to-latest protection.'
}

# The skill lock owns only the exact version. The resolver owns the fixed SO dependency family.
$bundle = @($ExpectedPackageIds | ForEach-Object {
    [pscustomobject]@{
        package_id = $_
        resolved_version = $resolvedVersion
    }
})
$cacheRoot = Get-ExactCacheRoot $PackageCacheRoot
$null = New-Item -ItemType Directory -Force -Path $cacheRoot
$packageResults = [System.Collections.Generic.List[object]]::new()
$cacheMisses = [System.Collections.Generic.List[object]]::new()

# Inspect every expected package before the first network request.
foreach ($member in $bundle) {
    $packageId = [string]$member.package_id
    $cached = Find-ValidExactPackage $cacheRoot $packageId $resolvedVersion
    if ($cached.valid) {
        $packageResults.Add([pscustomobject]@{
            package_id = $packageId
            resolved_version = $resolvedVersion
            cache_status = 'reused'
            path = $cached.package.path
            validation = $cached.package.reason
        })
        continue
    }

    $miss = [pscustomobject]@{
        package_id = $packageId
        resolved_version = $resolvedVersion
        invalid_candidates = $cached.invalid_candidates
    }
    $cacheMisses.Add($miss)
    $packageResults.Add([pscustomobject]@{
        package_id = $packageId
        resolved_version = $resolvedVersion
        cache_status = 'missing_or_invalid'
        validation = 'complete-bundle-inspection-failed'
    })
}

$initialCacheHit = $cacheMisses.Count -eq 0
if ($cacheMisses.Count -gt 0 -and $NoDownload) {
    Write-CacheInvalidResult $resolvedVersion $bundle $cacheMisses $packageResults
    exit 3
}

$downloadedPackages = [System.Collections.Generic.List[object]]::new()
foreach ($miss in @($cacheMisses)) {
    $packageId = [string]$miss.package_id
    $destinationPath = Get-ExpectedPackagePath $cacheRoot $packageId $resolvedVersion
    $destinationDirectory = Split-Path -Parent $destinationPath
    $null = New-Item -ItemType Directory -Force -Path $destinationDirectory
    $temporaryPath = "$destinationPath.$PID.$([guid]::NewGuid().ToString('N')).download"
    $packageUrl = "$($NuGetBaseUrl.TrimEnd('/'))/$([uri]::EscapeDataString($packageId))/$([uri]::EscapeDataString($resolvedVersion))"
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $packageUrl -OutFile $temporaryPath | Out-Null
        $downloadValidation = Test-ExactNupkg $temporaryPath $packageId $resolvedVersion
        if (-not $downloadValidation.valid) {
            throw "Downloaded exact package failed validation: $($downloadValidation.reason)"
        }

        Move-Item -LiteralPath $temporaryPath -Destination $destinationPath -Force
        $downloadedPackages.Add([pscustomobject]@{
            package_id = $packageId
            resolved_version = $resolvedVersion
            path = [System.IO.Path]::GetFullPath($destinationPath)
            url = $packageUrl
        })
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

$finalValidation = [System.Collections.Generic.List[object]]::new()
foreach ($member in $bundle) {
    $packageId = [string]$member.package_id
    $validation = Find-ValidExactPackage $cacheRoot $packageId $resolvedVersion
    if (-not $validation.valid) {
        throw "Exact package bundle remains invalid after restore for '$packageId' at version '$resolvedVersion'."
    }

    $finalValidation.Add([pscustomobject]@{
        package_id = $packageId
        resolved_version = $resolvedVersion
        path = $validation.package.path
        validation = $validation.package.reason
    })
}

$result = [pscustomobject]@{
    status = 'package_cache_ready'
    resolved_runtime_version = $resolvedVersion
    runtime_bundle_packages = @($bundle | ForEach-Object { [string]$_.package_id })
    package_cache_root = $cacheRoot
    cache_hit = $initialCacheHit
    downloaded_packages = @($downloadedPackages)
    cache_validation = [pscustomobject]@{
        status = 'passed'
        policy = 'dotnet-cli-runtime-bundle'
        package_count = $finalValidation.Count
        packages = @($finalValidation)
    }
    package_results = @($packageResults)
}
Write-Output ($result | ConvertTo-Json -Depth 10)
