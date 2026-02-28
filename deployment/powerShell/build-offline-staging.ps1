# ANE API - Offline Docker Build (Staging) - Production Grade
# ===========================================================
# Build locally -> publish -> docker build from published output -> export image to tar
# Use when Docker cannot connect to NuGet due to SSL/proxy issues

[CmdletBinding()]
param(
    [string]$ImageName = "ane-api",
    [string]$ImageTag = "staging",
    [string]$ApiProjectPath = "src/API/API.csproj",
    [switch]$NoCleanup,
    [switch]$BackupLineEndings,
    [switch]$WhatIfLineEndings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section([string]$title) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host $title -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-Step([int]$i, [int]$total, [string]$msg) {
    Write-Host ""
    Write-Host ("[{0}/{1}] {2}" -f $i, $total, $msg) -ForegroundColor White
}

function Ensure-Command([string]$cmd, [string]$hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "Missing required command '$cmd'. $hint"
    }
}

function Remove-PathSafe([string]$path) {
    if (Test-Path $path) {
        Remove-Item -Recurse -Force $path -ErrorAction Stop
    }
}

function Ensure-Dir([string]$path) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }
}

function Copy-DirSafe([string]$src, [string]$dst) {
    if (-not (Test-Path $src)) { return $false }
    Ensure-Dir $dst
    Copy-Item -Path $src -Destination $dst -Recurse -Force -ErrorAction Stop
    return $true
}

function Copy-FileSafe([string]$src, [string]$dst) {
    if (-not (Test-Path $src)) { return $false }
    Ensure-Dir (Split-Path $dst -Parent)
    Copy-Item -Path $src -Destination $dst -Force -ErrorAction Stop
    return $true
}

Write-Section "ANE API - Offline Docker Build (Staging) - Production Grade"

# ---------------- Paths ----------------
$ScriptDir   = $PSScriptRoot
# Assumption: this script lives under: deployment\scripts\
$ProjectRoot = Split-Path (Split-Path $ScriptDir -Parent) -Parent

Set-Location $ProjectRoot

$FullImageName = "$ImageName`:$ImageTag"
$TarFile       = "$ImageName-$ImageTag.tar"

$DeployRoot     = Join-Path $ProjectRoot "deployment"
$DeployPublish  = Join-Path $DeployRoot "publish"
$DeployDeps     = Join-Path $DeployRoot "docker-deps"
$DeploySources  = Join-Path $DeployRoot "Sources"
$DeployCerts    = Join-Path $DeployRoot "ca-certificates"
$DeployLicenses = Join-Path $DeployRoot "Licenses"
$DeployEntryp   = Join-Path $DeployRoot "docker-entrypoint.sh"
$OutputDir      = Join-Path $DeployRoot "output"
$TarPath        = Join-Path $OutputDir $TarFile

$FixLineEndingsScript = Join-Path $ScriptDir "fix-line-endings.ps1"
$DockerfileOffline    = Join-Path $DeployRoot "Dockerfile.offline"

$TotalSteps = 9
$DidBuild = $false

# ---------------- Pre-checks ----------------
Write-Step 1 $TotalSteps "Pre-check prerequisites..."
Ensure-Command "dotnet" "Install .NET SDK and ensure it's on PATH."
Ensure-Command "docker" "Install Docker Desktop and ensure docker CLI works."
if (-not (Test-Path $ApiProjectPath)) { throw "API csproj not found: $ApiProjectPath" }
if (-not (Test-Path $DockerfileOffline)) { throw "Dockerfile.offline not found: $DockerfileOffline" }

# Optional git info
$GitCommit = $null
if (Get-Command git -ErrorAction SilentlyContinue) {
    try { $GitCommit = (git rev-parse --short HEAD 2>$null).Trim() } catch { $GitCommit = $null }
}

try {
    # ---------------- Clean workspace ----------------
    Write-Step 2 $TotalSteps "Cleaning previous deployment build artifacts..."
    Remove-PathSafe $DeployPublish
    Remove-PathSafe $DeployDeps
    Remove-PathSafe $DeploySources
    Remove-PathSafe $DeployCerts
    Remove-PathSafe $DeployLicenses
    if (Test-Path $DeployEntryp) { Remove-Item -Force $DeployEntryp -ErrorAction SilentlyContinue }

    # ---------------- Fix line endings (Production) ----------------
    Write-Step 3 $TotalSteps "Fixing line endings (CRLF -> LF) for Linux scripts/configs..."
    if (Test-Path $FixLineEndingsScript) {
        $lineArgs = @(
            "-Root", $ProjectRoot,
            "-Recurse",
            "-IncludeExtensions", ".sh,.env,.conf"
        )
        if ($BackupLineEndings) { $lineArgs += "-Backup" }
        if ($WhatIfLineEndings) { $lineArgs += "-WhatIf" }

        & $FixLineEndingsScript @lineArgs
        Write-Host "  - Ran fix-line-endings.ps1 (production)" -ForegroundColor Green
    } else {
        Write-Host "  - WARNING: fix-line-endings.ps1 not found. Skipping." -ForegroundColor Yellow
    }

    # ---------------- Copy dependencies into deployment context ----------------
    Write-Step 4 $TotalSteps "Copying dependencies into deployment context..."

    # docker-deps -> deployment\docker-deps
    if (Copy-DirSafe (Join-Path $ProjectRoot "docker-deps") $DeployDeps) {
        Write-Host "  - Copied docker-deps" -ForegroundColor Green
    } else {
        Ensure-Dir $DeployDeps
        Write-Host "  - docker-deps not found; created empty deployment\docker-deps" -ForegroundColor Yellow
    }

    # Sources (Fonts + Reports) -> deployment\Sources
    $ApiSourcesRoot = Join-Path $ProjectRoot "src\API\Sources"
    if (Test-Path $ApiSourcesRoot) {
        Ensure-Dir $DeploySources

        if (Copy-DirSafe (Join-Path $ApiSourcesRoot "Fonts") (Join-Path $DeploySources "Fonts")) {
            $fontCount = @(Get-ChildItem (Join-Path $DeploySources "Fonts\*.ttf") -ErrorAction SilentlyContinue).Count
            Write-Host "  - Copied $fontCount font(s)" -ForegroundColor Green
        } else {
            Write-Host "  - Fonts folder not found under src\API\Sources" -ForegroundColor Yellow
        }

        if (Copy-DirSafe (Join-Path $ApiSourcesRoot "Reports") (Join-Path $DeploySources "Reports")) {
            $reportCount = @(Get-ChildItem (Join-Path $DeploySources "Reports\*.mrt") -ErrorAction SilentlyContinue).Count
            Write-Host "  - Copied $reportCount report template(s)" -ForegroundColor Green
        } else {
            Write-Host "  - Reports folder not found under src\API\Sources" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  - WARNING: Sources folder not found: $ApiSourcesRoot" -ForegroundColor Yellow
    }

    # CA certs -> deployment\ca-certificates
    $CertRoot = Join-Path $ProjectRoot "ca-certificates"
    if (Test-Path $CertRoot) {
        Ensure-Dir $DeployCerts
        Copy-Item -Path (Join-Path $CertRoot "*.crt") -Destination $DeployCerts -Force -ErrorAction SilentlyContinue
        $certCount = @(Get-ChildItem (Join-Path $DeployCerts "*.crt") -ErrorAction SilentlyContinue).Count
        Write-Host "  - Copied $certCount CA certificate(s)" -ForegroundColor Green
    } else {
        Write-Host "  - WARNING: ca-certificates folder not found (TLS trust inside container may fail)" -ForegroundColor Yellow
    }

    # docker-entrypoint.sh -> deployment\docker-entrypoint.sh
    if (Copy-FileSafe (Join-Path $ProjectRoot "docker-entrypoint.sh") $DeployEntryp) {
        Write-Host "  - Copied docker-entrypoint.sh" -ForegroundColor Green
    } else {
        Write-Host "  - WARNING: docker-entrypoint.sh not found in repo root" -ForegroundColor Yellow
    }

    # Licenses -> deployment\Licenses
    if (Copy-DirSafe (Join-Path $ProjectRoot "src\API\Licenses") $DeployLicenses) {
        Write-Host "  - Copied Licenses folder" -ForegroundColor Green
    } else {
        Write-Host "  - WARNING: Licenses folder not found under src\API" -ForegroundColor Yellow
    }

    # ---------------- dotnet publish ----------------
    Write-Step 5 $TotalSteps "dotnet publish (Release) to deployment\publish..."
    # Offline-safe: do not restore here; rely on local cache / prior restore
    dotnet publish $ApiProjectPath `
        -c Release `
        -o $DeployPublish `
        --nologo `
        --no-restore

    # ---------------- docker build ----------------
    Write-Step 6 $TotalSteps "docker build from published output (deployment context)..."
    Ensure-Dir $OutputDir

    $labels = @(
        "--label", "org.opencontainers.image.title=$ImageName",
        "--label", "org.opencontainers.image.created=$(Get-Date -Format o)"
    )
    if ($GitCommit) {
        $labels += @("--label", "org.opencontainers.image.revision=$GitCommit")
    }

    docker build `
        -f $DockerfileOffline `
        -t $FullImageName `
        @labels `
        $DeployRoot

    # ---------------- docker save ----------------
    Write-Step 7 $TotalSteps "docker save image -> tar: $TarFile"
    docker save $FullImageName -o $TarPath

    # ---------------- verify tar ----------------
    Write-Step 8 $TotalSteps "Verifying output..."
    if (-not (Test-Path $TarPath)) { throw "Export failed: tar not found at $TarPath" }
    $FileInfo = Get-Item $TarPath
    $FileSizeMB = [math]::Round($FileInfo.Length / 1MB, 2)
    Write-Host "  - Tar size: $FileSizeMB MB" -ForegroundColor Green

    $DidBuild = $true

    # ---------------- summary ----------------
    Write-Step 9 $TotalSteps "DONE - Summary + Next steps"
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "BUILD SUCCESSFUL (Staging)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Image: $FullImageName"
    if ($GitCommit) { Write-Host "Git:   $GitCommit" }
    Write-Host "File:  deployment\output\$TarFile"
    Write-Host "Size:  $FileSizeMB MB"
    Write-Host ""
    Write-Host "Linux next steps:" -ForegroundColor Yellow
    Write-Host "1) Upload tar to server (e.g. /tmp/$TarFile)" -ForegroundColor White
    Write-Host "2) docker load -i /tmp/$TarFile" -ForegroundColor White
    Write-Host "3) sudo ./deploy-staging.sh" -ForegroundColor White
    Write-Host ""
}
finally {
    if (-not $NoCleanup) {
        # Keep output tar; cleanup build context artifacts
        if (Test-Path $DeployPublish)  { Remove-PathSafe $DeployPublish }
        if (Test-Path $DeployDeps)     { Remove-PathSafe $DeployDeps }
        if (Test-Path $DeploySources)  { Remove-PathSafe $DeploySources }
        if (Test-Path $DeployCerts)    { Remove-PathSafe $DeployCerts }
        if (Test-Path $DeployLicenses) { Remove-PathSafe $DeployLicenses }
        if (Test-Path $DeployEntryp)   { Remove-Item -Force $DeployEntryp -ErrorAction SilentlyContinue }
    } else {
        Write-Host ""
        Write-Host "NOTE: -NoCleanup specified; keeping deployment temp folders for inspection." -ForegroundColor Yellow
    }
}
