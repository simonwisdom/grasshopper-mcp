using System;
using System.Collections.Generic;
using System.IO;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Rhino;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            var tcs = new TaskCompletionSource<object>();
            
            RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        tcs.SetException(new InvalidOperationException("No active Grasshopper document"));
                        return;
                    }
                    
                    var components = doc.Objects.Select(obj => new
                    {
                        id = obj.InstanceGuid.ToString(),
                        type = obj.GetType().Name,
                        name = obj.NickName
                    }).ToList();
                    
                    tcs.SetResult(new
                    {
                        name = doc.DisplayName,
                        path = doc.FilePath,
                        componentCount = doc.Objects.Count,
                        components = components
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
        /// Clear document
        /// </summary>
        /// <param name="command">Command</param>
        /// <returns>Operation result</returns>
        public static object ClearDocument(Command command)
        {
            var tcs = new TaskCompletionSource<object>();

            RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        tcs.SetException(new InvalidOperationException("No active Grasshopper document"));
                        return;
                    }

                    var objectsToRemove = doc.Objects.Where(obj => !IsEssentialComponent(obj)).ToList();
                    
                    doc.RemoveObjects(objectsToRemove, false);
                    doc.NewSolution(false);

                    tcs.SetResult(new
                    {
                        success = true,
                        message = "Document cleared"
                    });
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task.Result;
        }

        private static bool IsEssentialComponent(IGH_DocumentObject obj)
        {
            if (obj.NickName.Contains("MCP") || obj.NickName.Contains("Claude")) return true;
            if (obj.GetType().Name.Contains("GH_MCP")) return true;
            if (obj.Description.Contains("Machine Control Protocol")) return true;
            if (obj is GH_BooleanToggle || obj is GH_Panel) return true;
            
            // A more robust way would be to add a tag to the component's user data
            // For now, we rely on naming conventions.
            
            return false;
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
            
            throw new NotImplementedException("SaveDocument is temporarily disabled due to API compatibility issues. Please save the document manually.");
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
            
            throw new NotImplementedException("LoadDocument is temporarily disabled due to API compatibility issues. Please load the document manually.");
        }
    }
}
