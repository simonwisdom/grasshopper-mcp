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
using System.Threading.Tasks;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Handler for high-level intent command processing
    /// </summary>
    public class IntentCommandHandler
    {
        /// <summary>
        /// Handle create pattern command
        /// </summary>
        /// <param name="command">Command object</param>
        /// <returns>Command execution result</returns>
        public static object CreatePattern(Command command)
        {
            if (!command.Parameters.TryGetValue("description", out object descriptionObj) || descriptionObj == null)
            {
                throw new ArgumentException("Missing required parameter: description");
            }
            string description = descriptionObj.ToString();

            string patternName = IntentRecognizer.RecognizeIntent(description);
            if (string.IsNullOrEmpty(patternName))
            {
                throw new ArgumentException($"Could not recognize intent from description: {description}");
            }

            RhinoApp.WriteLine($"Recognized intent: {patternName}");

            var (components, connections) = IntentRecognizer.GetPatternDetails(patternName);
            if (components.Count == 0)
            {
                throw new ArgumentException($"Pattern '{patternName}' has no components defined");
            }

            var tcs = new TaskCompletionSource<object>();

            Task.Run(() =>
            {
                try
                {
                    var componentIdMap = new Dictionary<string, string>();

                    foreach (var component in components)
                    {
                        var addCommand = new Command("add_component", new Dictionary<string, object>
                        {
                            { "type", component.Type }, { "x", component.X }, { "y", component.Y }
                        });
                        
                        var result = GrasshopperCommandRegistry.ExecuteCommand(addCommand);
                        if (result.Success && result.Data is IDictionary<string, object> data && data.TryGetValue("id", out var id))
                        {
                            componentIdMap[component.Id] = id.ToString();
                        }
                        else
                        {
                            throw new Exception($"Failed to create component {component.Type}: {result.Error}");
                        }
                    }

                    foreach (var connection in connections)
                    {
                        if (!componentIdMap.TryGetValue(connection.SourceId, out var sourceId) ||
                            !componentIdMap.TryGetValue(connection.TargetId, out var targetId))
                        {
                            throw new Exception($"Could not find component IDs for connection {connection.SourceId} -> {connection.TargetId}");
                        }

                        var connectCommand = new Command("connect_components", new Dictionary<string, object>
                        {
                            { "sourceId", sourceId }, { "sourceParam", connection.SourceParam },
                            { "targetId", targetId }, { "targetParam", connection.TargetParam }
                        });

                        var result = GrasshopperCommandRegistry.ExecuteCommand(connectCommand);
                        if (!result.Success)
                        {
                            throw new Exception($"Failed to connect {connection.SourceId} -> {connection.TargetId}: {result.Error}");
                        }
                    }

                    tcs.SetResult(new
                    {
                        Pattern = patternName,
                        ComponentCount = components.Count,
                        ConnectionCount = connections.Count
                    });
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task.Result;
        }

        /// <summary>
        /// Get available pattern list
        /// </summary>
        /// <param name="command">Command object</param>
        /// <returns>Command execution result</returns>
        public static object GetAvailablePatterns(Command command)
        {
            IntentRecognizer.Initialize();

            var patterns = IntentRecognizer.GetAllPatternNames();

            return Response.Ok(patterns);
        }
    }
}
