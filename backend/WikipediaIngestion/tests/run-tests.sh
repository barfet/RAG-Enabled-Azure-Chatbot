#!/bin/bash

# run-tests.sh
# Script to run all tests for the Wikipedia Data Ingestion Function on Linux/macOS

# Define colors for console output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Display banner
echo -e "\n${CYAN}=========================================================${NC}"
echo -e "${CYAN}   Wikipedia Data Ingestion Function - Test Runner${NC}"
echo -e "${CYAN}=========================================================${NC}\n"

# Get the directory of the script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
TEST_PROJECT_DIR="${SCRIPT_DIR}/WikipediaDataIngestionFunction.Tests"
SOLUTION_DIR="$(dirname "$(dirname "${SCRIPT_DIR}")")"

# Check if we're in CI/CD environment
if [ "${CI}" = "true" ]; then
    IS_CI=true
else
    IS_CI=false
fi

# Ensure the test project exists
if [ ! -d "${TEST_PROJECT_DIR}" ]; then
    echo -e "${RED}Error: Test project directory not found: ${TEST_PROJECT_DIR}${NC}"
    exit 1
fi

# Test options
if [ "${IS_CI}" = "true" ]; then
    VERBOSITY="normal"
else
    VERBOSITY="detailed"
fi
USE_COLLECTOR=true
FILTER="$1"

echo -e "${YELLOW}Working directory: ${SOLUTION_DIR}${NC}"
echo -e "${YELLOW}Test project: ${TEST_PROJECT_DIR}${NC}"
if [ -n "${FILTER}" ]; then
    echo -e "${YELLOW}Filter: ${FILTER}${NC}"
fi
echo ""

# Step 1: Restore packages
echo -e "${CYAN}Step 1: Restoring NuGet packages...${NC}"
dotnet restore "${TEST_PROJECT_DIR}"
if [ $? -ne 0 ]; then
    echo -e "${RED}Error: Failed to restore NuGet packages${NC}"
    exit 1
fi
echo -e "${GREEN}Packages restored successfully.${NC}\n"

# Step 2: Build the test project
echo -e "${CYAN}Step 2: Building test project...${NC}"
dotnet build "${TEST_PROJECT_DIR}" --configuration Debug --no-restore
if [ $? -ne 0 ]; then
    echo -e "${RED}Error: Build failed${NC}"
    exit 1
fi
echo -e "${GREEN}Build completed successfully.${NC}\n"

# Step 3: Run the tests
echo -e "${CYAN}Step 3: Running tests...${NC}"

TEST_ARGS=("test" "${TEST_PROJECT_DIR}" "--no-build" "--verbosity" "${VERBOSITY}")

if [ -n "${FILTER}" ]; then
    TEST_ARGS+=("--filter" "${FILTER}")
fi

if [ "${USE_COLLECTOR}" = "true" ]; then
    TEST_ARGS+=("--collect" "XPlat Code Coverage")
fi

# Run the tests
dotnet "${TEST_ARGS[@]}"

TEST_RESULT=$?
if [ ${TEST_RESULT} -eq 0 ]; then
    echo -e "${GREEN}All tests passed successfully!${NC}\n"
else
    echo -e "${RED}Some tests failed. Check the output above for details.${NC}\n"
fi

# Step 4: Generate coverage report if not in CI (assumes reportgenerator tool is installed)
if [ "${USE_COLLECTOR}" = "true" ] && [ "${IS_CI}" = "false" ] && [ ${TEST_RESULT} -eq 0 ]; then
    echo -e "${CYAN}Step 4: Generating coverage report...${NC}"
    
    # Find the coverage file
    COVERAGE_DIR=$(find "${TEST_PROJECT_DIR}/TestResults" -type d -print | sort -r | head -n 1)
    if [ -n "${COVERAGE_DIR}" ]; then
        COVERAGE_FILE=$(find "${COVERAGE_DIR}" -name "*.coverage" -o -name "coverage.cobertura.xml" | head -n 1)
        
        if [ -n "${COVERAGE_FILE}" ]; then
            REPORT_DIR="${SCRIPT_DIR}/CoverageReport"
            
            # Check if reportgenerator is installed
            if command -v reportgenerator &> /dev/null; then
                # Generate the report
                reportgenerator "-reports:${COVERAGE_FILE}" "-targetdir:${REPORT_DIR}" "-reporttypes:Html"
                
                if [ $? -eq 0 ]; then
                    echo -e "${GREEN}Coverage report generated at: ${REPORT_DIR}${NC}"
                    
                    # Open the report in the default browser if not in CI
                    if [ "${IS_CI}" = "false" ]; then
                        INDEX_HTML="${REPORT_DIR}/index.html"
                        if [[ "$OSTYPE" == "darwin"* ]]; then
                            # macOS
                            open "${INDEX_HTML}"
                        else
                            # Linux
                            if command -v xdg-open &> /dev/null; then
                                xdg-open "${INDEX_HTML}"
                            else
                                echo -e "${YELLOW}Cannot open browser automatically. Open ${INDEX_HTML} manually.${NC}"
                            fi
                        fi
                    fi
                else
                    echo -e "${RED}Failed to generate coverage report${NC}"
                fi
            else
                echo -e "${YELLOW}reportgenerator tool not installed. Install with: dotnet tool install -g dotnet-reportgenerator-globaltool${NC}"
            fi
        else
            echo -e "${YELLOW}No coverage file found in ${COVERAGE_DIR}${NC}"
        fi
    else
        echo -e "${YELLOW}No test results directory found${NC}"
    fi
else
    echo -e "${YELLOW}Skipping coverage report generation.${NC}"
fi

# Return the test result
exit ${TEST_RESULT} 