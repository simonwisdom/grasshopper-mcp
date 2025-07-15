import socket
import json
import os
import sys
import traceback
import logging
from datetime import datetime
from typing import Dict, Any, Optional, List

# Use MCP server
from mcp.server.fastmcp import FastMCP

# Setup basic logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stderr)
    ]
)
logger = logging.getLogger(__name__)

# Set Grasshopper MCP connection parameters
GRASSHOPPER_HOST = "localhost"
GRASSHOPPER_PORT = int(os.environ.get("GRASSHOPPER_PORT", 8080))

# Create MCP server
server = FastMCP("Grasshopper Bridge")

# Custom exception hierarchy for MVP
class MCPError(Exception):
    """Base exception for MCP operations"""
    pass

class ConnectionError(MCPError):
    """Raised when connection to Grasshopper fails"""
    pass

class ValidationError(MCPError):
    """Raised when input validation fails"""
    pass

def validate_component_type(component_type: str) -> str:
    """Pass-through for component type, validation is handled by C#"""
    if not isinstance(component_type, str) or not component_type.strip():
        raise ValidationError("Component type must be a non-empty string.")
    logger.info(f"Component type '{component_type}' sent to GH for validation.")
    return component_type

def validate_coordinates(x: float, y: float) -> None:
    """Validate coordinate values"""
    if not isinstance(x, (int, float)) or not isinstance(y, (int, float)):
        raise ValidationError("Coordinates must be numeric values")
    
    if not (-10000 <= x <= 10000) or not (-10000 <= y <= 10000):
        raise ValidationError("Coordinates must be between -10000 and 10000")

def validate_path(path: str) -> None:
    """Validate file path"""
    if not path or not isinstance(path, str):
        raise ValidationError("Path must be a non-empty string")
    
    # Basic path validation - ensure it's not empty and has reasonable length
    if len(path.strip()) == 0:
        raise ValidationError("Path cannot be empty")
    
    if len(path) > 500:  # Reasonable max path length
        raise ValidationError("Path is too long")

def validate_component_id(component_id: str) -> None:
    """Validate component ID"""
    if not component_id or not isinstance(component_id, str):
        raise ValidationError("Component ID must be a non-empty string")

def send_to_grasshopper(command_type: str, params: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """Send command to Grasshopper MCP with enhanced error handling"""
    if params is None:
        params = {}
    
    # Create command
    command = {
        "type": command_type,
        "parameters": params
    }
    
    try:
        logger.info(f"Sending command to Grasshopper: {command_type} with params: {params}")
        
        # Connect to Grasshopper MCP
        client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client.settimeout(30)  # 30 second timeout
        client.connect((GRASSHOPPER_HOST, GRASSHOPPER_PORT))
        
        # Send command
        command_json = json.dumps(command)
        client.sendall((command_json + "\n").encode("utf-8"))
        logger.info(f"Command sent to Grasshopper: {command_json}")
        
        # Receive response
        response_data = b""
        while True:
            chunk = client.recv(4096)
            if not chunk:
                break
            response_data += chunk
            if response_data.endswith(b"\n"):
                break
        
        # Handle possible BOM
        response_str = response_data.decode("utf-8-sig").strip()
        logger.info(f"Response received from Grasshopper: {response_str}")
        
        # Parse JSON response
        response = json.loads(response_str)
        client.close()
        
        # Log success/failure
        if response.get("success", False):
            logger.info(f"Command {command_type} executed successfully")
        else:
            logger.warning(f"Command {command_type} failed: {response.get('error', 'Unknown error')}")
        
        return response
        
    except ConnectionRefusedError:
        logger.error("Connection refused - Grasshopper may not be running")
        return {
            "success": False,
            "error": "Grasshopper not running or not accessible"
        }
    except socket.timeout:
        logger.error("Connection timeout")
        return {
            "success": False,
            "error": "Connection timeout - Grasshopper may be unresponsive"
        }
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON response from Grasshopper: {e}")
        return {
            "success": False,
            "error": "Invalid response from Grasshopper"
        }
    except Exception as e:
        logger.error(f"Error communicating with Grasshopper: {str(e)}")
        logger.debug(f"Traceback: {traceback.format_exc()}")
        return {
            "success": False,
            "error": f"Error communicating with Grasshopper: {str(e)}"
        }

def safe_grasshopper_call(command_type: str, params: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """Safe wrapper for Grasshopper calls with logging"""
    try:
        return send_to_grasshopper(command_type, params)
    except Exception as e:
        logger.error(f"Error in {command_type}: {e}")
        return {"success": False, "error": str(e)}

# Register MCP tools
@server.tool("add_component")
def add_component(component_type: str, x: float, y: float):
    """
    Add a component to the Grasshopper canvas
    
    Args:
        component_type: Component type (point, curve, circle, line, panel, slider)
        x: X coordinate on the canvas
        y: Y coordinate on the canvas
    
    Returns:
        Result of adding the component
    """
    try:
        logger.info(f"Adding component: {component_type} at ({x}, {y})")
        
        # Validate inputs
        validated_type = validate_component_type(component_type)
        validate_coordinates(x, y)
        
        params = {
            "type": validated_type,
            "x": x,
            "y": y
        }
        
        result = safe_grasshopper_call("add_component", params)
        logger.info(f"Component added successfully: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in add_component: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Unexpected error in add_component: {e}")
        return {"success": False, "error": f"Unexpected error: {str(e)}"}

@server.tool("clear_document")
def clear_document():
    """Clear the Grasshopper document"""
    try:
        logger.info("Clearing Grasshopper document")
        result = safe_grasshopper_call("clear_document")
        logger.info(f"Document cleared: {result}")
        return result
    except Exception as e:
        logger.error(f"Error clearing document: {e}")
        return {"success": False, "error": str(e)}

@server.tool("save_document")
def save_document(path: str):
    """
    Save the Grasshopper document
    
    Args:
        path: Save path
    
    Returns:
        Result of the save operation
    """
    try:
        logger.info(f"Saving document to: {path}")
        
        # Validate path
        validate_path(path)
        
        params = {"path": path}
        result = safe_grasshopper_call("save_document", params)
        logger.info(f"Document saved: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in save_document: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error saving document: {e}")
        return {"success": False, "error": str(e)}

@server.tool("load_document")
def load_document(path: str):
    """
    Load a Grasshopper document
    
    Args:
        path: Document path
    
    Returns:
        Result of the load operation
    """
    try:
        logger.info(f"Loading document from: {path}")
        
        # Validate path
        validate_path(path)
        
        params = {"path": path}
        result = safe_grasshopper_call("load_document", params)
        logger.info(f"Document loaded: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in load_document: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error loading document: {e}")
        return {"success": False, "error": str(e)}

@server.tool("get_document_info")
def get_document_info():
    """Get information about the Grasshopper document"""
    try:
        logger.info("Getting document info")
        result = safe_grasshopper_call("get_document_info")
        logger.info(f"Document info retrieved: {result}")
        return result
    except Exception as e:
        logger.error(f"Error getting document info: {e}")
        return {"success": False, "error": str(e)}

@server.tool("health_check")
def health_check():
    """
    Basic health check for the MCP server and Grasshopper connection
    
    Returns:
        Health status information
    """
    try:
        logger.info("Performing health check")
        
        # Test basic connection
        ping_result = safe_grasshopper_call("ping")
        grasshopper_connected = ping_result.get("success", False)
        
        # Get basic system info
        status = "healthy" if grasshopper_connected else "unhealthy"
        
        health_info = {
            "status": status,
            "grasshopper_connected": grasshopper_connected,
            "timestamp": datetime.now().isoformat(),
            "server_info": {
                "host": GRASSHOPPER_HOST,
                "port": GRASSHOPPER_PORT
            }
        }
        
        # Add error details if unhealthy
        if not grasshopper_connected:
            health_info["error"] = ping_result.get("error", "Unknown error")
        
        logger.info(f"Health check completed: {status}")
        return health_info
        
    except Exception as e:
        logger.error(f"Error during health check: {e}")
        return {
            "status": "unhealthy",
            "error": str(e),
            "timestamp": datetime.now().isoformat()
        }

@server.tool("connect_components")
def connect_components(source_id: str, target_id: str, source_param: str = None, target_param: str = None, source_param_index: int = None, target_param_index: int = None):
    """
    Connect two components in the Grasshopper canvas
    
    Args:
        source_id: ID of the source component (output)
        target_id: ID of the target component (input)
        source_param: Name of the source parameter (optional)
        target_param: Name of the target parameter (optional)
        source_param_index: Index of the source parameter (optional, used if source_param is not provided)
        target_param_index: Index of the target parameter (optional, used if target_param is not provided)
    
    Returns:
        Result of connecting the components
    """
    try:
        logger.info(f"Connecting components: {source_id} -> {target_id}")
        
        # Validate component IDs
        validate_component_id(source_id)
        validate_component_id(target_id)
        
        # Get target component information, check if connection already exists
        target_info = safe_grasshopper_call("get_component_info", {"id": target_id})
        
        # Check component type, if it's a component requiring multiple inputs (e.g., Addition, Subtraction, etc.), smartly assign inputs
        if target_info and "result" in target_info and "type" in target_info["result"]:
            component_type = target_info["result"]["type"]
            
            # Get existing connections
            connections = safe_grasshopper_call("get_connections")
            existing_connections = []
            
            if connections and "result" in connections:
                for conn in connections["result"]:
                    if conn.get("targetId") == target_id:
                        existing_connections.append(conn)
            
            # For specific components requiring multiple inputs, automatically select the correct input port
            if component_type in ["Addition", "Subtraction", "Multiplication", "Division", "Math"]:
                # If target parameter is not specified and there is already a connection to the first input, automatically connect to the second input
                if target_param is None and target_param_index is None:
                    # Check if the first input is already occupied
                    first_input_occupied = False
                    for conn in existing_connections:
                        if conn.get("targetParam") == "A" or conn.get("targetParamIndex") == 0:
                            first_input_occupied = True
                            break
                    
                    # If the first input is occupied, connect to the second input
                    if first_input_occupied:
                        target_param = "B"  # The second input is usually named B
                    else:
                        target_param = "A"  # Otherwise, connect to the first input
        
        params = {
            "sourceId": source_id,
            "targetId": target_id
        }
        
        if source_param is not None:
            params["sourceParam"] = source_param
        elif source_param_index is not None:
            params["sourceParamIndex"] = source_param_index
            
        if target_param is not None:
            params["targetParam"] = target_param
        elif target_param_index is not None:
            params["targetParamIndex"] = target_param_index
        
        result = safe_grasshopper_call("connect_components", params)
        logger.info(f"Components connected: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in connect_components: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error connecting components: {e}")
        return {"success": False, "error": str(e)}

@server.tool("create_pattern")
def create_pattern(description: str):
    """
    Create a pattern of components based on a high-level description
    
    Args:
        description: High-level description of what to create (e.g., '3D voronoi cube')
    
    Returns:
        Result of creating the pattern
    """
    try:
        logger.info(f"Creating pattern: {description}")
        
        if not description or not isinstance(description, str):
            raise ValidationError("Description must be a non-empty string")
        
        params = {"description": description}
        result = safe_grasshopper_call("create_pattern", params)
        logger.info(f"Pattern created: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in create_pattern: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error creating pattern: {e}")
        return {"success": False, "error": str(e)}

@server.tool("get_available_patterns")
def get_available_patterns(query: str):
    """
    Get a list of available patterns that match a query
    
    Args:
        query: Query to search for patterns
    
    Returns:
        List of available patterns
    """
    try:
        logger.info(f"Getting available patterns for query: {query}")
        
        if not query or not isinstance(query, str):
            raise ValidationError("Query must be a non-empty string")
        
        params = {"query": query}
        result = safe_grasshopper_call("get_available_patterns", params)
        logger.info(f"Available patterns retrieved: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in get_available_patterns: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error getting available patterns: {e}")
        return {"success": False, "error": str(e)}

@server.tool("get_component_info")
def get_component_info(component_id: str):
    """
    Get detailed information about a specific component
    
    Args:
        component_id: ID of the component to get information about
    
    Returns:
        Detailed information about the component, including inputs, outputs, and current values
    """
    try:
        logger.info(f"Getting component info for: {component_id}")
        
        # Validate component ID
        validate_component_id(component_id)
        
        params = {"id": component_id}
        result = safe_grasshopper_call("get_component_info", params)
        
        # Enhance return result, add more parameter information
        if result and "result" in result:
            component_data = result["result"]
            
            # Get component type
            if "type" in component_data:
                component_type = component_data["type"]
                
                # Query component library, get detailed parameter information for that component type
                component_library = get_component_library()
                if "components" in component_library:
                    for lib_component in component_library["components"]:
                        if lib_component.get("name") == component_type or lib_component.get("fullName") == component_type:
                            # Merge component library parameter information into the return result
                            if "settings" in lib_component:
                                component_data["availableSettings"] = lib_component["settings"]
                            if "inputs" in lib_component:
                                component_data["inputDetails"] = lib_component["inputs"]
                            if "outputs" in lib_component:
                                component_data["outputDetails"] = lib_component["outputs"]
                            if "usage_examples" in lib_component:
                                component_data["usageExamples"] = lib_component["usage_examples"]
                            if "common_issues" in lib_component:
                                component_data["commonIssues"] = lib_component["common_issues"]
                            break
                
                # Special handling for certain component types
                if component_type == "Number Slider":
                    # Try to get the actual settings of the current slider from the component data
                    if "currentSettings" not in component_data:
                        component_data["currentSettings"] = {
                            "min": component_data.get("min", 0),
                            "max": component_data.get("max", 10),
                            "value": component_data.get("value", 5),
                            "rounding": component_data.get("rounding", 0.1),
                            "type": component_data.get("type", "float")
                        }
                
                # Add component connection information
                connections = safe_grasshopper_call("get_connections")
                if connections and "result" in connections:
                    # Find all connections related to this component
                    related_connections = []
                    for conn in connections["result"]:
                        if conn.get("sourceId") == component_id or conn.get("targetId") == component_id:
                            related_connections.append(conn)
                    
                    if related_connections:
                        component_data["connections"] = related_connections
        
        logger.info(f"Component info retrieved: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in get_component_info: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error getting component info: {e}")
        return {"success": False, "error": str(e)}

@server.tool("get_all_components")
def get_all_components():
    """
    Get a list of all components in the current document
    
    Returns:
        List of all components in the document with their IDs, types, and positions
    """
    try:
        logger.info("Getting all components")
        result = safe_grasshopper_call("get_all_components")
        
        # Enhance return result, add more parameter information for each component
        if result and "result" in result:
            components = result["result"]
            component_library = get_component_library()
            
            # Get all connection information
            connections = safe_grasshopper_call("get_connections")
            connections_data = connections.get("result", []) if connections else []
            
            # Add detailed information for each component
            for component in components:
                if "id" in component and "type" in component:
                    component_id = component["id"]
                    component_type = component["type"]
                    
                    # Add component detailed parameter information
                    if "components" in component_library:
                        for lib_component in component_library["components"]:
                            if lib_component.get("name") == component_type or lib_component.get("fullName") == component_type:
                                # Merge component library parameter information into component data
                                if "settings" in lib_component:
                                    component["availableSettings"] = lib_component["settings"]
                                if "inputs" in lib_component:
                                    component["inputDetails"] = lib_component["inputs"]
                                if "outputs" in lib_component:
                                    component["outputDetails"] = lib_component["outputs"]
                                break
                    
                    # Add component connection information
                    related_connections = []
                    for conn in connections_data:
                        if conn.get("sourceId") == component_id or conn.get("targetId") == component_id:
                            related_connections.append(conn)
                    
                    if related_connections:
                        component["connections"] = related_connections
                    
                    # Special handling for certain component types
                    if component_type == "Number Slider":
                        # Try to get the current settings of the slider
                        component_info = safe_grasshopper_call("get_component_info", {"id": component_id})
                        if component_info and "result" in component_info:
                            info_data = component_info["result"]
                            component["currentSettings"] = {
                                "min": info_data.get("min", 0),
                                "max": info_data.get("max", 10),
                                "value": info_data.get("value", 5),
                                "rounding": info_data.get("rounding", 0.1)
                            }
        
        logger.info(f"All components retrieved: {len(result.get('result', [])) if result else 0} components")
        return result
        
    except Exception as e:
        logger.error(f"Error getting all components: {e}")
        return {"success": False, "error": str(e)}

@server.tool("get_connections")
def get_connections():
    """
    Get a list of all connections between components in the current document
    
    Returns:
        List of all connections between components
    """
    try:
        logger.info("Getting all connections")
        result = safe_grasshopper_call("get_connections")
        logger.info(f"Connections retrieved: {len(result.get('result', [])) if result else 0} connections")
        return result
    except Exception as e:
        logger.error(f"Error getting connections: {e}")
        return {"success": False, "error": str(e)}

@server.tool("search_components")
def search_components(query: str):
    """
    Search for components by name or category
    
    Args:
        query: Search query
    
    Returns:
        List of components matching the search query
    """
    try:
        logger.info(f"Searching components with query: {query}")
        
        if not query or not isinstance(query, str):
            raise ValidationError("Query must be a non-empty string")
        
        params = {"query": query}
        result = safe_grasshopper_call("search_components", params)
        logger.info(f"Component search completed: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in search_components: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error searching components: {e}")
        return {"success": False, "error": str(e)}

@server.tool("get_component_parameters")
def get_component_parameters(component_type: str):
    """
    Get a list of parameters for a specific component type
    
    Args:
        component_type: Type of component to get parameters for
    
    Returns:
        List of input and output parameters for the component type
    """
    try:
        logger.info(f"Getting component parameters for: {component_type}")
        
        if not component_type or not isinstance(component_type, str):
            raise ValidationError("Component type must be a non-empty string")
        
        params = {"componentType": component_type}
        result = safe_grasshopper_call("get_component_parameters", params)
        logger.info(f"Component parameters retrieved: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in get_component_parameters: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error getting component parameters: {e}")
        return {"success": False, "error": str(e)}

@server.tool("validate_connection")
def validate_connection(source_id: str, target_id: str, source_param: str = None, target_param: str = None):
    """
    Validate if a connection between two components is possible
    
    Args:
        source_id: ID of the source component (output)
        target_id: ID of the target component (input)
        source_param: Name of the source parameter (optional)
        target_param: Name of the target parameter (optional)
    
    Returns:
        Whether the connection is valid and any potential issues
    """
    try:
        logger.info(f"Validating connection: {source_id} -> {target_id}")
        
        # Validate component IDs
        validate_component_id(source_id)
        validate_component_id(target_id)
        
        params = {
            "sourceId": source_id,
            "targetId": target_id
        }
        
        if source_param is not None:
            params["sourceParam"] = source_param
            
        if target_param is not None:
            params["targetParam"] = target_param
        
        result = safe_grasshopper_call("validate_connection", params)
        logger.info(f"Connection validation completed: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in validate_connection: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error validating connection: {e}")
        return {"success": False, "error": str(e)}

@server.tool("get_component_warnings")
def get_component_warnings(component_id: str = None):
    """
    Get warnings and errors for components in the Grasshopper canvas
    
    Args:
        component_id: ID of specific component to check (optional, if not provided checks all components)
    
    Returns:
        List of warnings and errors found on components
    """
    try:
        logger.info(f"Getting component warnings for component_id: {component_id}")
        
        params = {}
        if component_id is not None:
            validate_component_id(component_id)
            params["id"] = component_id
        
        result = safe_grasshopper_call("get_component_warnings", params)
        logger.info(f"Component warnings retrieved: {result}")
        return result
        
    except ValidationError as e:
        logger.error(f"Validation error in get_component_warnings: {e}")
        return {"success": False, "error": str(e)}
    except Exception as e:
        logger.error(f"Error getting component warnings: {e}")
        return {"success": False, "error": str(e)}

@server.tool("analyze_canvas_health")
def analyze_canvas_health():
    """
    Perform comprehensive health analysis of the Grasshopper canvas
    
    Returns:
        Detailed analysis of canvas health including warnings, errors, and suggestions
    """
    try:
        logger.info("Performing comprehensive canvas health analysis")
        
        # Get all warnings
        warnings_result = get_component_warnings()
        warnings = warnings_result.get("result", {}).get("warnings", []) if warnings_result.get("success") else []
        
        # Get all components
        components_result = get_all_components()
        components = components_result.get("result", []) if components_result.get("success") else []
        
        # Get all connections
        connections_result = safe_grasshopper_call("get_connections")
        connections = connections_result.get("result", []) if connections_result.get("success") else []
        
        # Analyze canvas health
        analysis = {
            "summary": {
                "total_components": len(components),
                "total_connections": len(connections),
                "total_warnings": len(warnings),
                "health_score": 100
            },
            "warnings": warnings,
            "issues": [],
            "suggestions": []
        }
        
        # Categorize warnings by severity
        errors = [w for w in warnings if w.get("level", "").lower() == "error"]
        warnings_list = [w for w in warnings if w.get("level", "").lower() == "warning"]
        remarks = [w for w in warnings if w.get("level", "").lower() == "remark"]
        
        analysis["summary"]["errors"] = len(errors)
        analysis["summary"]["warnings"] = len(warnings_list)
        analysis["summary"]["remarks"] = len(remarks)
        
        # Calculate health score (100 - errors*10 - warnings*5 - remarks*1)
        health_score = 100 - (len(errors) * 10) - (len(warnings_list) * 5) - (len(remarks) * 1)
        analysis["summary"]["health_score"] = max(0, health_score)
        
        # Generate suggestions based on common issues
        suggestions = []
        
        # Check for floating parameters
        floating_params = [w for w in warnings if w.get("source") == "floating_parameter"]
        if floating_params:
            suggestions.append({
                "category": "Floating Parameters",
                "description": f"Found {len(floating_params)} floating parameters",
                "suggestion": "Connect these parameters to appropriate sources or set default values",
                "components": [w.get("component", {}).get("name", "Unknown") for w in floating_params]
            })
        
        # Check for data collection issues
        data_issues = [w for w in warnings if w.get("source") == "data_collection"]
        if data_issues:
            suggestions.append({
                "category": "Data Collection Issues",
                "description": f"Found {len(data_issues)} parameters with data collection problems",
                "suggestion": "Check the source components and ensure they are properly connected and have valid data",
                "components": [w.get("component", {}).get("name", "Unknown") for w in data_issues]
            })
        
        # Check for hidden components
        hidden_components = [w for w in warnings if w.get("source") == "component_state" and "hidden" in w.get("text", "").lower()]
        if hidden_components:
            suggestions.append({
                "category": "Hidden Components",
                "description": f"Found {len(hidden_components)} hidden components",
                "suggestion": "Consider showing these components if they are needed for the definition",
                "components": [w.get("component", {}).get("name", "Unknown") for w in hidden_components]
            })
        
        # Check for locked components
        locked_components = [w for w in warnings if w.get("source") == "component_state" and "locked" in w.get("text", "").lower()]
        if locked_components:
            suggestions.append({
                "category": "Locked Components",
                "description": f"Found {len(locked_components)} locked components",
                "suggestion": "Unlock these components if you need to modify them",
                "components": [w.get("component", {}).get("name", "Unknown") for w in locked_components]
            })
        
        # General suggestions based on canvas state
        if len(components) == 0:
            suggestions.append({
                "category": "Empty Canvas",
                "description": "No components found on canvas",
                "suggestion": "Add components to start building your definition"
            })
        
        if len(connections) == 0 and len(components) > 1:
            suggestions.append({
                "category": "Unconnected Components",
                "description": f"Found {len(components)} components but no connections",
                "suggestion": "Connect components to create a functional definition"
            })
        
        # Check for common component patterns that might need attention
        component_types = [c.get("type", "") for c in components]
        
        if "Number Slider" in component_types and "Addition" in component_types:
            suggestions.append({
                "category": "Math Operations",
                "description": "Found Number Slider and Addition components",
                "suggestion": "Ensure Number Sliders are connected to Addition inputs A and B in the correct order"
            })
        
        if "Circle" in component_types and "XY Plane" not in component_types:
            suggestions.append({
                "category": "Plane Inputs",
                "description": "Found Circle component but no XY Plane",
                "suggestion": "Add XY Plane component to provide plane input for Circle"
            })
        
        analysis["suggestions"] = suggestions
        
        # Generate health status
        if analysis["summary"]["health_score"] >= 90:
            analysis["status"] = "Excellent"
            analysis["status_description"] = "Canvas is in excellent condition with minimal issues"
        elif analysis["summary"]["health_score"] >= 75:
            analysis["status"] = "Good"
            analysis["status_description"] = "Canvas is in good condition with some minor issues"
        elif analysis["summary"]["health_score"] >= 50:
            analysis["status"] = "Fair"
            analysis["status_description"] = "Canvas has several issues that should be addressed"
        else:
            analysis["status"] = "Poor"
            analysis["status_description"] = "Canvas has significant issues that need immediate attention"
        
        logger.info(f"Canvas health analysis completed: {analysis['summary']['health_score']}/100")
        return analysis
        
    except Exception as e:
        logger.error(f"Error analyzing canvas health: {e}")
        return {"success": False, "error": str(e)}

# Register MCP resources
@server.resource("grasshopper://status")
def get_grasshopper_status():
    """Get Grasshopper status"""
    try:
        logger.info("Getting Grasshopper status")
        
        # Get document information
        doc_info = safe_grasshopper_call("get_document_info")
        
        # Get all components (using enhanced get_all_components)
        components_result = get_all_components()
        components = components_result.get("result", []) if components_result else []
        
        # Get all connections
        connections = safe_grasshopper_call("get_connections")
        
        # Add hints for common components
        component_hints = {
            "Number Slider": {
                "description": "Single numeric value slider with adjustable range",
                "common_usage": "Use for single numeric inputs like radius, height, count, etc.",
                "parameters": ["min", "max", "value", "rounding", "type"],
                "NOT_TO_BE_CONFUSED_WITH": "MD Slider (which is for multi-dimensional values)"
            },
            "MD Slider": {
                "description": "Multi-dimensional slider for vector input",
                "common_usage": "Use for vector inputs, NOT for simple numeric values",
                "NOT_TO_BE_CONFUSED_WITH": "Number Slider (which is for single numeric values)"
            },
            "Panel": {
                "description": "Displays text or numeric data",
                "common_usage": "Use for displaying outputs and debugging"
            },
            "Addition": {
                "description": "Adds two or more numbers",
                "common_usage": "Connect two Number Sliders to inputs A and B",
                "parameters": ["A", "B"],
                "connection_tip": "First slider should connect to input A, second to input B"
            },
            "Divide Curve": {
                "description": "Divides a curve into equal segments",
                "common_usage": "Connect a curve and count value to get division points",
                "parameters": ["Curve", "Count", "Kinks"],
                "connection_tip": "Use with Circle or Line to create point arrays"
            },
            "Graph Mapper": {
                "description": "Maps input values through a visual graph function",
                "common_usage": "Connect Number Slider to modify value distributions",
                "parameters": ["Input"],
                "connection_tip": "Edit the graph curve to control mapping behavior"
            },
            "Range": {
                "description": "Creates a range of numbers between domain limits",
                "common_usage": "Connect Domain interval and Steps count for number sequences",
                "parameters": ["Domain", "Steps"],
                "connection_tip": "Use with Construct Domain for custom ranges"
            },
            "Amplitude": {
                "description": "Gets the length/magnitude of a vector",
                "common_usage": "Connect a vector to get its amplitude and unit vector",
                "parameters": ["Vector"],
                "connection_tip": "Useful for measuring distances and normalizing vectors"
            },
            "Move": {
                "description": "Translates geometry along a vector",
                "common_usage": "Connect geometry and motion vector to move objects",
                "parameters": ["Geometry", "Motion"],
                "connection_tip": "Use with Unit Vector scaled by distance for precise movement"
            },
            "Interpolate": {
                "description": "Creates smooth curves through points",
                "common_usage": "Connect a list of points to create interpolated curve",
                "parameters": ["Vertices", "Degree", "Periodic"],
                "connection_tip": "Use with Divide Curve points for smooth curve networks"
            },
            "Pipe": {
                "description": "Creates pipe/tube geometry around a curve",
                "common_usage": "Connect curve and radius to create cylindrical geometry",
                "parameters": ["Curve", "Radius", "Caps"],
                "connection_tip": "Great for creating structural elements or organic forms"
            },
            "List Item": {
                "description": "Extracts specific items from lists by index",
                "common_usage": "Connect a list and index number to get specific items",
                "parameters": ["List", "Index"],
                "connection_tip": "Use with Series or Range to access specific list positions"
            },
            "Cull Pattern": {
                "description": "Removes items from lists based on boolean pattern",
                "common_usage": "Connect list and boolean pattern to filter items",
                "parameters": ["List", "Pattern"],
                "connection_tip": "Use with conditional logic to selectively filter data"
            },
            "Graft": {
                "description": "Converts data tree structure to grafted format",
                "common_usage": "Connect data to restructure tree hierarchy",
                "parameters": ["Data"],
                "connection_tip": "Essential for complex data tree manipulations"
            },
            "Flatten": {
                "description": "Flattens data tree structure to single list",
                "common_usage": "Connect data to simplify tree structure",
                "parameters": ["Data"],
                "connection_tip": "Use to simplify complex data hierarchies"
            },
            "Series": {
                "description": "Creates a series of numbers with start, step, and count",
                "common_usage": "Connect start, step, and count values for number sequences",
                "parameters": ["Start", "Step", "Count"],
                "connection_tip": "Fundamental for creating parametric sequences"
            },
            "Offset": {
                "description": "Creates parallel curves at specified distance",
                "common_usage": "Connect curve and distance to create parallel geometry",
                "parameters": ["Curve", "Distance", "Side", "Corners"],
                "connection_tip": "Essential for architectural wall thickness and margins"
            },
            "Shatter": {
                "description": "Splits curves at parameter points",
                "common_usage": "Connect curve and parameters to split into segments",
                "parameters": ["Curve", "Parameters"],
                "connection_tip": "Use with Divide Curve parameters for precise splitting"
            },
            "Join Curves": {
                "description": "Connects curves into continuous curves",
                "common_usage": "Connect multiple curves to create continuous geometry",
                "parameters": ["Curves", "Tolerance"],
                "connection_tip": "Essential for creating complete closed boundaries"
            },
            "Loft": {
                "description": "Creates surfaces through curve profiles",
                "common_usage": "Connect profile curves to create complex surfaces",
                "parameters": ["Profiles", "Rails", "Closed", "Type"],
                "connection_tip": "Fundamental surface creation tool for organic forms"
            },
            "Rotate": {
                "description": "Rotates geometry around an axis",
                "common_usage": "Connect geometry, angle, and axis for rotation",
                "parameters": ["Geometry", "Angle", "Axis", "Center"],
                "connection_tip": "Use with Unit Vector and Number Slider for precise rotations"
            },
            "Polyline": {
                "description": "Creates connected line segments",
                "common_usage": "Connect points to create polygonal shapes",
                "parameters": ["Vertices"],
                "connection_tip": "Great for building outlines and polygonal geometry"
            }
        }
        
        # Add current parameter values summary for each component
        component_summaries = []
        for component in components:
            summary = {
                "id": component.get("id", ""),
                "type": component.get("type", ""),
                "position": {
                    "x": component.get("x", 0),
                    "y": component.get("y", 0)
                }
            }
            
            # Add component specific parameter information
            if "currentSettings" in component:
                summary["settings"] = component["currentSettings"]
            elif component.get("type") == "Number Slider":
                # Try to extract slider settings from component information
                summary["settings"] = {
                    "min": component.get("min", 0),
                    "max": component.get("max", 10),
                    "value": component.get("value", 5),
                    "rounding": component.get("rounding", 0.1)
                }
            
            # Add connection information summary
            if "connections" in component:
                conn_summary = []
                for conn in component["connections"]:
                    if conn.get("sourceId") == component.get("id"):
                        conn_summary.append({
                            "type": "output",
                            "to": conn.get("targetId", ""),
                            "sourceParam": conn.get("sourceParam", ""),
                            "targetParam": conn.get("targetParam", "")
                        })
                    else:
                        conn_summary.append({
                            "type": "input",
                            "from": conn.get("sourceId", ""),
                            "sourceParam": conn.get("sourceParam", ""),
                            "targetParam": conn.get("targetParam", "")
                        })
                
                if conn_summary:
                    summary["connections"] = conn_summary
            
            component_summaries.append(summary)
        
        logger.info(f"Grasshopper status retrieved: {len(component_summaries)} components, {len(connections.get('result', []))} connections")
        return {
            "status": "Connected to Grasshopper",
            "document": doc_info.get("result", {}),
            "components": component_summaries,
            "connections": connections.get("result", []),
            "component_hints": component_hints,
            "recommendations": [
                "When needing a simple numeric input control, ALWAYS use 'Number Slider', not MD Slider",
                "For vector inputs (like 3D points), use 'MD Slider' or 'Construct Point' with multiple Number Sliders",
                "Use 'Panel' to display outputs and debug values",
                "When connecting multiple sliders to Addition, first slider goes to input A, second to input B"
            ],
            "canvas_summary": f"Current canvas has {len(component_summaries)} components and {len(connections.get('result', []))} connections"
        }
    except Exception as e:
        logger.error(f"Error getting Grasshopper status: {str(e)}")
        return {
            "status": f"Error: {str(e)}",
            "document": {},
            "components": [],
            "connections": []
        }

@server.resource("grasshopper://component_guide")
def get_component_guide():
    """Get guide for Grasshopper components and connections"""
    return {
        "title": "Grasshopper Component Guide",
        "description": "Guide for creating and connecting Grasshopper components",
        "components": [
            {
                "name": "Point",
                "category": "Params",
                "description": "Creates a point at specific coordinates",
                "inputs": [
                    {"name": "X", "type": "Number"},
                    {"name": "Y", "type": "Number"},
                    {"name": "Z", "type": "Number"}
                ],
                "outputs": [
                    {"name": "Pt", "type": "Point"}
                ]
            },
            {
                "name": "Circle",
                "category": "Curve",
                "description": "Creates a circle",
                "inputs": [
                    {"name": "Plane", "type": "Plane", "description": "Base plane for the circle"},
                    {"name": "Radius", "type": "Number", "description": "Circle radius"}
                ],
                "outputs": [
                    {"name": "C", "type": "Circle"}
                ]
            },
            {
                "name": "XY Plane",
                "category": "Vector",
                "description": "Creates an XY plane at the world origin or at a specified point",
                "inputs": [
                    {"name": "Origin", "type": "Point", "description": "Origin point", "optional": True}
                ],
                "outputs": [
                    {"name": "Plane", "type": "Plane", "description": "XY plane"}
                ]
            },
            {
                "name": "Addition",
                "fullName": "Addition",
                "description": "Adds two or more numbers",
                "inputs": [
                    {"name": "A", "type": "Number", "description": "First input value"},
                    {"name": "B", "type": "Number", "description": "Second input value"}
                ],
                "outputs": [
                    {"name": "Result", "type": "Number", "description": "Sum of inputs"}
                ],
                "usage_examples": [
                    "Connect two Number Sliders to inputs A and B to add their values",
                    "Connect multiple values to add them all together"
                ],
                "common_issues": [
                    "When connecting multiple sliders, ensure they connect to different inputs (A and B)",
                    "The first slider should connect to input A, the second to input B"
                ]
            },
            {
                "name": "Number Slider",
                "fullName": "Number Slider",
                "description": "Creates a slider for numeric input with adjustable range and precision",
                "inputs": [],
                "outputs": [
                    {"name": "N", "type": "Number", "description": "Number output"}
                ],
                "settings": {
                    "min": {"description": "Minimum value of the slider", "default": 0},
                    "max": {"description": "Maximum value of the slider", "default": 10},
                    "value": {"description": "Current value of the slider", "default": 5},
                    "rounding": {"description": "Rounding precision (0.01, 0.1, 1, etc.)", "default": 0.1},
                    "type": {"description": "Slider type (integer, floating point)", "default": "float"},
                    "name": {"description": "Custom name for the slider", "default": ""}
                },
                "usage_examples": [
                    "Create a Number Slider with min=0, max=100, value=50",
                    "Create a Number Slider for radius with min=0.1, max=10, value=2.5, rounding=0.1"
                ],
                "common_issues": [
                    "Confusing with other slider types",
                    "Not setting appropriate min/max values for the intended use"
                ],
                "disambiguation": {
                    "similar_components": [
                        {
                            "name": "MD Slider",
                            "description": "Multi-dimensional slider for vector input, NOT for simple numeric values",
                            "how_to_distinguish": "Use Number Slider for single numeric values; use MD Slider only when you need multi-dimensional control"
                        },
                        {
                            "name": "Graph Mapper",
                            "description": "Maps values through a graph function, NOT a simple slider",
                            "how_to_distinguish": "Use Number Slider for direct numeric input; use Graph Mapper only for function-based mapping"
                        }
                    ],
                    "correct_usage": "When needing a simple numeric input control, ALWAYS use 'Number Slider', not MD Slider or other variants"
                }
            },
            {
                "name": "Panel",
                "fullName": "Panel",
                "description": "Displays text or numeric data",
                "inputs": [
                    {"name": "Input", "type": "Any"}
                ],
                "outputs": []
            },
            {
                "name": "Math",
                "fullName": "Mathematics",
                "description": "Performs mathematical operations",
                "inputs": [
                    {"name": "A", "type": "Number"},
                    {"name": "B", "type": "Number"}
                ],
                "outputs": [
                    {"name": "Result", "type": "Number"}
                ],
                "operations": ["Addition", "Subtraction", "Multiplication", "Division", "Power", "Modulo"]
            },
            {
                "name": "Construct Point",
                "fullName": "Construct Point",
                "description": "Constructs a point from X, Y, Z coordinates",
                "inputs": [
                    {"name": "X", "type": "Number"},
                    {"name": "Y", "type": "Number"},
                    {"name": "Z", "type": "Number"}
                ],
                "outputs": [
                    {"name": "Pt", "type": "Point"}
                ]
            },
            {
                "name": "Line",
                "fullName": "Line",
                "description": "Creates a line between two points",
                "inputs": [
                    {"name": "Start", "type": "Point"},
                    {"name": "End", "type": "Point"}
                ],
                "outputs": [
                    {"name": "L", "type": "Line"}
                ]
            },
            {
                "name": "Extrude",
                "fullName": "Extrude",
                "description": "Extrudes a curve to create a surface or a solid",
                "inputs": [
                    {"name": "Base", "type": "Curve"},
                    {"name": "Direction", "type": "Vector"},
                    {"name": "Height", "type": "Number"}
                ],
                "outputs": [
                    {"name": "Brep", "type": "Brep"}
                ]
            },
            {
                "name": "List Item",
                "fullName": "List Item",
                "description": "Extracts specific items from lists by index",
                "inputs": [
                    {"name": "List", "type": "Any"},
                    {"name": "Index", "type": "Integer"}
                ],
                "outputs": [
                    {"name": "Item", "type": "Any"}
                ]
            },
            {
                "name": "Cull Pattern",
                "fullName": "Cull Pattern",
                "description": "Removes items from lists based on boolean pattern",
                "inputs": [
                    {"name": "List", "type": "Any"},
                    {"name": "Pattern", "type": "Boolean"}
                ],
                "outputs": [
                    {"name": "List", "type": "Any"}
                ]
            },
            {
                "name": "Graft",
                "fullName": "Graft",
                "description": "Converts data tree structure to grafted format",
                "inputs": [
                    {"name": "Data", "type": "Any"}
                ],
                "outputs": [
                    {"name": "Data", "type": "Any"}
                ]
            },
            {
                "name": "Flatten",
                "fullName": "Flatten",
                "description": "Flattens data tree structure to single list",
                "inputs": [
                    {"name": "Data", "type": "Any"}
                ],
                "outputs": [
                    {"name": "Data", "type": "Any"}
                ]
            },
            {
                "name": "Series",
                "fullName": "Series",
                "description": "Creates a series of numbers with start, step, and count",
                "inputs": [
                    {"name": "Start", "type": "Number"},
                    {"name": "Step", "type": "Number"},
                    {"name": "Count", "type": "Integer"}
                ],
                "outputs": [
                    {"name": "Series", "type": "Number"}
                ]
            },
            {
                "name": "Offset",
                "fullName": "Offset",
                "description": "Creates parallel curves at specified distance",
                "inputs": [
                    {"name": "Curve", "type": "Curve"},
                    {"name": "Distance", "type": "Number"},
                    {"name": "Side", "type": "Integer", "optional": True},
                    {"name": "Corners", "type": "Integer", "optional": True}
                ],
                "outputs": [
                    {"name": "Curve", "type": "Curve"}
                ]
            },
            {
                "name": "Shatter",
                "fullName": "Shatter",
                "description": "Splits curves at parameter points",
                "inputs": [
                    {"name": "Curve", "type": "Curve"},
                    {"name": "Parameters", "type": "Number"}
                ],
                "outputs": [
                    {"name": "Curves", "type": "Curve"}
                ]
            },
            {
                "name": "Join Curves",
                "fullName": "Join Curves",
                "description": "Connects curves into continuous curves",
                "inputs": [
                    {"name": "Curves", "type": "Curve"},
                    {"name": "Tolerance", "type": "Number", "optional": True}
                ],
                "outputs": [
                    {"name": "Curve", "type": "Curve"}
                ]
            },
            {
                "name": "Loft",
                "fullName": "Loft",
                "description": "Creates surfaces through curve profiles",
                "inputs": [
                    {"name": "Profiles", "type": "Curve"},
                    {"name": "Rails", "type": "Curve", "optional": True},
                    {"name": "Closed", "type": "Boolean", "optional": True},
                    {"name": "Type", "type": "Integer", "optional": True}
                ],
                "outputs": [
                    {"name": "Surface", "type": "Surface"}
                ]
            },
            {
                "name": "Rotate",
                "fullName": "Rotate",
                "description": "Rotates geometry around an axis",
                "inputs": [
                    {"name": "Geometry", "type": "Geometry"},
                    {"name": "Angle", "type": "Number"},
                    {"name": "Axis", "type": "Line"},
                    {"name": "Center", "type": "Point", "optional": True}
                ],
                "outputs": [
                    {"name": "Geometry", "type": "Geometry"},
                    {"name": "Transform", "type": "Transform"}
                ]
            },
            {
                "name": "Polyline",
                "fullName": "Polyline",
                "description": "Creates connected line segments",
                "inputs": [
                    {"name": "Vertices", "type": "Point"}
                ],
                "outputs": [
                    {"name": "Polyline", "type": "Curve"}
                ]
            },
            {
                "name": "Ellipse",
                "fullName": "Ellipse",
                "description": "Creates an ellipse from a plane and major/minor radii",
                "inputs": [
                    {"name": "Plane", "type": "Plane", "description": "Ellipse plane"},
                    {"name": "Major", "type": "Number", "description": "Major radius"},
                    {"name": "Minor", "type": "Number", "description": "Minor radius"}
                ],
                "outputs": [
                    {"name": "Ellipse", "type": "Curve"}
                ]
            },
            {
                "name": "Polygon",
                "fullName": "Polygon",
                "description": "Creates a regular polygon",
                "inputs": [
                    {"name": "Plane", "type": "Plane", "description": "Polygon plane"},
                    {"name": "Radius", "type": "Number", "description": "Polygon radius"},
                    {"name": "Sides", "type": "Integer", "description": "Number of sides"}
                ],
                "outputs": [
                    {"name": "Polygon", "type": "Curve"}
                ]
            },
            {
                "name": "Arc",
                "fullName": "Arc",
                "description": "Creates an arc from center, radius, and angles",
                "inputs": [
                    {"name": "Center", "type": "Point", "description": "Arc center"},
                    {"name": "Radius", "type": "Number", "description": "Arc radius"},
                    {"name": "Start Angle", "type": "Number", "description": "Start angle in radians"},
                    {"name": "End Angle", "type": "Number", "description": "End angle in radians"}
                ],
                "outputs": [
                    {"name": "Arc", "type": "Curve"}
                ]
            },
            {
                "name": "Arc SED",
                "fullName": "Arc SED",
                "description": "Creates an arc from start, end, and direction",
                "inputs": [
                    {"name": "Start Point", "type": "Point", "description": "Start point"},
                    {"name": "End Point", "type": "Point", "description": "End point"},
                    {"name": "Direction", "type": "Vector", "description": "Arc direction"}
                ],
                "outputs": [
                    {"name": "Arc", "type": "Curve"}
                ]
            },
            {
                "name": "Evaluate Curve",
                "fullName": "Evaluate Curve",
                "description": "Evaluates a curve at a parameter value",
                "inputs": [
                    {"name": "Curve", "type": "Curve", "description": "Curve to evaluate"},
                    {"name": "Parameter", "type": "Number", "description": "Parameter value (0-1)"}
                ],
                "outputs": [
                    {"name": "Point", "type": "Point", "description": "Point on curve"},
                    {"name": "Tangent", "type": "Vector", "description": "Tangent vector"},
                    {"name": "Normal", "type": "Vector", "description": "Normal vector"},
                    {"name": "Curvature", "type": "Number", "description": "Curvature value"}
                ]
            },
            {
                "name": "Point On Curve",
                "fullName": "Point On Curve",
                "description": "Finds the closest point on a curve",
                "inputs": [
                    {"name": "Curve", "type": "Curve", "description": "Target curve"},
                    {"name": "Point", "type": "Point", "description": "Test point"}
                ],
                "outputs": [
                    {"name": "Point", "type": "Point", "description": "Closest point on curve"},
                    {"name": "Parameter", "type": "Number", "description": "Parameter value"}
                ]
            },
            {
                "name": "Remap Numbers",
                "fullName": "Remap Numbers",
                "description": "Remaps numbers from one domain to another",
                "inputs": [
                    {"name": "Source", "type": "Number", "description": "Source values"},
                    {"name": "Source Domain", "type": "Interval", "description": "Source domain"},
                    {"name": "Target Domain", "type": "Interval", "description": "Target domain"}
                ],
                "outputs": [
                    {"name": "Target", "type": "Number", "description": "Remapped values"}
                ]
            },
            {
                "name": "Sweep1",
                "fullName": "Sweep1",
                "description": "Creates a surface by sweeping a profile along a path",
                "inputs": [
                    {"name": "Profile", "type": "Curve", "description": "Profile curve"},
                    {"name": "Path", "type": "Curve", "description": "Sweep path"}
                ],
                "outputs": [
                    {"name": "Surface", "type": "Surface"}
                ]
            },
            {
                "name": "Sweep2",
                "fullName": "Sweep2",
                "description": "Creates a surface by sweeping a profile along two rails",
                "inputs": [
                    {"name": "Profile", "type": "Curve", "description": "Profile curve"},
                    {"name": "Rail 1", "type": "Curve", "description": "First rail"},
                    {"name": "Rail 2", "type": "Curve", "description": "Second rail"}
                ],
                "outputs": [
                    {"name": "Surface", "type": "Surface"}
                ]
            },
            {
                "name": "Revolve",
                "fullName": "Revolve",
                "description": "Creates a surface by revolving a curve around an axis",
                "inputs": [
                    {"name": "Curve", "type": "Curve", "description": "Curve to revolve"},
                    {"name": "Axis", "type": "Line", "description": "Revolve axis"},
                    {"name": "Angle", "type": "Number", "description": "Revolve angle in radians"}
                ],
                "outputs": [
                    {"name": "Surface", "type": "Surface"}
                ]
            },
            {
                "name": "Cap Holes",
                "fullName": "Cap Holes",
                "description": "Caps holes in surfaces or breps",
                "inputs": [
                    {"name": "Geometry", "type": "Geometry", "description": "Geometry with holes"}
                ],
                "outputs": [
                    {"name": "Geometry", "type": "Geometry"}
                ]
            },
            {
                "name": "Offset Surface",
                "fullName": "Offset Surface",
                "description": "Creates offset surfaces",
                "inputs": [
                    {"name": "Surface", "type": "Surface", "description": "Base surface"},
                    {"name": "Distance", "type": "Number", "description": "Offset distance"}
                ],
                "outputs": [
                    {"name": "Surface", "type": "Surface"}
                ]
            },
            {
                "name": "Fillet",
                "fullName": "Fillet",
                "description": "Creates fillet curves between two curves",
                "inputs": [
                    {"name": "Curve A", "type": "Curve", "description": "First curve"},
                    {"name": "Curve B", "type": "Curve", "description": "Second curve"},
                    {"name": "Radius", "type": "Number", "description": "Fillet radius"}
                ],
                "outputs": [
                    {"name": "Fillet", "type": "Curve"}
                ]
            },
            {
                "name": "Trim",
                "fullName": "Trim",
                "description": "Trims curves using cutting objects",
                "inputs": [
                    {"name": "Curve", "type": "Curve", "description": "Curve to trim"},
                    {"name": "Cutter", "type": "Geometry", "description": "Cutting object"}
                ],
                "outputs": [
                    {"name": "Curve", "type": "Curve"}
                ]
            },
            {
                "name": "Mirror",
                "fullName": "Mirror",
                "description": "Mirrors geometry across a plane",
                "inputs": [
                    {"name": "Geometry", "type": "Geometry", "description": "Geometry to mirror"},
                    {"name": "Mirror Plane", "type": "Plane", "description": "Mirror plane"}
                ],
                "outputs": [
                    {"name": "Geometry", "type": "Geometry"}
                ]
            },
            {
                "name": "Scale",
                "fullName": "Scale",
                "description": "Scales geometry by factors",
                "inputs": [
                    {"name": "Geometry", "type": "Geometry", "description": "Geometry to scale"},
                    {"name": "Factors", "type": "Number", "description": "Scale factors"},
                    {"name": "Center", "type": "Point", "description": "Scale center"}
                ],
                "outputs": [
                    {"name": "Geometry", "type": "Geometry"}
                ]
            },
            {
                "name": "Orient",
                "fullName": "Orient",
                "description": "Orients geometry from reference to target plane",
                "inputs": [
                    {"name": "Geometry", "type": "Geometry", "description": "Geometry to orient"},
                    {"name": "Reference", "type": "Plane", "description": "Reference plane"},
                    {"name": "Target Plane", "type": "Plane", "description": "Target plane"}
                ],
                "outputs": [
                    {"name": "Geometry", "type": "Geometry"}
                ]
            },
            {
                "name": "Bounding Box",
                "fullName": "Bounding Box",
                "description": "Creates a bounding box around geometry",
                "inputs": [
                    {"name": "Geometry", "type": "Geometry", "description": "Input geometry"}
                ],
                "outputs": [
                    {"name": "Box", "type": "Brep"}
                ]
            },
            {
                "name": "Area",
                "fullName": "Area",
                "description": "Calculates the area of surfaces",
                "inputs": [
                    {"name": "Surface", "type": "Surface", "description": "Input surface"}
                ],
                "outputs": [
                    {"name": "Area", "type": "Number"}
                ]
            },
            {
                "name": "Volume",
                "fullName": "Volume",
                "description": "Calculates the volume of breps",
                "inputs": [
                    {"name": "Brep", "type": "Brep", "description": "Input brep"}
                ],
                "outputs": [
                    {"name": "Volume", "type": "Number"}
                ]
            },
            {
                "name": "Center Box",
                "fullName": "Center Box",
                "description": "Creates a box centered on geometry",
                "inputs": [
                    {"name": "Geometry", "type": "Geometry", "description": "Input geometry"},
                    {"name": "Size", "type": "Number", "description": "Box size"}
                ],
                "outputs": [
                    {"name": "Box", "type": "Brep"}
                ]
            },
            {
                "name": "Dispatch",
                "fullName": "Dispatch",
                "description": "Dispatches items to different outputs based on pattern",
                "inputs": [
                    {"name": "List", "type": "Any", "description": "Input list"},
                    {"name": "Pattern", "type": "Boolean", "description": "Dispatch pattern"}
                ],
                "outputs": [
                    {"name": "A", "type": "Any", "description": "Items matching pattern"},
                    {"name": "B", "type": "Any", "description": "Items not matching pattern"}
                ]
            },
            {
                "name": "Shift List",
                "fullName": "Shift List",
                "description": "Shifts items in a list",
                "inputs": [
                    {"name": "List", "type": "Any", "description": "Input list"},
                    {"name": "Shift", "type": "Integer", "description": "Shift amount"},
                    {"name": "Wrap", "type": "Boolean", "description": "Wrap around", "optional": True}
                ],
                "outputs": [
                    {"name": "List", "type": "Any"}
                ]
            },
            {
                "name": "Flip Matrix",
                "fullName": "Flip Matrix",
                "description": "Flips the rows and columns of a matrix",
                "inputs": [
                    {"name": "Matrix", "type": "Any", "description": "Input matrix"}
                ],
                "outputs": [
                    {"name": "Matrix", "type": "Any"}
                ]
            }
        ],
        "connectionRules": [
            {
                "from": "Number",
                "to": "Circle.Radius",
                "description": "Connect a number to the radius input of a circle"
            },
            {
                "from": "Point",
                "to": "Circle.Plane",
                "description": "Connect a point to the plane input of a circle (not recommended, use XY Plane instead)"
            },
            {
                "from": "XY Plane",
                "to": "Circle.Plane",
                "description": "Connect an XY Plane to the plane input of a circle (recommended)"
            },
            {
                "from": "Number",
                "to": "Math.A",
                "description": "Connect a number to the first input of a Math component"
            },
            {
                "from": "Number",
                "to": "Math.B",
                "description": "Connect a number to the second input of a Math component"
            },
            {
                "from": "Number",
                "to": "Construct Point.X",
                "description": "Connect a number to the X input of a Construct Point component"
            },
            {
                "from": "Number",
                "to": "Construct Point.Y",
                "description": "Connect a number to the Y input of a Construct Point component"
            },
            {
                "from": "Number",
                "to": "Construct Point.Z",
                "description": "Connect a number to the Z input of a Construct Point component"
            },
            {
                "from": "Point",
                "to": "Line.Start",
                "description": "Connect a point to the start input of a Line component"
            },
            {
                "from": "Point",
                "to": "Line.End",
                "description": "Connect a point to the end input of a Line component"
            },
            {
                "from": "Circle",
                "to": "Extrude.Base",
                "description": "Connect a circle to the base input of an Extrude component"
            },
            {
                "from": "Number",
                "to": "Extrude.Height",
                "description": "Connect a number to the height input of an Extrude component"
            }
        ],
        "commonIssues": [
            "Using Point component instead of XY Plane for inputs that require planes",
            "Not specifying parameter names when connecting components",
            "Using incorrect component names (e.g., 'addition' instead of 'Math' with Addition operation)",
            "Trying to connect incompatible data types",
            "Not providing all required inputs for a component",
            "Using incorrect parameter names (e.g., 'A' and 'B' for Math component instead of the actual parameter names)",
            "Not checking if a connection was successful before proceeding"
        ],
        "tips": [
            "Always use XY Plane component for plane inputs",
            "Specify parameter names when connecting components",
            "For Circle components, make sure to use the correct inputs (Plane and Radius)",
            "Test simple connections before creating complex geometry",
            "Avoid using components that require selection from Rhino",
            "Use get_component_info to check the actual parameter names of a component",
            "Use get_connections to verify if connections were established correctly",
            "Use search_components to find the correct component name before adding it",
            "Use validate_connection to check if a connection is possible before attempting it"
        ]
    }

@server.resource("grasshopper://component_library")
def get_component_library():
    """Get a comprehensive library of Grasshopper components"""
    # This resource provides a more comprehensive component library, including detailed information for common components
    return {
        "categories": [
            {
                "name": "Params",
                "components": [
                    {
                        "name": "Point",
                        "fullName": "Point Parameter",
                        "description": "Creates a point parameter",
                        "inputs": [
                            {"name": "X", "type": "Number", "description": "X coordinate"},
                            {"name": "Y", "type": "Number", "description": "Y coordinate"},
                            {"name": "Z", "type": "Number", "description": "Z coordinate"}
                        ],
                        "outputs": [
                            {"name": "Pt", "type": "Point", "description": "Point output"}
                        ]
                    },
                    {
                        "name": "Number Slider",
                        "fullName": "Number Slider",
                        "description": "Creates a slider for numeric input with adjustable range and precision",
                        "inputs": [],
                        "outputs": [
                            {"name": "N", "type": "Number", "description": "Number output"}
                        ],
                        "settings": {
                            "min": {"description": "Minimum value of the slider", "default": 0},
                            "max": {"description": "Maximum value of the slider", "default": 10},
                            "value": {"description": "Current value of the slider", "default": 5},
                            "rounding": {"description": "Rounding precision (0.01, 0.1, 1, etc.)", "default": 0.1},
                            "type": {"description": "Slider type (integer, floating point)", "default": "float"},
                            "name": {"description": "Custom name for the slider", "default": ""}
                        },
                        "usage_examples": [
                            "Create a Number Slider with min=0, max=100, value=50",
                            "Create a Number Slider for radius with min=0.1, max=10, value=2.5, rounding=0.1"
                        ],
                        "common_issues": [
                            "Confusing with other slider types",
                            "Not setting appropriate min/max values for the intended use"
                        ],
                        "disambiguation": {
                            "similar_components": [
                                {
                                    "name": "MD Slider",
                                    "description": "Multi-dimensional slider for vector input, NOT for simple numeric values",
                                    "how_to_distinguish": "Use Number Slider for single numeric values; use MD Slider only when you need multi-dimensional control"
                                },
                                {
                                    "name": "Graph Mapper",
                                    "description": "Maps values through a graph function, NOT a simple slider",
                                    "how_to_distinguish": "Use Number Slider for direct numeric input; use Graph Mapper only for function-based mapping"
                                }
                            ],
                            "correct_usage": "When needing a simple numeric input control, ALWAYS use 'Number Slider', not MD Slider or other variants"
                        }
                    },
                    {
                        "name": "Panel",
                        "fullName": "Panel",
                        "description": "Displays text or numeric data",
                        "inputs": [
                            {"name": "Input", "type": "Any", "description": "Any input data"}
                        ],
                        "outputs": []
                    }
                ]
            },
            {
                "name": "Maths",
                "components": [
                    {
                        "name": "Math",
                        "fullName": "Mathematics",
                        "description": "Performs mathematical operations",
                        "inputs": [
                            {"name": "A", "type": "Number", "description": "First number"},
                            {"name": "B", "type": "Number", "description": "Second number"}
                        ],
                        "outputs": [
                            {"name": "Result", "type": "Number", "description": "Result of the operation"}
                        ],
                        "operations": ["Addition", "Subtraction", "Multiplication", "Division", "Power", "Modulo"]
                    }
                ]
            },
            {
                "name": "Vector",
                "components": [
                    {
                        "name": "XY Plane",
                        "fullName": "XY Plane",
                        "description": "Creates an XY plane at the world origin or at a specified point",
                        "inputs": [
                            {"name": "Origin", "type": "Point", "description": "Origin point", "optional": True}
                        ],
                        "outputs": [
                            {"name": "Plane", "type": "Plane", "description": "XY plane"}
                        ]
                    },
                    {
                        "name": "Construct Point",
                        "fullName": "Construct Point",
                        "description": "Constructs a point from X, Y, Z coordinates",
                        "inputs": [
                            {"name": "X", "type": "Number", "description": "X coordinate"},
                            {"name": "Y", "type": "Number", "description": "Y coordinate"},
                            {"name": "Z", "type": "Number", "description": "Z coordinate"}
                        ],
                        "outputs": [
                            {"name": "Pt", "type": "Point", "description": "Constructed point"}
                        ]
                    }
                ]
            },
            {
                "name": "Curve",
                "components": [
                    {
                        "name": "Circle",
                        "fullName": "Circle",
                        "description": "Creates a circle",
                        "inputs": [
                            {"name": "Plane", "type": "Plane", "description": "Base plane for the circle"},
                            {"name": "Radius", "type": "Number", "description": "Circle radius"}
                        ],
                        "outputs": [
                            {"name": "C", "type": "Circle", "description": "Circle output"}
                        ]
                    },
                    {
                        "name": "Line",
                        "fullName": "Line",
                        "description": "Creates a line between two points",
                        "inputs": [
                            {"name": "Start", "type": "Point", "description": "Start point"},
                            {"name": "End", "type": "Point", "description": "End point"}
                        ],
                        "outputs": [
                            {"name": "L", "type": "Line", "description": "Line output"}
                        ]
                    },
                    {
                        "name": "Offset",
                        "fullName": "Offset",
                        "description": "Creates parallel curves at specified distance",
                        "inputs": [
                            {"name": "Curve", "type": "Curve", "description": "Base curve"},
                            {"name": "Distance", "type": "Number", "description": "Offset distance"},
                            {"name": "Side", "type": "Integer", "description": "Side to offset", "optional": True},
                            {"name": "Corners", "type": "Integer", "description": "Corner type", "optional": True}
                        ],
                        "outputs": [
                            {"name": "Curve", "type": "Curve", "description": "Offset curve"}
                        ]
                    },
                    {
                        "name": "Shatter",
                        "fullName": "Shatter",
                        "description": "Splits curves at parameter points",
                        "inputs": [
                            {"name": "Curve", "type": "Curve", "description": "Curve to split"},
                            {"name": "Parameters", "type": "Number", "description": "Parameter values for splitting"}
                        ],
                        "outputs": [
                            {"name": "Curves", "type": "Curve", "description": "Split curve segments"}
                        ]
                    },
                    {
                        "name": "Join Curves",
                        "fullName": "Join Curves",
                        "description": "Connects curves into continuous curves",
                        "inputs": [
                            {"name": "Curves", "type": "Curve", "description": "Curves to join"},
                            {"name": "Tolerance", "type": "Number", "description": "Joining tolerance", "optional": True}
                        ],
                        "outputs": [
                            {"name": "Curve", "type": "Curve", "description": "Joined curve"}
                        ]
                    },
                    {
                        "name": "Polyline",
                        "fullName": "Polyline",
                        "description": "Creates connected line segments",
                        "inputs": [
                            {"name": "Vertices", "type": "Point", "description": "Polyline vertices"}
                        ],
                        "outputs": [
                            {"name": "Polyline", "type": "Curve", "description": "Polyline curve"}
                        ]
                    }
                ]
            },
            {
                "name": "Surface",
                "components": [
                    {
                        "name": "Extrude",
                        "fullName": "Extrude",
                        "description": "Extrudes a curve to create a surface or a solid",
                        "inputs": [
                            {"name": "Base", "type": "Curve", "description": "Base curve to extrude"},
                            {"name": "Direction", "type": "Vector", "description": "Direction of extrusion", "optional": True},
                            {"name": "Height", "type": "Number", "description": "Height of extrusion"}
                        ],
                        "outputs": [
                            {"name": "Brep", "type": "Brep", "description": "Extruded brep"}
                        ]
                    },
                    {
                        "name": "Loft",
                        "fullName": "Loft",
                        "description": "Creates surfaces through curve profiles",
                        "inputs": [
                            {"name": "Profiles", "type": "Curve", "description": "Profile curves"},
                            {"name": "Rails", "type": "Curve", "description": "Rail curves", "optional": True},
                            {"name": "Closed", "type": "Boolean", "description": "Close the loft", "optional": True},
                            {"name": "Type", "type": "Integer", "description": "Loft type", "optional": True}
                        ],
                        "outputs": [
                            {"name": "Surface", "type": "Surface", "description": "Lofted surface"}
                        ]
                    }
                ]
            },
            {
                "name": "Sets",
                "components": [
                    {
                        "name": "List Item",
                        "fullName": "List Item",
                        "description": "Extracts specific items from lists by index",
                        "inputs": [
                            {"name": "List", "type": "Any", "description": "Input list"},
                            {"name": "Index", "type": "Integer", "description": "Index of item to extract"}
                        ],
                        "outputs": [
                            {"name": "Item", "type": "Any", "description": "Extracted item"}
                        ]
                    },
                    {
                        "name": "Cull Pattern",
                        "fullName": "Cull Pattern",
                        "description": "Removes items from lists based on boolean pattern",
                        "inputs": [
                            {"name": "List", "type": "Any", "description": "Input list"},
                            {"name": "Pattern", "type": "Boolean", "description": "Boolean pattern"}
                        ],
                        "outputs": [
                            {"name": "List", "type": "Any", "description": "Filtered list"}
                        ]
                    },
                    {
                        "name": "Graft",
                        "fullName": "Graft",
                        "description": "Converts data tree structure to grafted format",
                        "inputs": [
                            {"name": "Data", "type": "Any", "description": "Input data"}
                        ],
                        "outputs": [
                            {"name": "Data", "type": "Any", "description": "Grafted data"}
                        ]
                    },
                    {
                        "name": "Flatten",
                        "fullName": "Flatten",
                        "description": "Flattens data tree structure to single list",
                        "inputs": [
                            {"name": "Data", "type": "Any", "description": "Input data"}
                        ],
                        "outputs": [
                            {"name": "Data", "type": "Any", "description": "Flattened data"}
                        ]
                    },
                    {
                        "name": "Series",
                        "fullName": "Series",
                        "description": "Creates a series of numbers with start, step, and count",
                        "inputs": [
                            {"name": "Start", "type": "Number", "description": "Starting value"},
                            {"name": "Step", "type": "Number", "description": "Step size"},
                            {"name": "Count", "type": "Integer", "description": "Number of values"}
                        ],
                        "outputs": [
                            {"name": "Series", "type": "Number", "description": "Series of numbers"}
                        ]
                    }
                ]
            },
            {
                "name": "Transform",
                "components": [
                    {
                        "name": "Rotate",
                        "fullName": "Rotate",
                        "description": "Rotates geometry around an axis",
                        "inputs": [
                            {"name": "Geometry", "type": "Geometry", "description": "Geometry to rotate"},
                            {"name": "Angle", "type": "Number", "description": "Rotation angle in radians"},
                            {"name": "Axis", "type": "Line", "description": "Rotation axis"},
                            {"name": "Center", "type": "Point", "description": "Rotation center", "optional": True}
                        ],
                        "outputs": [
                            {"name": "Geometry", "type": "Geometry", "description": "Rotated geometry"},
                            {"name": "Transform", "type": "Transform", "description": "Transformation matrix"}
                        ]
                    }
                ]
            }
        ],
        "dataTypes": [
            {
                "name": "Number",
                "description": "A numeric value",
                "compatibleWith": ["Number", "Integer", "Double"]
            },
            {
                "name": "Point",
                "description": "A 3D point in space",
                "compatibleWith": ["Point3d", "Point"]
            },
            {
                "name": "Vector",
                "description": "A 3D vector",
                "compatibleWith": ["Vector3d", "Vector"]
            },
            {
                "name": "Plane",
                "description": "A plane in 3D space",
                "compatibleWith": ["Plane"]
            },
            {
                "name": "Circle",
                "description": "A circle curve",
                "compatibleWith": ["Circle", "Curve"]
            },
            {
                "name": "Line",
                "description": "A line segment",
                "compatibleWith": ["Line", "Curve"]
            },
            {
                "name": "Curve",
                "description": "A curve object",
                "compatibleWith": ["Curve", "Circle", "Line", "Arc", "Polyline"]
            },
            {
                "name": "Brep",
                "description": "A boundary representation object",
                "compatibleWith": ["Brep", "Surface", "Solid"]
            }
        ]
    }

def main():
    """Main entry point for the Grasshopper MCP Bridge Server"""
    try:
        # Start MCP server
        logger.info("Starting Grasshopper MCP Bridge Server...")
        logger.info("Please add this MCP server to Claude Desktop")
        server.run()
    except Exception as e:
        logger.error(f"Error starting MCP server: {str(e)}")
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
