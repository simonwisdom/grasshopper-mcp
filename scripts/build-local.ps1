#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local build script for Grasshopper MCP Component

.DESCRIPTION
    Builds the C# project locally and copies the output to the releases directory.
    This script is useful for testing builds before pushing to GitHub.

.PARAMETER Configuration
    Build configuration (Debug or Release). Defaults to Release.

.PARAMETER Framework
    Target framework to build. Defaults to net48.

.EXAMPLE
    .\build-local.ps1
    Builds the project in Release configuration for net48

.EXAMPLE
    .\build-local.ps1 -Configuration Debug -Framework net7.0-windows
    Builds the project in Debug configuration for net7.0-windows
#>

param(
    [string]$Configuration = "Release",
    [string]$Framework = "net48"
)

Write-Host "=== Grasshopper MCP Local Build ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Framework: $Framework" -ForegroundColor Yellow
Write-Host ""

# Check if we're in the right directory
if (-not (Test-Path "GH_MCP/GH_MCP.sln")) {
    Write-Error "GH_MCP.sln not found. Please run this script from the project root directory."
    exit 1
}

# Restore dependencies
Write-Host "Restoring dependencies..." -ForegroundColor Cyan
dotnet restore GH_MCP/GH_MCP.sln
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to restore dependencies"
    exit 1
}

# Build the project
Write-Host "Building project..." -ForegroundColor Cyan
dotnet build GH_MCP/GH_MCP.sln --configuration $Configuration --framework $Framework --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Check if build output exists
$buildOutput = "GH_MCP/GH_MCP/bin/$Configuration/$Framework/GH_MCP.gha"
if (-not (Test-Path $buildOutput)) {
    Write-Error "Build output not found at: $buildOutput"
    exit 1
}

# Create releases directory if it doesn't exist
if (-not (Test-Path "releases")) {
    New-Item -ItemType Directory -Path "releases" | Out-Null
}

# Copy to releases directory
$releaseFile = "releases/GH_MCP.gha"
Copy-Item $buildOutput $releaseFile -Force
Write-Host "Build output copied to: $releaseFile" -ForegroundColor Green

# Show file info
$fileInfo = Get-Item $releaseFile
Write-Host ""
Write-Host "=== Build Summary ===" -ForegroundColor Green
Write-Host "File: $($fileInfo.Name)" -ForegroundColor White
Write-Host "Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor White
Write-Host "Created: $($fileInfo.CreationTime)" -ForegroundColor White
Write-Host "Framework: $Framework" -ForegroundColor White
Write-Host "Configuration: $Configuration" -ForegroundColor White

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "You can now copy $releaseFile to your Grasshopper components folder." -ForegroundColor Cyan 