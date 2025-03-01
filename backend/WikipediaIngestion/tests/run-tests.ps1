#!/usr/bin/env pwsh

# run-tests.ps1
# Script to run all tests for the Wikipedia Data Ingestion Function

# Define colors for console output
$green = [ConsoleColor]::Green
$red = [ConsoleColor]::Red
$yellow = [ConsoleColor]::Yellow
$cyan = [ConsoleColor]::Cyan

# Display banner
Write-Host "`n=========================================================" -ForegroundColor $cyan
Write-Host "   Wikipedia Data Ingestion Function - Test Runner" -ForegroundColor $cyan
Write-Host "=========================================================`n" -ForegroundColor $cyan

# Get the directory of the script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testProjectDir = Join-Path $scriptDir "WikipediaDataIngestionFunction.Tests"
$solutionDir = Split-Path -Parent (Split-Path -Parent $scriptDir)

# Check if we're in CI/CD environment
$isCI = $env:CI -eq "true"

# Ensure the test project exists
if (-not (Test-Path $testProjectDir)) {
    Write-Host "Error: Test project directory not found: $testProjectDir" -ForegroundColor $red
    exit 1
}

# Test options
$verbosity = if ($isCI) { "normal" } else { "detailed" }
$useCollector = $true
$filter = $args[0]

Write-Host "Working directory: $solutionDir" -ForegroundColor $yellow
Write-Host "Test project: $testProjectDir" -ForegroundColor $yellow
if ($filter) {
    Write-Host "Filter: $filter" -ForegroundColor $yellow
}
Write-Host ""

# Step 1: Restore packages
Write-Host "Step 1: Restoring NuGet packages..." -ForegroundColor $cyan
dotnet restore "$testProjectDir"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to restore NuGet packages" -ForegroundColor $red
    exit 1
}
Write-Host "Packages restored successfully.`n" -ForegroundColor $green

# Step 2: Build the test project
Write-Host "Step 2: Building test project..." -ForegroundColor $cyan
dotnet build "$testProjectDir" --configuration Debug --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed" -ForegroundColor $red
    exit 1
}
Write-Host "Build completed successfully.`n" -ForegroundColor $green

# Step 3: Run the tests
Write-Host "Step 3: Running tests..." -ForegroundColor $cyan

$testArgs = @(
    "test",
    "$testProjectDir",
    "--no-build",
    "--verbosity", $verbosity
)

if ($filter) {
    $testArgs += "--filter", $filter
}

if ($useCollector) {
    $testArgs += "--collect", "XPlat Code Coverage"
}

# Run the tests
dotnet $testArgs

$testResult = $LASTEXITCODE
if ($testResult -eq 0) {
    Write-Host "All tests passed successfully!`n" -ForegroundColor $green
} else {
    Write-Host "Some tests failed. Check the output above for details.`n" -ForegroundColor $red
}

# Step 4: Generate coverage report if not in CI (assumes reportgenerator tool is installed)
if ($useCollector -and (-not $isCI) -and ($testResult -eq 0)) {
    Write-Host "Step 4: Generating coverage report..." -ForegroundColor $cyan
    
    # Find the coverage file
    $coverageDir = Get-ChildItem -Path "$testProjectDir\TestResults" -Directory | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1
    if ($coverageDir) {
        $coverageFile = Get-ChildItem -Path $coverageDir.FullName -Filter "*.coverage" -Recurse | Select-Object -First 1
        if (-not $coverageFile) {
            $coverageFile = Get-ChildItem -Path $coverageDir.FullName -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1
        }
        
        if ($coverageFile) {
            $reportDir = Join-Path $scriptDir "CoverageReport"
            
            # Check if reportgenerator is installed
            $reportGenInstalled = Get-Command reportgenerator -ErrorAction SilentlyContinue
            
            if ($reportGenInstalled) {
                # Generate the report
                reportgenerator "-reports:$($coverageFile.FullName)" "-targetdir:$reportDir" "-reporttypes:Html"
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "Coverage report generated at: $reportDir" -ForegroundColor $green
                    
                    # Open the report in the default browser if not in CI
                    if (-not $isCI) {
                        $indexHtml = Join-Path $reportDir "index.html"
                        Start-Process $indexHtml
                    }
                } else {
                    Write-Host "Failed to generate coverage report" -ForegroundColor $red
                }
            } else {
                Write-Host "reportgenerator tool not installed. Install with: dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor $yellow
            }
        } else {
            Write-Host "No coverage file found in $($coverageDir.FullName)" -ForegroundColor $yellow
        }
    } else {
        Write-Host "No test results directory found" -ForegroundColor $yellow
    }
} else {
    Write-Host "Skipping coverage report generation." -ForegroundColor $yellow
}

# Return the test result
exit $testResult 