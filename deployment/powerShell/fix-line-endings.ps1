<#
.SYNOPSIS
  Fix line endings for Linux deployment scripts/configs.
  Converts CRLF -> LF for target files.

.DESCRIPTION
  - Supports recurse
  - Supports backup
  - Only rewrites files that actually contain CRLF
  - Works with bytes to avoid encoding surprises
  - Summary report at the end

.PARAMETER Root
  Root folder to scan. Default: current script directory ($PSScriptRoot)

.PARAMETER Recurse
  Scan subfolders recursively.

.PARAMETER IncludeExtensions
  File extensions to include (default: .sh)

.PARAMETER Backup
  Create .bak file before overwriting.

.PARAMETER WhatIf
  Dry-run (show what would change).

.EXAMPLE
  .\fix-line-endings.ps1

.EXAMPLE
  .\fix-line-endings.ps1 -Recurse -IncludeExtensions .sh,.env,.conf -Backup

#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Root = $PSScriptRoot,
    [switch]$Recurse,
    [string[]]$IncludeExtensions = @(".sh"),
    [switch]$Backup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fix Line Endings for Linux Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $Root)) {
    throw "Root path not found: $Root"
}

# Normalize extensions: ".sh" format, lower-case, unique
$exts = $IncludeExtensions |
    ForEach-Object {
        $e = $_.Trim()
        if (-not $e.StartsWith(".")) { $e = "." + $e }
        $e.ToLowerInvariant()
    } | Select-Object -Unique

$scanMode = if ($Recurse) { "-Recurse" } else { "(no recurse)" }
Write-Host "Root: $Root $scanMode" -ForegroundColor White
Write-Host "Extensions: $($exts -join ', ')" -ForegroundColor White
Write-Host "Backup: $Backup" -ForegroundColor White
Write-Host ""

# Collect files
$gciParams = @{
    Path = $Root
    File = $true
    ErrorAction = "SilentlyContinue"
}
if ($Recurse) { $gciParams.Recurse = $true }

$files = Get-ChildItem @gciParams |
    Where-Object { $exts -contains $_.Extension.ToLowerInvariant() }

if (-not $files -or $files.Count -eq 0) {
    Write-Host "No matching files found under: $Root" -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $($files.Count) file(s) to scan:" -ForegroundColor Green
$files | ForEach-Object { Write-Host "  - $($_.FullName)" -ForegroundColor White }
Write-Host ""

[int]$converted = 0
[int]$skipped   = 0
[int]$failed    = 0

foreach ($f in $files) {
    try {
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
        if ($bytes.Length -eq 0) { $skipped++; continue }

        # detect CRLF quickly
        $hasCrLf = $false
        for ($i = 0; $i -lt $bytes.Length - 1; $i++) {
            if ($bytes[$i] -eq 13 -and $bytes[$i+1] -eq 10) { $hasCrLf = $true; break }
        }

        if (-not $hasCrLf) {
            Write-Host "Skip (already LF): $($f.FullName)" -ForegroundColor DarkGray
            $skipped++
            continue
        }

        if ($PSCmdlet.ShouldProcess($f.FullName, "Convert CRLF -> LF")) {
            Write-Host "Converting: $($f.FullName)..." -ForegroundColor Yellow

            if ($Backup) {
                Copy-Item -Path $f.FullName -Destination ($f.FullName + ".bak") -Force
            }

            $ms = New-Object System.IO.MemoryStream
            for ($i = 0; $i -lt $bytes.Length; $i++) {
                if ($i -lt $bytes.Length - 1 -and $bytes[$i] -eq 13 -and $bytes[$i+1] -eq 10) {
                    $ms.WriteByte(10) # LF
                    $i++              # skip paired LF
                } else {
                    $ms.WriteByte($bytes[$i])
                }
            }

            [System.IO.File]::WriteAllBytes($f.FullName, $ms.ToArray())
            Write-Host "  Converted to LF" -ForegroundColor Green
            $converted++
        }
    }
    catch {
        $failed++
        Write-Host "ERROR: $($f.FullName) => $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Conversion Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ("Converted: {0}" -f $converted) -ForegroundColor Green
Write-Host ("Skipped:   {0}" -f $skipped)   -ForegroundColor Yellow
Write-Host ("Failed:    {0}" -f $failed)    -ForegroundColor Red
Write-Host ""

if ($failed -gt 0) { exit 1 }
