using System;
using System.Collections.Generic;
using GH_MCP.Commands;
using GrasshopperMCP.Models;
using GrasshopperMCP.Commands;
using Grasshopper.Kernel;
using Rhino;
using System.Linq;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Grasshopper command registry for registering and executing commands
    /// </summary>
    public static class GrasshopperCommandRegistry
    {
        // Command handler dictionary, key is command type, value is function to handle command
        private static readonly Dictionary<string, Func<Command, object>> CommandHandlers = new Dictionary<string, Func<Command, object>>();

        /// <summary>
        /// Initialize command registry
        /// </summary>
        public static void Initialize()
        {
            // Register geometry commands
            RegisterGeometryCommands();
            
            // Register component commands
            RegisterComponentCommands();
            
            // Register document commands
            RegisterDocumentCommands();
            
            // Register intent commands
            RegisterIntentCommands();
            
            // Register utility commands
            RegisterUtilityCommands();
            
            RhinoApp.WriteLine("GH_MCP: Command registry initialized.");
        }

        /// <summary>
        /// Register geometry commands
        /// </summary>
        private static void RegisterGeometryCommands()
        {
            // Create point
            RegisterCommand("create_point", GeometryCommandHandler.CreatePoint);
            
            // Create curve
            RegisterCommand("create_curve", GeometryCommandHandler.CreateCurve);
            
            // Create circle
            RegisterCommand("create_circle", GeometryCommandHandler.CreateCircle);
        }

        /// <summary>
        /// Register component commands
        /// </summary>
        private static void RegisterComponentCommands()
        {
            // Add component
            RegisterCommand("add_component", ComponentCommandHandler.AddComponent);
            
            // Connect components
            RegisterCommand("connect_components", ConnectionCommandHandler.ConnectComponents);
            
            // Set component value
            RegisterCommand("set_component_value", ComponentCommandHandler.SetComponentValue);
            
            // Get component information
            RegisterCommand("get_component_info", ComponentCommandHandler.GetComponentInfo);
            
            // Get component warnings
            RegisterCommand("get_component_warnings", ComponentCommandHandler.GetComponentWarnings);
            
            // Get all components
            RegisterCommand("get_all_components", ComponentCommandHandler.GetAllComponents);
            
            // Get all connections
            RegisterCommand("get_connections", ComponentCommandHandler.GetConnections);
            
            // Search components
            RegisterCommand("search_components", ComponentCommandHandler.SearchComponents);
            
            // Get component parameters
            RegisterCommand("get_component_parameters", ComponentCommandHandler.GetComponentParameters);
        }

        /// <summary>
        /// Register document commands
        /// </summary>
        private static void RegisterDocumentCommands()
        {
            // Get document information
            RegisterCommand("get_document_info", DocumentCommandHandler.GetDocumentInfo);
            
            // Clear document
            RegisterCommand("clear_document", DocumentCommandHandler.ClearDocument);
            
            // Save document
            RegisterCommand("save_document", DocumentCommandHandler.SaveDocument);
            
            // Load document
            RegisterCommand("load_document", DocumentCommandHandler.LoadDocument);
        }

        /// <summary>
        /// Register intent commands
        /// </summary>
        private static void RegisterIntentCommands()
        {
            // Create pattern
            RegisterCommand("create_pattern", IntentCommandHandler.CreatePattern);
            
            // Get available patterns
            RegisterCommand("get_available_patterns", IntentCommandHandler.GetAvailablePatterns);
            
            RhinoApp.WriteLine("GH_MCP: Intent commands registered.");
        }

        /// <summary>
        /// Register basic utility commands
        /// </summary>
        private static void RegisterUtilityCommands()
        {
            // Ping command for connection testing
            RegisterCommand("ping", (Command command) => 
            {
                return new { message = "pong", timestamp = DateTime.UtcNow };
            });
            
            RhinoApp.WriteLine("GH_MCP: Utility commands registered.");
        }

        /// <summary>
        /// Register command handler
        /// </summary>
        /// <param name="commandType">Command type</param>
        /// <param name="handler">Handler function</param>
        public static void RegisterCommand(string commandType, Func<Command, object> handler)
        {
            if (string.IsNullOrEmpty(commandType))
                throw new ArgumentNullException(nameof(commandType));
                
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            CommandHandlers[commandType] = handler;
            RhinoApp.WriteLine($"GH_MCP: Registered command handler for '{commandType}'");
        }

        /// <summary>
        /// Execute command
        /// </summary>
        /// <param name="command">Command to execute</param>
        /// <returns>Command execution result</returns>
        public static Response ExecuteCommand(Command command)
        {
            if (command == null)
            {
                return Response.CreateError("Command is null");
            }
            
            if (string.IsNullOrEmpty(command.Type))
            {
                return Response.CreateError("Command type is null or empty");
            }
            
            if (CommandHandlers.TryGetValue(command.Type, out var handler))
            {
                try
                {
                    var result = handler(command);
                    return Response.Ok(result);
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"GH_MCP: Error executing command '{command.Type}': {ex.Message}");
                    RhinoApp.WriteLine($"GH_MCP: Exception type: {ex.GetType().Name}");
                    RhinoApp.WriteLine($"GH_MCP: Stack trace: {ex.StackTrace}");
                    
                    // Log inner exception if present
                    if (ex.InnerException != null)
                    {
                        RhinoApp.WriteLine($"GH_MCP: Inner exception: {ex.InnerException.Message}");
                        RhinoApp.WriteLine($"GH_MCP: Inner exception type: {ex.InnerException.GetType().Name}");
                    }
                    
                    return Response.CreateError($"Error executing command '{command.Type}': {ex.Message}");
                }
            }
            
            RhinoApp.WriteLine($"GH_MCP: No handler registered for command type '{command.Type}'");
            RhinoApp.WriteLine($"GH_MCP: Available commands: {string.Join(", ", CommandHandlers.Keys)}");
            return Response.CreateError($"No handler registered for command type '{command.Type}'");
        }

        /// <summary>
        /// Get all registered command types
        /// </summary>
        /// <returns>List of command types</returns>
        public static List<string> GetRegisteredCommandTypes()
        {
            return CommandHandlers.Keys.ToList();
        }
    }
}
