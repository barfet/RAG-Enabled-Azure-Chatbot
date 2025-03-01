#!/bin/bash

# Change to the backend directory
cd "$(dirname "$0")"

# Set text color variables
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}Running code analysis on all projects...${NC}"

# Restore packages first
echo -e "\n${YELLOW}Restoring packages...${NC}"
dotnet restore

# Build the solution with detailed output to show warnings and errors
echo -e "\n${YELLOW}Building solution with standard settings...${NC}"
dotnet build --no-incremental 

# Get the build exit code
BUILD_RESULT=$?

if [ $BUILD_RESULT -eq 0 ]; then
  echo -e "${GREEN}Build succeeded.${NC}"
else
  echo -e "${RED}Build failed with errors.${NC}"
  exit 1
fi

echo -e "\n${BLUE}Running detailed code analysis...${NC}"

# Run code analysis on all projects (you can add more projects as needed)
PROJECTS=(
  "src/WikipediaIngestion.Core"
  "src/WikipediaIngestion.Infrastructure"
  "src/WikipediaIngestion.Functions"
  "tests/WikipediaIngestion.UnitTests"
  "tests/WikipediaIngestion.IntegrationTests"
)

for PROJECT in "${PROJECTS[@]}"; do
  echo -e "\n${YELLOW}Analyzing $PROJECT...${NC}"
  # Show more details with detailed verbosity and warn as error flags
  dotnet build "$PROJECT" -v detailed /p:TreatWarningsAsErrors=true /warnaserror /p:WarningLevel=5
  
  # Check if any analysis errors occurred
  if [ $? -ne 0 ]; then
    echo -e "${RED}Analysis found issues in $PROJECT${NC}"
  else
    echo -e "${GREEN}No issues found in $PROJECT${NC}"
  fi
done

# Run nullability check with the /p:Nullable=enable flag
echo -e "\n${YELLOW}Running nullability check across all projects...${NC}"
dotnet build /p:Nullable=enable /p:TreatWarningsAsErrors=true

echo -e "\n${BLUE}Analysis complete.${NC}"

# Visual feedback on completion
if [ $BUILD_RESULT -eq 0 ]; then
  echo -e "\n${GREEN}✓ All checks completed. Project is ready for development.${NC}"
else
  echo -e "\n${RED}✗ Project has issues that need to be fixed.${NC}"
  exit 1
fi 