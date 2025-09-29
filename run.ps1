#!/usr/bin/env pwsh
# Launcher script for PosKernel.AI.Demo
# Builds the solution first, then runs the demo only if build succeeds

Write-Host "Building PosKernel solution..." -ForegroundColor Cyan

# Build the solution
dotnet build PosKernel.sln

# Check if the build was successful
if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful! Starting PosKernel.AI.Demo..." -ForegroundColor Green
    dotnet run --project PosKernel.AI.Demo $args
} else {
    Write-Host "Build failed! Not starting the demo." -ForegroundColor Red
    exit $LASTEXITCODE
}
