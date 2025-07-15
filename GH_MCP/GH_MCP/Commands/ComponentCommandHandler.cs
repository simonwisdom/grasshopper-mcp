using System;
using System.Collections.Generic;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using System.Linq;
using Grasshopper.Kernel.Components;
using System.Threading;
using GH_MCP.Utils;

namespace GrasshopperMCP.Commands
{
    /// <summary>
    /// Handler for component-related commands
    /// </summary>
    public static class ComponentCommandHandler
    {
        private static bool _loggedComponentTypes = false;

        /// <summary>
        /// Add component
        /// </summary>
        /// <param name="command">Command containing component type and position</param>
        /// <returns>Information about the added component</returns>
        public static object AddComponent(Command command)
        {
            string type = command.GetParameter<string>("type");
            double x = command.GetParameter<double>("x");
            double y = command.GetParameter<double>("y");
            
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("Component type is required");
            }
            
            // Use fuzzy matching to get standardized component name
            string normalizedType = type; // Temporarily bypass FuzzyMatcher
            try
            {
                normalizedType = FuzzyMatcher.GetClosestComponentName(type);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"GH_MCP: FuzzyMatcher failed, using original type '{type}': {ex.Message}");
                normalizedType = type;
            }
            
            // Log request information
            RhinoApp.WriteLine($"AddComponent request: type={type}, normalized={normalizedType}, x={x}, y={y}");
            
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }
                    
                    // Create component
                    IGH_DocumentObject component = null;
                    
                    // Log available component types (only on first call)
                    if (!_loggedComponentTypes)
                    {
                        var availableTypes = Grasshopper.Instances.ComponentServer.ObjectProxies
                            .Select(p => p.Desc.Name)
                            .OrderBy(n => n)
                            .ToList();
                        
                        RhinoApp.WriteLine($"Available component types: {string.Join(", ", availableTypes.Take(50))}...");
                        _loggedComponentTypes = true;
                    }
                    
                    // Create component dynamically
                    component = CreateComponentByName(normalizedType);
                    if (component == null)
                    {
                        // Fallback for special cases or components not found by name
                        switch (normalizedType.ToLowerInvariant())
                        {
                            case "point":
                            case "pt":
                            case "pointparam":
                            case "param_point":
                                component = new Param_Point();
                                break;
                            case "circleparam":
                            case "param_circle":
                                component = new Param_Circle();
                                break;
                            case "lineparam":
                            case "param_line":
                                component = new Param_Line();
                                break;
                            case "panel":
                            case "gh_panel":
                                component = new GH_Panel();
                                break;
                            case "slider":
                            case "numberslider":
                            case "gh_numberslider":
                                var slider = new GH_NumberSlider();
                                slider.SetInitCode("0.0 < 0.5 < 1.0");
                                component = slider;
                                break;
                            case "number":
                            case "num":
                            case "integer":
                            case "int":
                            case "param_number":
                            case "param_integer":
                                component = new Param_Number();
                                break;
                            case "curve":
                            case "crv":
                            case "curveparam":
                            case "param_curve":
                                component = new Param_Curve();
                                break;
                            default:
                                throw new ArgumentException($"Component type '{normalizedType}' not found or could not be created.");
                        }
                    }

                    if (component == null)
                    {
                        throw new InvalidOperationException($"Failed to create component of type '{normalizedType}'");
                    }
                    
                    // Set component position
                    if (component != null)
                    {
                        // Ensure component has valid attributes object
                        if (component.Attributes == null)
                        {
                            RhinoApp.WriteLine("Component attributes are null, creating new attributes");
                            component.CreateAttributes();
                        }
                        
                        // Set position
                        component.Attributes.Pivot = new System.Drawing.PointF((float)x, (float)y);
                        
                        // Add to document
                        doc.AddObject(component, false);
                        
                        // Refresh canvas
                        doc.NewSolution(false);
                        
                        // Return component information
                        result = new
                        {
                            id = component.InstanceGuid.ToString(),
                            type = component.GetType().Name,
                            name = component.NickName,
                            x = component.Attributes.Pivot.X,
                            y = component.Attributes.Pivot.Y
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to create component");
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in AddComponent: {ex.Message}");
                }
            }));
            
            // Wait for UI thread operation to complete
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            // If there's an exception, throw it
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// Connect components
        /// </summary>
        /// <param name="command">Command containing source and target component information</param>
        /// <returns>Connection information</returns>
        public static object ConnectComponents(Command command)
        {
            var fromData = command.GetParameter<Dictionary<string, object>>("from");
            var toData = command.GetParameter<Dictionary<string, object>>("to");
            
            if (fromData == null || toData == null)
            {
                throw new ArgumentException("Source and target component information are required");
            }
            
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }
                    
                    // Parse source component information
                    string fromIdStr = fromData["id"].ToString();
                    string fromParamName = fromData["parameterName"].ToString();
                    
                    // Parse target component information
                    string toIdStr = toData["id"].ToString();
                    string toParamName = toData["parameterName"].ToString();
                    
                    // Convert string IDs to GUIDs
                    Guid fromId, toId;
                    if (!Guid.TryParse(fromIdStr, out fromId) || !Guid.TryParse(toIdStr, out toId))
                    {
                        throw new ArgumentException("Invalid component ID format");
                    }
                    
                    // Find source and target components
                    IGH_Component fromComponent = doc.FindComponent(fromId) as IGH_Component;
                    IGH_Component toComponent = doc.FindComponent(toId) as IGH_Component;
                    
                    if (fromComponent == null || toComponent == null)
                    {
                        throw new ArgumentException("Source or target component not found");
                    }
                    
                    // Find source output parameter
                    IGH_Param fromParam = null;
                    foreach (var param in fromComponent.Params.Output)
                    {
                        if (param.Name.Equals(fromParamName, StringComparison.OrdinalIgnoreCase))
                        {
                            fromParam = param;
                            break;
                        }
                    }
                    
                    // Find target input parameter
                    IGH_Param toParam = null;
                    foreach (var param in toComponent.Params.Input)
                    {
                        if (param.Name.Equals(toParamName, StringComparison.OrdinalIgnoreCase))
                        {
                            toParam = param;
                            break;
                        }
                    }
                    
                    if (fromParam == null || toParam == null)
                    {
                        throw new ArgumentException("Source or target parameter not found");
                    }
                    
                    // Connect parameters
                    toParam.AddSource(fromParam);
                    
                    // Refresh canvas
                    doc.NewSolution(false);
                    
                    // Return connection information
                    result = new
                    {
                        from = new
                        {
                            id = fromComponent.InstanceGuid.ToString(),
                            name = fromComponent.NickName,
                            parameter = fromParam.Name
                        },
                        to = new
                        {
                            id = toComponent.InstanceGuid.ToString(),
                            name = toComponent.NickName,
                            parameter = toParam.Name
                        }
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in ConnectComponents: {ex.Message}");
                }
            }));
            
            // Wait for UI thread operation to complete
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            // If there's an exception, throw it
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// Set component value
        /// </summary>
        /// <param name="command">Command containing component ID and value</param>
        /// <returns>Operation result</returns>
        public static object SetComponentValue(Command command)
        {
            string idStr = command.GetParameter<string>("id");
            string value = command.GetParameter<string>("value");
            
            if (string.IsNullOrEmpty(idStr))
            {
                throw new ArgumentException("Component ID is required");
            }
            
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }
                    
                    // Convert string ID to GUID
                    Guid id;
                    if (!Guid.TryParse(idStr, out id))
                    {
                        throw new ArgumentException("Invalid component ID format");
                    }
                    
                    // Find component
                    IGH_DocumentObject component = doc.FindObject(id, true);
                    if (component == null)
                    {
                        throw new ArgumentException($"Component with ID {idStr} not found");
                    }
                    
                    // Set value based on component type
                    if (component is GH_Panel panel)
                    {
                        panel.UserText = value;
                    }
                    else if (component is GH_NumberSlider slider)
                    {
                        if (double.TryParse(value, out double number))
                        {
                            // Ensure the value is within the slider's range
                            if (number < (double)slider.Slider.Minimum)
                                number = (double)slider.Slider.Minimum;
                            if (number > (double)slider.Slider.Maximum)
                                number = (double)slider.Slider.Maximum;
                            
                            slider.SetSliderValue((decimal)number);
                        }
                        else
                        {
                            throw new ArgumentException("Invalid slider value format");
                        }
                    }
                    else if (component is IGH_Component ghComponent)
                    {
                        // Try to set the first input parameter value
                        if (ghComponent.Params.Input.Count > 0)
                        {
                            var param = ghComponent.Params.Input[0];
                            if (param is Param_String stringParam)
                            {
                                stringParam.PersistentData.Clear();
                                stringParam.PersistentData.Append(new Grasshopper.Kernel.Types.GH_String(value));
                            }
                            else if (param is Param_Number numberParam)
                            {
                                double doubleValue;
                                if (double.TryParse(value, out doubleValue))
                                {
                                    numberParam.PersistentData.Clear();
                                    numberParam.PersistentData.Append(new Grasshopper.Kernel.Types.GH_Number(doubleValue));
                                }
                                else
                                {
                                    throw new ArgumentException("Invalid number value format");
                                }
                            }
                            else
                            {
                                throw new ArgumentException($"Cannot set value for parameter type {param.GetType().Name}");
                            }
                        }
                        else
                        {
                            throw new ArgumentException("Component has no input parameters");
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Cannot set value for component type {component.GetType().Name}");
                    }
                    
                    // Refresh canvas
                    doc.NewSolution(false);
                    
                    // Return operation result
                    result = new
                    {
                        id = component.InstanceGuid.ToString(),
                        type = component.GetType().Name,
                        value = value
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in SetComponentValue: {ex.Message}");
                }
            }));
            
            // Wait for UI thread operation to complete
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            // If there's an exception, throw it
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get component information
        /// </summary>
        /// <param name="command">Command containing component ID</param>
        /// <returns>Component information</returns>
        public static object GetComponentInfo(Command command)
        {
            // Try to get component ID from either "id" or "component_id" parameter
            string idStr = null;
            if (command.Parameters.ContainsKey("id"))
            {
                idStr = command.GetParameter<string>("id");
            }
            else if (command.Parameters.ContainsKey("component_id"))
            {
                idStr = command.GetParameter<string>("component_id");
            }
            
            if (string.IsNullOrEmpty(idStr))
            {
                throw new ArgumentException("Component ID is required (use 'id' or 'component_id' parameter)");
            }
            
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document found.");
                    }
                    
                    // Convert string ID to GUID
                    Guid id;
                    if (!Guid.TryParse(idStr, out id))
                    {
                        throw new ArgumentException("Invalid component ID format");
                    }
                    
                    // Find component
                    IGH_DocumentObject component = doc.FindObject(id, true);
                    if (component == null)
                    {
                        throw new ArgumentException($"Component with ID {idStr} not found");
                    }
                    
                    // Collect component information
                    var componentInfo = new Dictionary<string, object>
                    {
                        { "id", component.InstanceGuid.ToString() },
                        { "type", component.GetType().Name },
                        { "name", component.NickName },
                        { "description", component.Description }
                    };
                    
                    // If it's an IGH_Component, collect input and output parameter information
                    if (component is IGH_Component ghComponent)
                    {
                        var inputs = new List<Dictionary<string, object>>();
                        foreach (var param in ghComponent.Params.Input)
                        {
                            inputs.Add(new Dictionary<string, object>
                            {
                                { "name", param.Name },
                                { "nickname", param.NickName },
                                { "description", param.Description },
                                { "type", param.GetType().Name },
                                { "dataType", param.TypeName }
                            });
                        }
                        componentInfo["inputs"] = inputs;
                        
                        var outputs = new List<Dictionary<string, object>>();
                        foreach (var param in ghComponent.Params.Output)
                        {
                            outputs.Add(new Dictionary<string, object>
                            {
                                { "name", param.Name },
                                { "nickname", param.NickName },
                                { "description", param.Description },
                                { "type", param.GetType().Name },
                                { "dataType", param.TypeName }
                            });
                        }
                        componentInfo["outputs"] = outputs;
                    }
                    
                    // If it's a GH_Panel, get its text value
                    if (component is GH_Panel panel)
                    {
                        componentInfo["value"] = panel.UserText;
                    }
                    
                    // If it's a GH_NumberSlider, get its value and range
                    if (component is GH_NumberSlider slider)
                    {
                        componentInfo["value"] = (double)slider.CurrentValue;
                        componentInfo["minimum"] = (double)slider.Slider.Minimum;
                        componentInfo["maximum"] = (double)slider.Slider.Maximum;
                    }
                    
                    result = componentInfo;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetComponentInfo: {ex.Message}");
                }
            }));
            
            // Wait for UI thread operation to complete
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            // If there's an exception, throw it
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get component warnings and errors
        /// </summary>
        /// <param name="command">Command containing component ID (optional, if null gets all components)</param>
        /// <returns>Component warnings and errors information</returns>
        public static object GetComponentWarnings(Command command)
        {
            string idStr = null;
            if (command.Parameters.ContainsKey("id"))
            {
                idStr = command.GetParameter<string>("id");
            }
            
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document found.");
                    }
                    
                    var warnings = new List<Dictionary<string, object>>();
                    
                    // If specific component ID provided, check only that component
                    if (!string.IsNullOrEmpty(idStr))
                    {
                        Guid id;
                        if (!Guid.TryParse(idStr, out id))
                        {
                            throw new ArgumentException("Invalid component ID format");
                        }
                        
                        IGH_DocumentObject component = doc.FindObject(id, true);
                        if (component == null)
                        {
                            throw new ArgumentException($"Component with ID {idStr} not found");
                        }
                        
                        var componentWarnings = GetComponentMessages(component);
                        if (componentWarnings.Any())
                        {
                            warnings.AddRange(componentWarnings);
                        }
                    }
                    else
                    {
                        // Check all components for warnings
                        foreach (var obj in doc.Objects)
                        {
                            var componentWarnings = GetComponentMessages(obj);
                            if (componentWarnings.Any())
                            {
                                warnings.AddRange(componentWarnings);
                            }
                        }
                    }
                    
                    result = new
                    {
                        totalWarnings = warnings.Count,
                        warnings = warnings,
                        summary = warnings.Count == 0 ? "No warnings found" : $"Found {warnings.Count} warning(s)"
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetComponentWarnings: {ex.Message}");
                }
            }));
            
            // Wait for UI thread operation to complete
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            // If there's an exception, throw it
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get messages (warnings, errors, remarks) for a specific component
        /// </summary>
        /// <param name="component">Component to check</param>
        /// <returns>List of component messages</returns>
        private static List<Dictionary<string, object>> GetComponentMessages(IGH_DocumentObject component)
        {
            var messages = new List<Dictionary<string, object>>();
            
            try
            {
                // Get component information
                var componentInfo = new Dictionary<string, object>
                {
                    { "id", component.InstanceGuid.ToString() },
                    { "type", component.GetType().Name },
                    { "name", component.NickName },
                    { "description", component.Description }
                };
                
                // Check if component has messages
                if (component is IGH_Component ghComponent)
                {
                    // Get component messages
                    var componentMessages = ghComponent.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                    if (componentMessages != null && componentMessages.Count > 0)
                    {
                        foreach (var message in componentMessages)
                        {
                            messages.Add(new Dictionary<string, object>
                            {
                                { "component", componentInfo },
                                { "level", "Warning" },
                                { "text", message },
                                { "description", "Component warning message" },
                                { "source", "component" }
                            });
                        }
                    }
                    
                    // Check for error messages
                    var errorMessages = ghComponent.RuntimeMessages(GH_RuntimeMessageLevel.Error);
                    if (errorMessages != null && errorMessages.Count > 0)
                    {
                        foreach (var message in errorMessages)
                        {
                            messages.Add(new Dictionary<string, object>
                            {
                                { "component", componentInfo },
                                { "level", "Error" },
                                { "text", message },
                                { "description", "Component error message" },
                                { "source", "component" }
                            });
                        }
                    }
                    
                    // Check input parameters for warnings
                    foreach (var inputParam in ghComponent.Params.Input)
                    {
                        var paramMessages = inputParam.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                        if (paramMessages != null && paramMessages.Count > 0)
                        {
                            foreach (var message in paramMessages)
                            {
                                messages.Add(new Dictionary<string, object>
                                {
                                    { "component", componentInfo },
                                    { "parameter", new Dictionary<string, object>
                                        {
                                            { "name", inputParam.Name },
                                            { "nickname", inputParam.NickName },
                                            { "type", "input" }
                                        }
                                    },
                                    { "level", "Warning" },
                                    { "text", message },
                                    { "description", "Parameter warning message" },
                                    { "source", "parameter" }
                                });
                            }
                        }
                        
                        // Check for parameter error messages
                        var paramErrorMessages = inputParam.RuntimeMessages(GH_RuntimeMessageLevel.Error);
                        if (paramErrorMessages != null && paramErrorMessages.Count > 0)
                        {
                            foreach (var message in paramErrorMessages)
                            {
                                messages.Add(new Dictionary<string, object>
                                {
                                    { "component", componentInfo },
                                    { "parameter", new Dictionary<string, object>
                                        {
                                            { "name", inputParam.Name },
                                            { "nickname", inputParam.NickName },
                                            { "type", "input" }
                                        }
                                    },
                                    { "level", "Error" },
                                    { "text", message },
                                    { "description", "Parameter error message" },
                                    { "source", "parameter" }
                                });
                            }
                        }
                        // Check for data collection issues
                        if (inputParam.VolatileDataCount == 0 && inputParam.SourceCount > 0)
                        {
                            messages.Add(new Dictionary<string, object>
                            {
                                { "component", componentInfo },
                                { "parameter", new Dictionary<string, object>
                                    {
                                        { "name", inputParam.Name },
                                        { "nickname", inputParam.NickName },
                                        { "type", "input" }
                                    }
                                },
                                { "level", "Warning" },
                                { "text", $"Parameter '{inputParam.NickName}' failed to collect data" },
                                { "description", "This parameter has connections but no data is being received" },
                                { "source", "data_collection" }
                            });
                        }
                    }
                    // Check output parameters for warnings
                    foreach (var outputParam in ghComponent.Params.Output)
                    {
                        var paramMessages = outputParam.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                        if (paramMessages != null && paramMessages.Count > 0)
                        {
                            foreach (var message in paramMessages)
                            {
                                messages.Add(new Dictionary<string, object>
                                {
                                    { "component", componentInfo },
                                    { "parameter", new Dictionary<string, object>
                                        {
                                            { "name", outputParam.Name },
                                            { "nickname", outputParam.NickName },
                                            { "type", "output" }
                                        }
                                    },
                                    { "level", "Warning" },
                                    { "text", message },
                                    { "description", "Parameter warning message" },
                                    { "source", "parameter" }
                                });
                            }
                        }
                        
                        // Check for output parameter error messages
                        var paramErrorMessages = outputParam.RuntimeMessages(GH_RuntimeMessageLevel.Error);
                        if (paramErrorMessages != null && paramErrorMessages.Count > 0)
                        {
                            foreach (var message in paramErrorMessages)
                            {
                                messages.Add(new Dictionary<string, object>
                                {
                                    { "component", componentInfo },
                                    { "parameter", new Dictionary<string, object>
                                        {
                                            { "name", outputParam.Name },
                                            { "nickname", outputParam.NickName },
                                            { "type", "output" }
                                        }
                                    },
                                    { "level", "Error" },
                                    { "text", message },
                                    { "description", "Parameter error message" },
                                    { "source", "parameter" }
                                });
                            }
                        }
                    }
                }
                
                // Check for floating parameters (parameters without connections)
                if (component is IGH_Param floatingParam)
                {
                    if (floatingParam.Kind == GH_ParamKind.input && floatingParam.SourceCount == 0)
                    {
                        messages.Add(new Dictionary<string, object>
                        {
                            { "component", componentInfo },
                            { "level", "Warning" },
                            { "text", $"Floating parameter '{floatingParam.NickName}' has no connections" },
                            { "description", "This parameter is not connected to any source" },
                            { "source", "floating_parameter" }
                        });
                    }
                }
                
                // Check component state
                if (component is IGH_Component ghComp)
                {
                    // Check if component is hidden
                    if (ghComp.Hidden)
                    {
                        messages.Add(new Dictionary<string, object>
                        {
                            { "component", componentInfo },
                            { "level", "Remark" },
                            { "text", "Component is hidden" },
                            { "description", "This component is hidden from view" },
                            { "source", "component_state" }
                        });
                    }
                    
                    // Check if component is locked
                    if (ghComp.Locked)
                    {
                        messages.Add(new Dictionary<string, object>
                        {
                            { "component", componentInfo },
                            { "level", "Remark" },
                            { "text", "Component is locked" },
                            { "description", "This component is locked and cannot be modified" },
                            { "source", "component_state" }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting messages for component {component.NickName}: {ex.Message}");
            }
            
            return messages;
        }

        /// <summary>
        /// Get all components in the document
        /// </summary>
        /// <param name="command">Command object</param>
        /// <returns>List of all components</returns>
        public static object GetAllComponents(Command command)
        {
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document found.");
                    }
                    
                    // Collect all components
                    var components = new List<Dictionary<string, object>>();
                    foreach (var obj in doc.Objects)
                    {
                        var componentInfo = new Dictionary<string, object>
                        {
                            { "id", obj.InstanceGuid.ToString() },
                            { "type", obj.GetType().Name },
                            { "name", obj.NickName },
                            { "description", obj.Description }
                        };
                        
                        // Add position information if available
                        if (obj.Attributes != null)
                        {
                            componentInfo["x"] = obj.Attributes.Pivot.X;
                            componentInfo["y"] = obj.Attributes.Pivot.Y;
                        }
                        
                        // Add special handling for specific component types
                        if (obj is GH_NumberSlider slider)
                        {
                            componentInfo["value"] = (double)slider.CurrentValue;
                            componentInfo["minimum"] = (double)slider.Slider.Minimum;
                            componentInfo["maximum"] = (double)slider.Slider.Maximum;
                        }
                        else if (obj is GH_Panel panel)
                        {
                            componentInfo["value"] = panel.UserText;
                        }
                        
                        components.Add(componentInfo);
                    }
                    
                    result = components;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetAllComponents: {ex.Message}");
                }
            }));
            
            // Wait for UI thread operation to complete
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            // If there's an exception, throw it
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get all connections in the document
        /// </summary>
        /// <param name="command">Command object</param>
        /// <returns>List of all connections</returns>
        public static object GetConnections(Command command)
        {
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document found.");
                    }
                    
                    // Collect all connections
                    var connections = new List<Dictionary<string, object>>();
                    
                    foreach (var obj in doc.Objects)
                    {
                        if (obj is IGH_Component component)
                        {
                            // Check input parameters for connections
                            foreach (var inputParam in component.Params.Input)
                            {
                                foreach (var source in inputParam.Sources)
                                {
                                    connections.Add(new Dictionary<string, object>
                                    {
                                        { "sourceId", source.InstanceGuid.ToString() },
                                        { "sourceParam", source.Name },
                                        { "targetId", component.InstanceGuid.ToString() },
                                        { "targetParam", inputParam.Name }
                                    });
                                }
                            }
                        }
                    }
                    
                    result = connections;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetConnections: {ex.Message}");
                }
            }));
            
            // Wait for UI thread operation to complete
            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }
            
            // If there's an exception, throw it
            if (exception != null)
            {
                throw exception;
            }
            
            return result;
        }

        public static object SearchComponents(Command command)
        {
            string query = command.GetParameter<string>("query");
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentException("Search query is required");
            }

            object result = null;
            
            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document found.");
                    }

                    var matchingComponents = new List<object>();
                    var queryLower = query.ToLowerInvariant();
                    
                    foreach (var obj in doc.Objects)
                    {
                        var componentName = obj.NickName?.ToLowerInvariant() ?? "";
                        var componentType = obj.GetType().Name.ToLowerInvariant();
                        var componentDesc = obj.Description?.ToLowerInvariant() ?? "";
                        
                        if (componentName.Contains(queryLower) || 
                            componentType.Contains(queryLower) || 
                            componentDesc.Contains(queryLower))
                        {
                            matchingComponents.Add(new
                            {
                                id = obj.InstanceGuid.ToString(),
                                name = obj.NickName,
                                type = obj.GetType().Name,
                                description = obj.Description
                            });
                        }
                    }
                    
                    result = matchingComponents;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }));
            
            return result;
        }

        public static object GetComponentParameters(Command command)
        {
            string componentType = command.GetParameter<string>("componentType");
            if (string.IsNullOrEmpty(componentType))
            {
                throw new ArgumentException("Component type is required");
            }

            // Use IntentRecognizer to get component details
            var componentDetails = IntentRecognizer.GetComponentDetails(componentType);
            if (componentDetails != null)
            {
                return componentDetails;
            }

            // Fallback: return basic info
            return new
            {
                name = componentType,
                inputs = new List<object>(),
                outputs = new List<object>(),
                description = "Component details not available"
            };
        }

        private static IGH_DocumentObject CreateComponentByName(string name)
        {
            var obj = Grasshopper.Instances.ComponentServer.ObjectProxies
                .FirstOrDefault(p => p.Desc.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                
            if (obj != null)
            {
                return obj.CreateInstance();
            }
            else
            {
                throw new ArgumentException($"Component with name {name} not found");
            }
        }
    }
}
