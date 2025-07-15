using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GrasshopperMCP.Models;
using GH_MCP.Models;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino;
using Newtonsoft.Json;
using GH_MCP.Utils;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Handler for component connection-related commands
    /// </summary>
    public class ConnectionCommandHandler
    {
        private static Connection ParseConnection(Command command, string idKey, string paramKey, string indexKey)
        {
            if (!command.Parameters.TryGetValue(idKey, out object idObj) || idObj == null)
            {
                throw new ArgumentException($"Missing required parameter: {idKey}");
            }

            string paramName = null;
            int? paramIndex = null;

            if (command.Parameters.TryGetValue(paramKey, out object paramObj) && paramObj != null)
            {
                paramName = paramObj.ToString();
                try
                {
                    paramName = FuzzyMatcher.GetClosestParameterName(paramName);
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"GH_MCP: FuzzyMatcher failed for parameter '{paramName}', using original: {ex.Message}");
                    // Keep original parameter name
                }
            }
            else if (command.Parameters.TryGetValue(indexKey, out object indexObj) && indexObj != null)
            {
                if (int.TryParse(indexObj.ToString(), out int index))
                {
                    paramIndex = index;
                }
            }

            return new Connection
            {
                ComponentId = idObj.ToString(),
                ParameterName = paramName,
                ParameterIndex = paramIndex
            };
        }

        /// <summary>
        /// Connect two components
        /// </summary>
        /// <param name="command">Command object</param>
        /// <returns>Command execution result</returns>
        public static object ConnectComponents(Command command)
        {
            var sourceConnection = ParseConnection(command, "sourceId", "sourceParam", "sourceParamIndex");
            var targetConnection = ParseConnection(command, "targetId", "targetParam", "targetParamIndex");

            // Log connection information
            RhinoApp.WriteLine($"Connecting: sourceId={sourceConnection.ComponentId}, sourceParam={sourceConnection.ParameterName}, targetId={targetConnection.ComponentId}, targetParam={targetConnection.ParameterName}");

            var connection = new ConnectionPairing
            {
                Source = sourceConnection,
                Target = targetConnection
            };

            if (!connection.IsValid())
            {
                throw new ArgumentException("Invalid connection parameters");
            }

            var tcs = new TaskCompletionSource<object>();

            RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    var doc = Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        tcs.SetException(new InvalidOperationException("No active Grasshopper document"));
                        return;
                    }

                    var sourceComponent = doc.FindObject(new Guid(connection.Source.ComponentId), true);
                    if (sourceComponent == null)
                    {
                        tcs.SetException(new ArgumentException($"Source component not found: {connection.Source.ComponentId}"));
                        return;
                    }

                    var targetComponent = doc.FindObject(new Guid(connection.Target.ComponentId), true);
                    if (targetComponent == null)
                    {
                        tcs.SetException(new ArgumentException($"Target component not found: {connection.Target.ComponentId}"));
                        return;
                    }

                    IGH_Param sourceParameter = GetParameter(sourceComponent, connection.Source, false);
                    if (sourceParameter == null)
                    {
                        tcs.SetException(new ArgumentException($"Source parameter not found: {connection.Source.ParameterName ?? connection.Source.ParameterIndex.ToString()}"));
                        return;
                    }

                    IGH_Param targetParameter = GetParameter(targetComponent, connection.Target, true);
                    if (targetParameter == null)
                    {
                        tcs.SetException(new ArgumentException($"Target parameter not found: {connection.Target.ParameterName ?? connection.Target.ParameterIndex.ToString()}"));
                        return;
                    }

                    if (!AreParametersCompatible(sourceParameter, targetParameter))
                    {
                        tcs.SetException(new ArgumentException($"Parameters are not compatible: {sourceParameter.GetType().Name} cannot connect to {targetParameter.GetType().Name}"));
                        return;
                    }

                    if (targetParameter.SourceCount > 0)
                    {
                        targetParameter.RemoveAllSources();
                    }

                    targetParameter.AddSource(sourceParameter);
                    doc.NewSolution(false);

                    tcs.SetResult(new
                    {
                        success = true,
                        message = "Connection created successfully",
                        sourceId = connection.Source.ComponentId,
                        targetId = connection.Target.ComponentId,
                        sourceParam = sourceParameter.Name,
                        targetParam = targetParameter.Name,
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
        /// Get component parameter
        /// </summary>
        /// <param name="docObj">Document object</param>
        /// <param name="connection">Connection information</param>
        /// <param name="isInput">Whether it's an input parameter</param>
        /// <returns>Parameter object</returns>
        private static IGH_Param GetParameter(IGH_DocumentObject docObj, Connection connection, bool isInput)
        {
            // Handle parameter components
            if (docObj is IGH_Param param)
            {
                return param;
            }
            
            // Handle general components
            if (docObj is IGH_Component component)
            {
                // Get parameter collection
                IList<IGH_Param> parameters = isInput ? component.Params.Input : component.Params.Output;
                
                // Check if parameter collection is empty
                if (parameters == null || parameters.Count == 0)
                {
                    return null;
                }
                
                // If there's only one parameter, return it directly (only when name or index is not specified)
                if (parameters.Count == 1 && string.IsNullOrEmpty(connection.ParameterName) && !connection.ParameterIndex.HasValue)
                {
                    return parameters[0];
                }
                
                // Find parameter by name
                if (!string.IsNullOrEmpty(connection.ParameterName))
                {
                    // Exact match
                    foreach (var p in parameters)
                    {
                        if (string.Equals(p.Name, connection.ParameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            return p;
                        }
                    }
                    
                    // Fuzzy match
                    foreach (var p in parameters)
                    {
                        if (p.Name.IndexOf(connection.ParameterName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return p;
                        }
                    }

                    // Try to match NickName
                    foreach (var p in parameters)
                    {
                        if (string.Equals(p.NickName, connection.ParameterName, StringComparison.OrdinalIgnoreCase))
                        {
                            return p;
                        }
                    }
                }
                
                // Find parameter by index
                if (connection.ParameterIndex.HasValue)
                {
                    int index = connection.ParameterIndex.Value;
                    if (index >= 0 && index < parameters.Count)
                    {
                        return parameters[index];
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Check if two parameters are compatible
        /// </summary>
        /// <param name="source">Source parameter</param>
        /// <param name="target">Target parameter</param>
        /// <returns>Whether they are compatible</returns>
        private static bool AreParametersCompatible(IGH_Param source, IGH_Param target)
        {
            // If parameter types match exactly, they are compatible
            if (source.GetType() == target.GetType())
            {
                return true;
            }

            // Check if data types are compatible
            var sourceType = source.Type;
            var targetType = target.Type;
            
            // Log parameter type information for debugging
            RhinoApp.WriteLine($"Parameter types: source={sourceType.Name}, target={targetType.Name}");
            RhinoApp.WriteLine($"Parameter names: source={source.Name}, target={target.Name}");
            
            // Check numeric type compatibility
            bool isSourceNumeric = IsNumericType(source);
            bool isTargetNumeric = IsNumericType(target);
            
            if (isSourceNumeric && isTargetNumeric)
            {
                return true;
            }

            // Special handling between curves and geometry
            bool isSourceCurve = source is Param_Curve;
            bool isTargetCurve = target is Param_Curve;
            bool isSourceGeometry = source is Param_Geometry;
            bool isTargetGeometry = target is Param_Geometry;

            if ((isSourceCurve && isTargetGeometry) || (isSourceGeometry && isTargetCurve))
            {
                return true;
            }

            // Special handling between points and vectors
            bool isSourcePoint = source is Param_Point;
            bool isTargetPoint = target is Param_Point;
            bool isSourceVector = source is Param_Vector;
            bool isTargetVector = target is Param_Vector;

            if ((isSourcePoint && isTargetVector) || (isSourceVector && isTargetPoint))
            {
                return true;
            }

            // Check component GUID to ensure connection to correct component type
            // Get the component that owns the parameter
            var sourceDoc = source.OnPingDocument();
            var targetDoc = target.OnPingDocument();
            
            if (sourceDoc != null && targetDoc != null)
            {
                // Try to find the component that owns the parameter
                IGH_Component sourceComponent = FindComponentForParam(sourceDoc, source);
                IGH_Component targetComponent = FindComponentForParam(targetDoc, target);
                
                // If source and target components are found
                if (sourceComponent != null && targetComponent != null)
                {
                    // Log component information for debugging
                    RhinoApp.WriteLine($"Components: source={sourceComponent.Name}, target={targetComponent.Name}");
                    RhinoApp.WriteLine($"Component GUIDs: source={sourceComponent.ComponentGuid}, target={targetComponent.ComponentGuid}");
                    
                    // Special handling for plane to geometry component connections
                    if (IsPlaneComponent(sourceComponent) && RequiresPlaneInput(targetComponent))
                    {
                        RhinoApp.WriteLine("Connecting plane component to geometry component that requires plane input");
                        return true;
                    }
                    
                    // If source is slider and target is circle, ensure target is circle creation component
                    if (sourceComponent.Name.Contains("Number") && targetComponent.Name.Contains("Circle"))
                    {
                        // Check if target is the correct circle component (using GUID or description)
                        if (targetComponent.ComponentGuid.ToString() == "d1028c72-ff86-4057-9eb0-36c687a4d98c")
                        {
                            // This is the wrong circle component (parameter container)
                            RhinoApp.WriteLine("Detected connection to Circle parameter container instead of Circle component");
                            return false;
                        }
                        if (targetComponent.ComponentGuid.ToString() == "807b86e3-be8d-4970-92b5-f8cdcb45b06b")
                        {
                            // This is the correct circle component (create circle)
                            return true;
                        }
                    }
                    
                    // If source is plane and target is box, allow connection
                    if (IsPlaneComponent(sourceComponent) && targetComponent.Name.Contains("Box"))
                    {
                        RhinoApp.WriteLine("Connecting plane component to box component");
                        return true;
                    }
                }
            }

            // Default allow connection, let Grasshopper decide compatibility at runtime
            return true;
        }

        /// <summary>
        /// Check if parameter is numeric type
        /// </summary>
        /// <param name="param">Parameter</param>
        /// <returns>Whether it's numeric type</returns>
        private static bool IsNumericType(IGH_Param param)
        {
            return param is Param_Integer || 
                   param is Param_Number || 
                   param is Param_Time;
        }

        /// <summary>
        /// Find the component that owns the parameter
        /// </summary>
        /// <param name="doc">Document</param>
        /// <param name="param">Parameter</param>
        /// <returns>Component that owns the parameter</returns>
        private static IGH_Component FindComponentForParam(GH_Document doc, IGH_Param param)
        {
            foreach (var obj in doc.Objects)
            {
                if (obj is IGH_Component comp)
                {
                    // Check output parameters
                    foreach (var outParam in comp.Params.Output)
                    {
                        if (outParam.InstanceGuid == param.InstanceGuid)
                        {
                            return comp;
                        }
                    }
                    
                    // Check input parameters
                    foreach (var inParam in comp.Params.Input)
                    {
                        if (inParam.InstanceGuid == param.InstanceGuid)
                        {
                            return comp;
                        }
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Check if component is a plane component
        /// </summary>
        /// <param name="component">Component</param>
        /// <returns>Whether it's a plane component</returns>
        private static bool IsPlaneComponent(IGH_Component component)
        {
            if (component == null)
                return false;
                
            // Check component name
            string name = component.Name.ToLowerInvariant();
            if (name.Contains("plane"))
                return true;
                
            // Check XY Plane component GUID
            if (component.ComponentGuid.ToString() == "896a1e5e-c2ac-4996-a6d8-5b61157080b3")
                return true;
                
            return false;
        }
        
        /// <summary>
        /// Check if component requires plane input
        /// </summary>
        /// <param name="component">Component</param>
        /// <returns>Whether it requires plane input</returns>
        private static bool RequiresPlaneInput(IGH_Component component)
        {
            if (component == null)
                return false;
                
            // Check if component has input parameters named "Plane" or "Base"
            foreach (var param in component.Params.Input)
            {
                string paramName = param.Name.ToLowerInvariant();
                if (paramName.Contains("plane") || paramName.Contains("base"))
                    return true;
            }
            
            // Check specific component types
            string name = component.Name.ToLowerInvariant();
            return name.Contains("box") || 
                   name.Contains("rectangle") || 
                   name.Contains("circle") || 
                   name.Contains("cylinder") || 
                   name.Contains("cone");
        }
    }

    /// <summary>
    /// Represents a pairing of source and target connections
    /// </summary>
    public class ConnectionPairing
    {
        /// <summary>
        /// Gets or sets the source connection
        /// </summary>
        public Connection Source { get; set; }
        
        /// <summary>
        /// Gets or sets the target connection
        /// </summary>
        public Connection Target { get; set; }

        /// <summary>
        /// Checks if the connection pairing is valid
        /// </summary>
        /// <returns>True if both source and target are not null</returns>
        public bool IsValid()
        {
            return Source != null && Target != null;
        }
    }

    /// <summary>
    /// Represents a connection to a component parameter
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Gets or sets the component identifier
        /// </summary>
        public string ComponentId { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter name
        /// </summary>
        public string ParameterName { get; set; }
        
        /// <summary>
        /// Gets or sets the parameter index (optional)
        /// </summary>
        public int? ParameterIndex { get; set; }
    }
}
