using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GrasshopperMCP.Models;
using GrasshopperMCP.Commands;
using GH_MCP.Models;
using GH_MCP.Utils;
using Rhino;
using Newtonsoft.Json;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Handler for high-level intent command processing
    /// </summary>
    public class IntentCommandHandler
    {
        private static Dictionary<string, string> _componentIdMap = new Dictionary<string, string>();

        /// <summary>
        /// Handle create pattern command
        /// </summary>
        /// <param name="command">Command object</param>
        /// <returns>Command execution result</returns>
        public static object CreatePattern(Command command)
        {
            // Get pattern name or description
            if (!command.Parameters.TryGetValue("description", out object descriptionObj) || descriptionObj == null)
            {
                return Response.CreateError("Missing required parameter: description");
            }
            string description = descriptionObj.ToString();

            // Recognize intent
            string patternName = IntentRecognizer.RecognizeIntent(description);
            if (string.IsNullOrEmpty(patternName))
            {
                return Response.CreateError($"Could not recognize intent from description: {description}");
            }

            RhinoApp.WriteLine($"Recognized intent: {patternName}");

            // Get pattern details
            var (components, connections) = IntentRecognizer.GetPatternDetails(patternName);
            if (components.Count == 0)
            {
                return Response.CreateError($"Pattern '{patternName}' has no components defined");
            }

            // Clear component ID mapping
            _componentIdMap.Clear();

            // Create all components
            foreach (var component in components)
            {
                try
                {
                    // Create add component command
                    var addCommand = new Command(
                        "add_component",
                        new Dictionary<string, object>
                        {
                            { "type", component.Type },
                            { "x", component.X },
                            { "y", component.Y }
                        }
                    );

                    // If there are settings, add them
                    if (component.Settings != null)
                    {
                        foreach (var setting in component.Settings)
                        {
                            addCommand.Parameters.Add(setting.Key, setting.Value);
                        }
                    }

                    // Execute add component command
                    var result = ComponentCommandHandler.AddComponent(addCommand);
                    if (result is Response response && response.Success && response.Data != null)
                    {
                        // Save component ID mapping
                        string componentId = response.Data.ToString();
                        _componentIdMap[component.Id] = componentId;
                        RhinoApp.WriteLine($"Created component {component.Type} with ID {componentId}");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Failed to create component {component.Type}");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error creating component {component.Type}: {ex.Message}");
                }

                // Add short delay to ensure component creation is complete
                Thread.Sleep(100);
            }

            // Create all connections
            foreach (var connection in connections)
            {
                try
                {
                    // Check if source and target component IDs exist
                    if (!_componentIdMap.TryGetValue(connection.SourceId, out string sourceId) ||
                        !_componentIdMap.TryGetValue(connection.TargetId, out string targetId))
                    {
                        RhinoApp.WriteLine($"Could not find component IDs for connection {connection.SourceId} -> {connection.TargetId}");
                        continue;
                    }

                    // Create connect command
                    var connectCommand = new Command(
                        "connect_components",
                        new Dictionary<string, object>
                        {
                            { "sourceId", sourceId },
                            { "sourceParam", connection.SourceParam },
                            { "targetId", targetId },
                            { "targetParam", connection.TargetParam }
                        }
                    );

                    // Execute connect command
                    var result = ConnectionCommandHandler.ConnectComponents(connectCommand);
                    if (result is Response response && response.Success)
                    {
                        RhinoApp.WriteLine($"Connected {connection.SourceId}.{connection.SourceParam} -> {connection.TargetId}.{connection.TargetParam}");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Failed to connect {connection.SourceId}.{connection.SourceParam} -> {connection.TargetId}.{connection.TargetParam}");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error creating connection: {ex.Message}");
                }

                // Add short delay to ensure connection creation is complete
                Thread.Sleep(100);
            }

            // Return success result
            return Response.Ok(new
            {
                Pattern = patternName,
                ComponentCount = components.Count,
                ConnectionCount = connections.Count
            });
        }

        /// <summary>
        /// Get available pattern list
        /// </summary>
        /// <param name="command">Command object</param>
        /// <returns>Command execution result</returns>
        public static object GetAvailablePatterns(Command command)
        {
            // Initialize intent recognizer
            IntentRecognizer.Initialize();

            // Get all available patterns
            var patterns = new List<string>();
            if (command.Parameters.TryGetValue("query", out object queryObj) && queryObj != null)
            {
                string query = queryObj.ToString();
                string patternName = IntentRecognizer.RecognizeIntent(query);
                if (!string.IsNullOrEmpty(patternName))
                {
                    patterns.Add(patternName);
                }
            }
            else
            {
                // If no query, return all patterns
                // Need to extend IntentRecognizer here to support getting all patterns
                // Temporarily return empty list
            }

            // Return success result
            return Response.Ok(patterns);
        }
    }
}
