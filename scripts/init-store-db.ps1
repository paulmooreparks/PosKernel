# Initialize Store-Specific Restaurant Databases
# Copyright 2025 Paul Moore Parks and contributors
# Licensed under the Apache License, Version 2.0

param(
    [string]$StoreType = "kopitiam",
    [switch]$Force = $false
)

Write-Host "🏪 Initializing $StoreType Restaurant Database" -ForegroundColor Cyan

$rootDir = Split-Path $PSScriptRoot -Parent
$dataDir = Join-Path $rootDir "data\catalog"
$dbPath = Join-Path $dataDir "${StoreType}_catalog.db"
$schemaFile = Join-Path $dataDir "restaurant_catalog_schema.sql"

Write-Host "Database path: $dbPath"

# Create data directory if it doesn't exist
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    Write-Host "Created data directory: $dataDir" -ForegroundColor Green
}

# Remove existing database if Force is specified
if ($Force -and (Test-Path $dbPath)) {
    Remove-Item $dbPath -Force
    Write-Host "Removed existing database" -ForegroundColor Yellow
}

# Check if database already exists
if (Test-Path $dbPath) {
    Write-Host "Database already exists. Use -Force to recreate." -ForegroundColor Yellow
    return
}

# Check for required files
if (-not (Test-Path $schemaFile)) {
    Write-Host "❌ Schema file not found: $schemaFile" -ForegroundColor Red
    exit 1
}

# Determine which data file to use based on store type
$dataFile = switch ($StoreType.ToLower()) {
    "kopitiam" { Join-Path $dataDir "kopitiam_catalog_data.sql" }
    "coffeeshop" { Join-Path $dataDir "restaurant_catalog_data.sql" }
    "boulangerie" { Join-Path $dataDir "french_catalog_data.sql" }
    "convenience" { Join-Path $dataDir "convenience_catalog_data.sql" }
    default { Join-Path $dataDir "restaurant_catalog_data.sql" }
}

if (-not (Test-Path $dataFile)) {
    Write-Host "❌ Data file not found: $dataFile" -ForegroundColor Red
    Write-Host "Available data files:" -ForegroundColor Yellow
    Get-ChildItem -Path $dataDir -Filter "*_catalog_data.sql" | ForEach-Object {
        Write-Host "  • $($_.Name)" -ForegroundColor Gray
    }
    exit 1
}

# Try to use sqlite3 command-line tool
$sqlite3 = Get-Command sqlite3 -ErrorAction SilentlyContinue

if ($sqlite3) {
    Write-Host "Using sqlite3 command-line tool..." -ForegroundColor Green
    
    # Create schema
    $schemaCommand = ".read ""$schemaFile"""
    & sqlite3 $dbPath $schemaCommand
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Schema created successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to create schema" -ForegroundColor Red
        exit 1
    }
    
    # Load data
    $dataCommand = ".read ""$dataFile"""
    & sqlite3 $dbPath $dataCommand
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ $StoreType data loaded successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to load $StoreType data" -ForegroundColor Red
        exit 1
    }
    
    # Verify data was loaded
    $productCount = & sqlite3 $dbPath "SELECT COUNT(*) FROM products;"
    Write-Host "🎉 Database initialization complete!" -ForegroundColor Green
    Write-Host "Product count: $productCount" -ForegroundColor Cyan
} else {
    Write-Host "❌ sqlite3 command not found. Please install SQLite3." -ForegroundColor Red
    Write-Host "You can download it from: https://www.sqlite.org/download.html" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "🔧 To use this database, update your restaurant service configuration:" -ForegroundColor Cyan
Write-Host "   DatabasePath: `"data/catalog/${StoreType}_catalog.db`"" -ForegroundColor Gray
