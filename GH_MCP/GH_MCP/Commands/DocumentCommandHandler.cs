using System;
using System.Collections.Generic;
using System.IO;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Rhino;
using System.Linq;
using System.Threading;

namespace GrasshopperMCP.Commands
{
    /// <summary>
    /// Handler for document-related commands
    /// </summary>
    public static class DocumentCommandHandler
    {
        /// <summary>
        /// Get document information
        /// </summary>
        /// <param name="command">Command</param>
        /// <returns>Document information</returns>
        public static object GetDocumentInfo(Command command)
        {
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }
                    
                    // Collect component information
                    var components = new List<object>();
                    foreach (var obj in doc.Objects)
                    {
                        var componentInfo = new Dictionary<string, object>
                        {
                            { "id", obj.InstanceGuid.ToString() },
                            { "type", obj.GetType().Name },
                            { "name", obj.NickName }
                        };
                        
                        components.Add(componentInfo);
                    }
                    
                    // Collect document information
                    var docInfo = new Dictionary<string, object>
                    {
                        { "name", doc.DisplayName },
                        { "path", doc.FilePath },
                        { "componentCount", doc.Objects.Count },
                        { "components", components }
                    };
                    
                    result = docInfo;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetDocumentInfo: {ex.Message}");
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
        /// Clear document
        /// </summary>
        /// <param name="command">Command</param>
        /// <returns>Operation result</returns>
        public static object ClearDocument(Command command)
        {
            object result = null;
            Exception exception = null;
            
            // Execute on UI thread
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    // Get Grasshopper document
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }
                    
                    // Create a new document object list to avoid modifying collection during iteration
                    var objectsToRemove = doc.Objects.ToList();
                    
                    // Filter out essential components (keep those used for communication with Claude Desktop)
                    // We can identify essential components by GUID, name, or type
                    var essentialComponents = objectsToRemove.Where(obj => 
                        // Check if component name contains specific keywords
                        obj.NickName.Contains("MCP") || 
                        obj.NickName.Contains("Claude") ||
                        // Or check component type
                        obj.GetType().Name.Contains("GH_MCP") ||
                        // Or check component description
                        obj.Description.Contains("Machine Control Protocol") ||
                        // Keep toggle components
                        obj.GetType().Name.Contains("GH_BooleanToggle") ||
                        // Keep panel components (for displaying status)
                        obj.GetType().Name.Contains("GH_Panel") ||
                        // Additional component name checks
                        obj.NickName.Contains("Toggle") ||
                        obj.NickName.Contains("Status") ||
                        obj.NickName.Contains("Panel")
                    ).ToList();
                    
                    // Remove essential components from the deletion list
                    foreach (var component in essentialComponents)
                    {
                        objectsToRemove.Remove(component);
                    }
                    
                    // Clear document (only delete non-essential components)
                    doc.RemoveObjects(objectsToRemove, false);
                    
                    // Refresh canvas
                    doc.NewSolution(false);
                    
                    // Return operation result
                    result = new
                    {
                        success = true,
                        message = "Document cleared"
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in ClearDocument: {ex.Message}");
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
        /// Save document
        /// </summary>
        /// <param name="command">Command</param>
        /// <returns>Operation result</returns>
        public static object SaveDocument(Command command)
        {
            string path = command.GetParameter<string>("path");
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Save path is required");
            }
            
            // Return an error message indicating this feature is temporarily unavailable
            return new
            {
                success = false,
                message = "SaveDocument is temporarily disabled due to API compatibility issues. Please save the document manually."
            };
        }
        
        /// <summary>
        /// Load document
        /// </summary>
        /// <param name="command">Command</param>
        /// <returns>Operation result</returns>
        public static object LoadDocument(Command command)
        {
            string path = command.GetParameter<string>("path");
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Load path is required");
            }
            
            // Return an error message indicating this feature is temporarily unavailable
            return new
            {
                success = false,
                message = "LoadDocument is temporarily disabled due to API compatibility issues. Please load the document manually."
            };
        }
    }
}
