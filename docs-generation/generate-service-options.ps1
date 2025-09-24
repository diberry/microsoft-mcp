#!/usr/bin/env pwsh

# Helper functions for colored output
function Write-Info { param([string]$Message) Write-Host "INFO: $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Error { param([string]$Message) Write-Host "ERROR: $Message" -ForegroundColor Red }
function Write-Progress { param([string]$Message) Write-Host "PROGRESS: $Message" -ForegroundColor Magenta }

Write-Progress "Generating service options documentation..."

# Ensure output directory exists
$outputDir = "generated/multi-page"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Info "Created output directory: $outputDir"
}

# Run the CSharpGenerator with just service options
Push-Location "CSharpGenerator"
& dotnet run --configuration Release -- generate-docs ../generated/cli-output.json ../generated/multi-page --service-options
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate documentation"
    exit 1
}
Pop-Location

Write-Success "Service options documentation generated successfully"

# Show the generated file
$filePath = Join-Path $outputDir "service-start-option.md"
if (Test-Path $filePath) {
    Write-Info "Generated file: $filePath"
    Write-Info "File content:"
    Get-Content $filePath | ForEach-Object { Write-Host $_ }
} else {
    Write-Error "Generated file not found: $filePath"
}