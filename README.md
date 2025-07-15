# Grasshopper MCP Bridge

Grasshopper MCP Bridge is a bridging server that connects Grasshopper and Claude Desktop using the Model Context Protocol (MCP) standard.

## Features

- Connects Grasshopper and Claude Desktop through the MCP protocol
- Provides intuitive tool functions for creating and connecting Grasshopper components
- Supports high-level intent recognition, automatically creating complex component patterns from simple descriptions
- Includes a component knowledge base that understands parameters and connection rules for common components
- Provides component guidance resources to help Claude Desktop correctly connect components
- **NEW**: Comprehensive warning detection and canvas health analysis system

## System Architecture

The system consists of the following parts:

1. **Grasshopper MCP Component (GH_MCP.gha)**: A plugin installed in Grasshopper that provides a TCP server to receive commands
2. **Python MCP Bridge Server**: A bridge server that connects Claude Desktop and the Grasshopper MCP component
3. **Component Knowledge Base**: JSON files containing component information, patterns, and intents

## Installation Instructions

### Prerequisites

- Rhino 7 or higher
- Grasshopper
- Python 3.8 or higher
- Claude Desktop

### Installation Steps

1. **Install the Grasshopper MCP Component**

   **Method 1: Download the pre-compiled GH_MCP.gha file (Recommended)**
   
   **Option A: Latest Release**
   Download the latest release from the [Releases page](https://github.com/simonwisdom/grasshopper-mcp/releases) and choose the appropriate version for your Rhino installation:
   - `GH_MCP.gha` - Primary build (recommended for most users)
   - `GH_MCP-v*-net48.gha` - .NET Framework 4.8 (Rhino 7/8)
   - `GH_MCP-v*-net7.0-windows.gha` - .NET 7.0 Windows (Rhino 8)
   - `GH_MCP-v*-net7.0.gha` - .NET 7.0 Cross-platform
   
   **Option B: Latest Build**
   Download the latest build artifact from the [Actions tab](https://github.com/simonwisdom/grasshopper-mcp/actions) (look for the most recent successful build).
   
   Copy the downloaded .gha file to your Grasshopper components folder:
   ```
   %APPDATA%\Grasshopper\Libraries\
   ```

   **Method 2: Build from source**
   
   If you prefer to build from source, clone the repository and build the C# project using Visual Studio.

2. **Install the Python MCP Bridge Server**

   **Method 1: Install from PyPI (Recommended)**
   
   The simplest method is to install directly from PyPI using pip:
   ```
   pip install grasshopper-mcp
   ```
   
   **Method 2: Install from GitHub**
   
   You can also install the latest version from GitHub:
   ```
   pip install git+https://github.com/alfredatnycu/grasshopper-mcp.git
   ```
   
   **Method 3: Install from Source Code**
   
   If you need to modify the code or develop new features, you can clone the repository and install:
   ```
   git clone https://github.com/alfredatnycu/grasshopper-mcp.git
   cd grasshopper-mcp
   pip install -e .
   ```

   **Install a Specific Version**
   
   If you need to install a specific version, you can use:
   ```
   pip install grasshopper-mcp==0.1.0
   ```
   Or install from a specific GitHub tag:
   ```
   pip install git+https://github.com/alfredatnycu/grasshopper-mcp.git@v0.1.0
   ```

## Usage

1. **Start Rhino and Grasshopper**

   Launch Rhino and open Grasshopper.

2. **Add the GH_MCP Component to Your Canvas**

   Find the GH_MCP component in the Grasshopper component panel and add it to your canvas.

3. **Start the Python MCP Bridge Server**

   **Method 1: Using the Python Launcher (Recommended)**
   
   The easiest way is to use the included Python launcher:
   ```bash
   python launch_mcp.py
   ```
   
   This launcher automatically:
   - Validates your environment
   - Installs/updates dependencies
   - Checks for port conflicts
   - Provides detailed error messages
   
   **Method 2: Direct Module Execution**
   
   Alternatively, you can run the bridge directly:
   ```bash
   python -m grasshopper_mcp.bridge
   ```
   
   > **Note**: The command `grasshopper-mcp` might not work directly due to Python script path issues. Using the launcher or `python -m grasshopper_mcp.bridge` is the recommended method.

4. **Connect Claude Desktop to the MCP Bridge**

   **Method 1: Manual Connection**
   
   In Claude Desktop, connect to the MCP Bridge server using the following settings:
   - Protocol: MCP
   - Host: localhost
   - Port: 8080

   **Method 2: Configure Claude Desktop to Auto-Start the Bridge**
   
   You can configure Claude Desktop to automatically start the MCP Bridge server by modifying its configuration:
   
   ```json
   "grasshopper": {
     "command": "python",
     "args": ["-m", "grasshopper_mcp.bridge"]
   }
   ```
   
   This configuration tells Claude Desktop to use the command `python -m grasshopper_mcp.bridge` to start the MCP server.

5. **Start Using Grasshopper with Claude Desktop**

   You can now use Claude Desktop to control Grasshopper through natural language commands.

## Example Commands

Here are some example commands you can use with Claude Desktop:

- "Create a circle with radius 5 at point (0,0,0)"
- "Connect the circle to a extrude component with a height of 10"
- "Create a grid of points with 5 rows and 5 columns"
- "Apply a random rotation to all selected objects"
- "Check for warnings in my Grasshopper canvas"
- "Analyze the health of my definition"
- "Find floating parameters that need connections"

## Troubleshooting

If you encounter issues, check the following:

1. **GH_MCP Component Not Loading**
   - Ensure the .gha file is in the correct location
   - In Grasshopper, go to File > Preferences > Libraries and click "Unblock" to unblock new components
   - Restart Rhino and Grasshopper

2. **Bridge Server Won't Start**
   - If `grasshopper-mcp` command doesn't work, use `python -m grasshopper_mcp.bridge` instead
   - Ensure all required Python dependencies are installed
   - Check if port 8080 is already in use by another application

3. **Claude Desktop Can't Connect**
   - Ensure the bridge server is running
   - Verify you're using the correct connection settings (localhost:8080)
   - Check the console output of the bridge server for any error messages

4. **Commands Not Executing**
   - Verify the GH_MCP component is on your Grasshopper canvas
   - Check the bridge server console for error messages
   - Ensure Claude Desktop is properly connected to the bridge server

## Development

### Automated Build System

This project uses GitHub Actions for automated builds and releases:

- **Build Status**: ![Build Status](https://github.com/simonwisdom/grasshopper-mcp/workflows/Build%20Grasshopper%20MCP%20Component/badge.svg)
- **Builds on**: Every push to `main` and `develop` branches
- **Releases**: Automatically created when tags are pushed (e.g., `v1.0.0`)

#### Getting the Latest Build

1. **From GitHub Actions**: Go to the Actions tab and download the latest build artifacts
2. **From Releases**: Check the Releases page for versioned releases with multiple framework builds
3. **From Source**: Clone and build locally using Visual Studio or `dotnet build`

#### Creating a Release

```bash
# Create and push a new version tag
git tag v1.0.0
git push origin v1.0.0
```

This automatically triggers a release build with:
- Multiple framework versions (net48, net7.0-windows, net7.0)
- Automated release notes
- Downloadable .gha files

#### Local Development Builds

For local development and testing, use the provided build scripts:

**Windows (PowerShell):**
```powershell
.\scripts\build-local.ps1
# Or with custom parameters:
.\scripts\build-local.ps1 -Configuration Debug -Framework net7.0-windows
```

**Mac/Linux (Bash):**
```bash
./scripts/build-local.sh
# Or with custom parameters:
./scripts/build-local.sh Debug net7.0-windows
```

These scripts will:
- Build the project locally
- Copy the output to `releases/GH_MCP.gha`
- Show build information and file details

### Project Structure

```
grasshopper-mcp/
├── grasshopper_mcp/       # Python bridge server
│   ├── __init__.py
│   └── bridge.py          # Main bridge server implementation
├── GH_MCP/                # Grasshopper component (C#)
│   └── ...
├── .github/               # GitHub configuration
│   ├── workflows/         # GitHub Actions workflows
│   │   ├── build.yml      # Automated build workflow
│   │   └── release.yml    # Automated release workflow
│   └── dependabot.yml     # Automated dependency updates
├── scripts/               # Build and development scripts
│   ├── build-local.ps1    # Windows local build script
│   └── build-local.sh     # Mac/Linux local build script
├── releases/              # Pre-compiled binaries
│   └── GH_MCP.gha         # Compiled Grasshopper component
├── setup.py               # Python package setup
├── launch_mcp.py          # Python launcher script
└── README.md              # This file
```

### Development Tools

**Python Launcher (`launch_mcp.py`)**
The included Python launcher provides a robust way to start the MCP bridge server with:
- Automatic environment validation
- Dependency management with `uv`
- Port conflict detection
- Enhanced error reporting
- Cross-platform compatibility

Usage:
```bash
python launch_mcp.py
```

## Attribution

This project is based on the work of Alfred Chen (https://github.com/alfredatnycu/grasshopper-mcp/), licensed under the MIT License.

Significant modifications, new features, and ongoing maintenance are by Simon Wisdom, 2025–present.