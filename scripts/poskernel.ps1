#!/usr/bin/env pwsh
<#
.SYNOPSIS
    PosKernel CLI wrapper script - run from anywhere
.DESCRIPTION
    This script allows you to run the PosKernel CLI from any directory.
    It automatically navigates to the correct project folder and passes
    all parameters through to the CLI.
.PARAMETER Command
    The CLI command and parameters to execute
.EXAMPLE
    poskernel start rust
    poskernel stop
    poskernel status
    poskernel logs rust
#>

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

# Get the script directory and navigate to PosKernel root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$PosKernelRoot = Split-Path -Parent $ScriptDir
$OriginalLocation = Get-Location

try {
    # Change to PosKernel root directory
    Set-Location $PosKernelRoot

    # Build the CLI arguments
    $CliArgs = @("run", "--project", "PosKernel.AI.Cli", "--no-build", "--")
    if ($Arguments) {
        $CliArgs += $Arguments
    }

    # Execute the CLI
    & dotnet @CliArgs
    exit $LASTEXITCODE
}
finally {
    # Restore original location
    Set-Location $OriginalLocation
}
