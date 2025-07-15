#!/bin/bash
# Local build script for Grasshopper MCP Component
# Builds the C# project locally and copies the output to the releases directory

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
CONFIGURATION=${1:-Release}
FRAMEWORK=${2:-net48}

echo -e "${GREEN}=== Grasshopper MCP Local Build ===${NC}"
echo -e "${YELLOW}Configuration: $CONFIGURATION${NC}"
echo -e "${YELLOW}Framework: $FRAMEWORK${NC}"
echo ""

# Check if we're in the right directory
if [ ! -f "GH_MCP/GH_MCP.sln" ]; then
    echo -e "${RED}Error: GH_MCP.sln not found. Please run this script from the project root directory.${NC}"
    exit 1
fi

# Check if dotnet is available
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: dotnet CLI not found. Please install .NET SDK.${NC}"
    exit 1
fi

# Restore dependencies
echo -e "${CYAN}Restoring dependencies...${NC}"
dotnet restore GH_MCP/GH_MCP.sln
if [ $? -ne 0 ]; then
    echo -e "${RED}Failed to restore dependencies${NC}"
    exit 1
fi

# Build the project
echo -e "${CYAN}Building project...${NC}"
dotnet build GH_MCP/GH_MCP.sln --configuration $CONFIGURATION --framework $FRAMEWORK --no-restore
if [ $? -ne 0 ]; then
    echo -e "${RED}Build failed${NC}"
    exit 1
fi

# Check if build output exists
BUILD_OUTPUT="GH_MCP/GH_MCP/bin/$CONFIGURATION/$FRAMEWORK/GH_MCP.gha"
if [ ! -f "$BUILD_OUTPUT" ]; then
    echo -e "${RED}Error: Build output not found at: $BUILD_OUTPUT${NC}"
    exit 1
fi

# Create releases directory if it doesn't exist
mkdir -p releases

# Copy to releases directory
RELEASE_FILE="releases/GH_MCP.gha"
cp "$BUILD_OUTPUT" "$RELEASE_FILE"
echo -e "${GREEN}Build output copied to: $RELEASE_FILE${NC}"

# Show file info
echo ""
echo -e "${GREEN}=== Build Summary ===${NC}"
echo -e "${CYAN}File:${NC} $(basename "$RELEASE_FILE")"
echo -e "${CYAN}Size:${NC} $(du -h "$RELEASE_FILE" | cut -f1)"
echo -e "${CYAN}Created:${NC} $(stat -f "%Sm" "$RELEASE_FILE" 2>/dev/null || stat -c "%y" "$RELEASE_FILE")"
echo -e "${CYAN}Framework:${NC} $FRAMEWORK"
echo -e "${CYAN}Configuration:${NC} $CONFIGURATION"

echo ""
echo -e "${GREEN}Build completed successfully!${NC}"
echo -e "${CYAN}You can now copy $RELEASE_FILE to your Grasshopper components folder.${NC}"
echo ""
echo -e "${YELLOW}Note:${NC} This build was created on $(uname -s). For production use,"
echo -e "consider using the automated builds from GitHub Actions." 