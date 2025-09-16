#!/usr/bin/env pwsh
#
# Database initialization script for Restaurant Extension
#

param(
    [string]$DatabasePath = "data/catalog/restaurant_catalog.db",
    [switch]$Force = $false
)

Write-Host "🗄️  Initializing Restaurant Catalog Database" -ForegroundColor Green
Write-Host "Database path: $DatabasePath"

# Ensure directory exists
$dbDir = Split-Path $DatabasePath -Parent
if (-not (Test-Path $dbDir)) {
    New-Item -ItemType Directory -Path $dbDir -Force | Out-Null
    Write-Host "Created directory: $dbDir" -ForegroundColor Yellow
}

# Remove existing database if Force is specified
if ($Force -and (Test-Path $DatabasePath)) {
    Remove-Item $DatabasePath -Force
    Write-Host "Removed existing database" -ForegroundColor Yellow
}

# Check if database already exists
if (Test-Path $DatabasePath) {
    Write-Host "Database already exists. Use -Force to recreate." -ForegroundColor Yellow
    return
}

try {
    # Create database using sqlite3 if available, otherwise use .NET
    $sqlite3Path = Get-Command sqlite3 -ErrorAction SilentlyContinue
    
    if ($sqlite3Path) {
        Write-Host "Using sqlite3 command-line tool..." -ForegroundColor Cyan
        
        # Create schema
        $schemaPath = "data/catalog/restaurant_catalog_schema.sql"
        if (Test-Path $schemaPath) {
            & sqlite3 $DatabasePath ".read $schemaPath"
            Write-Host "✅ Schema created successfully" -ForegroundColor Green
        } else {
            Write-Error "Schema file not found: $schemaPath"
            return
        }
        
        # Insert sample data
        $dataPath = "data/catalog/restaurant_catalog_data.sql"
        if (Test-Path $dataPath) {
            & sqlite3 $DatabasePath ".read $dataPath"
            Write-Host "✅ Sample data loaded successfully" -ForegroundColor Green
        } else {
            Write-Warning "Data file not found: $dataPath"
        }
    } else {
        Write-Host "sqlite3 not found, using .NET SQLite..." -ForegroundColor Cyan
        
        # Use .NET approach (this would require compiling the extension first)
        Write-Warning "Please install sqlite3 command-line tool or build the restaurant extension first"
        Write-Host "To install sqlite3:"
        Write-Host "  - Windows: winget install SQLite.SQLite"
        Write-Host "  - macOS: brew install sqlite"
        Write-Host "  - Linux: apt install sqlite3"
        return
    }

    Write-Host "🎉 Database initialization complete!" -ForegroundColor Green
    
    # Verify database
    $count = & sqlite3 $DatabasePath "SELECT COUNT(*) FROM products;"
    Write-Host "Product count: $count" -ForegroundColor Cyan
    
} catch {
    Write-Error "Failed to initialize database: $_"
}
