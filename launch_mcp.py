#!/usr/bin/env python3
"""
Grasshopper MCP Bridge Server Launcher
Python-based alternative to shell script with enhanced error handling and performance
"""

import os
import sys
import subprocess
import json
import importlib.util
from pathlib import Path
from typing import Optional, List, Dict, Any

# ANSI color codes
class Colors:
    RED = '\033[0;31m'
    GREEN = '\033[0;32m'
    YELLOW = '\033[1;33m'
    BLUE = '\033[0;34m'
    NC = '\033[0m'  # No Color

def log_info(message: str) -> None:
    """Log info message with color"""
    print(f"{Colors.BLUE}[INFO]{Colors.NC} {message}", file=sys.stderr)

def log_success(message: str) -> None:
    """Log success message with color"""
    print(f"{Colors.GREEN}[SUCCESS]{Colors.NC} {message}", file=sys.stderr)

def log_warning(message: str) -> None:
    """Log warning message with color"""
    print(f"{Colors.YELLOW}[WARNING]{Colors.NC} {message}", file=sys.stderr)

def log_error(message: str) -> None:
    """Log error message with color"""
    print(f"{Colors.RED}[ERROR]{Colors.NC} {message}", file=sys.stderr)

class MCPLauncher:
    def __init__(self):
        self.script_dir = Path(__file__).parent
        self.mcp_dir = self.script_dir  # Now we're already in the project directory
        self.python_cmd = self._find_python()
        self.uv_cmd = "uv"
        
    def _find_python(self) -> str:
        """Find the best available Python command"""
        for cmd in ["python3", "python"]:
            try:
                result = subprocess.run([cmd, "--version"], 
                                      capture_output=True, text=True, check=True)
                log_info(f"Using Python: {cmd}")
                log_info(f"Python version: {result.stdout.strip()}")
                return cmd
            except (subprocess.CalledProcessError, FileNotFoundError):
                continue
        
        log_error("Python not found in PATH")
        sys.exit(1)
    
    def _check_uv(self) -> bool:
        """Check if uv is available"""
        try:
            subprocess.run([self.uv_cmd, "--version"], 
                         capture_output=True, check=True)
            return True
        except (subprocess.CalledProcessError, FileNotFoundError):
            return False
    
    def validate_environment(self) -> None:
        """Validate the environment"""
        log_info("Validating environment...")
        
        # Check MCP directory
        if not self.mcp_dir.exists():
            log_error(f"grasshopper-mcp-master directory not found at {self.mcp_dir}")
            sys.exit(1)
        
        # Check uv
        if not self._check_uv():
            log_error("uv is not installed. Please install with: curl -LsSf https://astral.sh/uv/install.sh | sh")
            sys.exit(1)
        
        log_success("Environment validation passed")
    
    def install_package(self) -> None:
        """Install/update the package"""
        log_info("Installing/updating grasshopper-mcp package...")
        
        try:
            # Change to MCP directory
            os.chdir(self.mcp_dir)
            
            # Install with uv
            result = subprocess.run([
                self.uv_cmd, "pip", "install", "-e", ".", "--quiet"
            ], capture_output=True, text=True, check=True)
            
            log_success("Package installed successfully")
            
        except subprocess.CalledProcessError as e:
            log_error(f"Failed to install package: {e}")
            if e.stderr:
                log_error(f"Error details: {e.stderr}")
            sys.exit(1)
    
    def validate_module(self) -> None:
        """Validate the grasshopper_mcp module"""
        log_info("Validating grasshopper_mcp module...")
        
        validation_script = """
import sys
import json
from pathlib import Path

try:
    import grasshopper_mcp.bridge
    print('Module imported successfully')
    
    # Check for enhanced components
    try:
        kb_path = Path('GH_MCP/GH_MCP/Resources/ComponentKnowledgeBase.json')
        if kb_path.exists():
            with open(kb_path, 'r') as f:
                data = json.load(f)
                components = [c['name'] for c in data['components']]
                enhanced_components = ['Divide Curve', 'Graph Mapper', 'Range', 'Amplitude', 'Move', 'Interpolate', 'Pipe']
                found = [c for c in enhanced_components if c in components]
                print(f'Enhanced components found: {len(found)}/{len(enhanced_components)}')
                if found:
                    print(f'Available: {found}')
        else:
            print('Component knowledge base not found at: ' + str(kb_path.absolute()))
    except Exception as e:
        print(f'Component validation warning: {e}')
    
    print('Module validation passed')
    
except ImportError as e:
    print(f'Module import failed: {e}')
    sys.exit(1)
except Exception as e:
    print(f'Module validation failed: {e}')
    sys.exit(1)
"""
        
        try:
            result = subprocess.run([
                self.uv_cmd, "run", self.python_cmd, "-c", validation_script
            ], capture_output=True, text=True, check=True)
            
            # Print validation output
            for line in result.stdout.strip().split('\n'):
                if line:
                    log_info(line)
            
            log_success("Module validation passed")
            
        except subprocess.CalledProcessError as e:
            log_error("Module validation failed")
            if e.stderr:
                log_error(f"Error details: {e.stderr}")
            sys.exit(1)
    
    def check_server_status(self) -> None:
        """Check if Grasshopper is ready to accept connections"""
        log_info("Checking if Grasshopper is ready on port 8080...")
        
        try:
            import socket
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(1)
            result = sock.connect_ex(('localhost', 8080))
            sock.close()
            
            if result == 0:
                # Port is in use, let's check what's running on it
                process_info = self._get_process_on_port(8080)
                if process_info:
                    if self._is_grasshopper_process(process_info):
                        log_success(f"Grasshopper is ready and listening on port 8080 (PID: {process_info['pid']})")
                    else:
                        log_warning(f"Port 8080 is in use by: {process_info['name']} (PID: {process_info['pid']})")
                        log_warning("This might conflict with Grasshopper. Please ensure:")
                        log_warning("1. Rhino and Grasshopper are running")
                        log_warning("2. The GH_MCP component is added to your Grasshopper canvas")
                        log_warning("3. The GH_MCP component is enabled and listening on port 8080")
                        response = input("Continue anyway? (y/N): ").strip().lower()
                        if response not in ['y', 'yes']:
                            log_info("Exiting...")
                            sys.exit(0)
                else:
                    log_success("Port 8080 is responding (process info unavailable)")
            else:
                log_warning("Port 8080 is not responding. Please ensure:")
                log_warning("1. Rhino and Grasshopper are running")
                log_warning("2. The GH_MCP component is added to your Grasshopper canvas")
                log_warning("3. The GH_MCP component is enabled and listening on port 8080")
                response = input("Continue anyway? (y/N): ").strip().lower()
                if response not in ['y', 'yes']:
                    log_info("Exiting...")
                    sys.exit(0)
                    
        except Exception as e:
            log_warning(f"Could not check port status: {e}")
    
    def _get_process_on_port(self, port: int) -> Optional[Dict[str, Any]]:
        """Get information about the process using a specific port"""
        try:
            # Try using lsof (macOS/Linux)
            result = subprocess.run([
                'lsof', '-i', f':{port}', '-t'
            ], capture_output=True, text=True, timeout=5)
            
            if result.returncode == 0 and result.stdout.strip():
                pid = result.stdout.strip().split('\n')[0]
                
                # Get process name
                name_result = subprocess.run([
                    'ps', '-p', pid, '-o', 'comm='
                ], capture_output=True, text=True, timeout=5)
                
                if name_result.returncode == 0:
                    process_name = name_result.stdout.strip()
                    return {
                        'pid': pid,
                        'name': process_name
                    }
            
            return None
            
        except (subprocess.TimeoutExpired, subprocess.CalledProcessError, FileNotFoundError):
            return None
    
    def _is_grasshopper_process(self, process_info: Dict[str, Any]) -> bool:
        """Check if the process is likely to be Grasshopper/Rhino"""
        name = process_info['name'].lower()
        
        # Common Grasshopper/Rhino process names (including macOS variants)
        grasshopper_names = [
            'rhino', 'rhinoceros', 'rhinocero', 'grasshopper', 'gh_mcp', 'mcp'
        ]
        
        return any(gh_name in name for gh_name in grasshopper_names)
    
    def launch_server(self) -> None:
        """Launch the MCP server"""
        log_info("Starting Enhanced MCP Bridge Server...")
        log_info("Supported components: Point, Line, Circle, Rectangle, Polyline, Ellipse, Polygon, Arc, Arc SED, Divide Curve, Evaluate Curve, Point On Curve, Offset, Shatter, Join Curves, Fillet, Trim, Loft, Sweep1, Sweep2, Revolve, Extrude, Boundary Surface, Cap Holes, Offset Surface, List Item, Cull Pattern, Graft, Flatten, Series, Range, Remap Numbers, Move, Rotate, Mirror, Scale, Orient, Bounding Box, Area, Volume, Center Box, Dispatch, Shift List, Flip Matrix, Number Slider, Graph Mapper, Amplitude, Interpolate, Pipe")
        log_info("New features: Component warning detection and canvas health analysis")
        log_info("=========================================")
        
        try:
            # Launch the server
            subprocess.run([
                self.uv_cmd, "run", self.python_cmd, "-m", "grasshopper_mcp.bridge"
            ], check=True)
            
        except subprocess.CalledProcessError as e:
            log_error(f"Server failed to start: {e}")
            sys.exit(1)
        except KeyboardInterrupt:
            log_info("Server stopped by user")
            sys.exit(0)
    
    def run(self) -> None:
        """Main execution method"""
        log_info("=== Grasshopper MCP Server Debug Info ===")
        log_info(f"Timestamp: {subprocess.run(['date'], capture_output=True, text=True).stdout.strip()}")
        log_info(f"Working Directory: {os.getcwd()}")
        log_info(f"Script Location: {__file__}")
        log_info(f"Arguments: {' '.join(sys.argv[1:])}")
        log_info(f"User: {os.getenv('USER', 'unknown')}")
        log_info(f"Python: {sys.executable}")
        
        self.validate_environment()
        self.install_package()
        self.validate_module()
        self.check_server_status()
        self.launch_server()

def main():
    """Main entry point"""
    launcher = MCPLauncher()
    launcher.run()

if __name__ == "__main__":
    main() 