using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GH_MCP.Commands;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Rhino;
using Newtonsoft.Json;
using System.IO;

namespace GrasshopperMCP
{
    /// <summary>
    /// Grasshopper MCP component for communicating with Python server
    /// </summary>
    public class GrasshopperMCPComponent : GH_Component
    {
        private static TcpListener listener;
        private static bool isRunning = false;
        private static int grasshopperPort = 8080;
        private static bool autoStart = true; // Auto-start flag
        private static bool hasInitialized = false; // Prevent duplicate initialization
        
        /// <summary>
        /// Initialize a new instance of GrasshopperMCPComponent class
        /// </summary>
        public GrasshopperMCPComponent()
            : base("Grasshopper MCP", "MCP", "Machine Control Protocol for Grasshopper", "Params", "Util")
        {
        }
        
        /// <summary>
        /// Register input parameters
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Enabled", "E", "Enable or disable the MCP server", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("Port", "P", "Port to listen on", GH_ParamAccess.item, 8080);
            pManager.AddBooleanParameter("Auto Start", "A", "Automatically start server when component is added", GH_ParamAccess.item, true);
        }
        
        /// <summary>
        /// Register output parameters
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "S", "Server status", GH_ParamAccess.item);
            pManager.AddTextParameter("LastCommand", "C", "Last received command", GH_ParamAccess.item);
            pManager.AddTextParameter("Connection Info", "I", "Connection information", GH_ParamAccess.item);
        }
        
        /// <summary>
        /// Called when component is initialized
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            
            // Check if auto-start is needed (only on first initialization)
            if (!hasInitialized && autoStart && !isRunning)
            {
                hasInitialized = true;
                // Delay start to ensure component is fully initialized
                Task.Delay(500).ContinueWith((Task _) => 
                {
                    RhinoApp.InvokeOnUiThread((Action)(() => 
                    {
                        if (!isRunning)
                        {
                            Start();
                        }
                    }));
                });
            }
        }
        
        /// <summary>
        /// Solve component
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool enabled = true;
            int port = 8080;
            bool autoStartParam = true;
            
            // Get input parameters, use defaults if not provided
            DA.GetData(0, ref enabled);
            DA.GetData(1, ref port);
            DA.GetData(2, ref autoStartParam);
            
            // Update global variables
            grasshopperPort = port;
            autoStart = autoStartParam;
            
            // Start or stop server based on enabled state
            if (enabled && !isRunning)
            {
                Start();
                DA.SetData(0, $"Running on port {grasshopperPort}");
                DA.SetData(2, $"Server active on localhost:{grasshopperPort}");
            }
            else if (!enabled && isRunning)
            {
                Stop();
                DA.SetData(0, "Stopped");
                DA.SetData(2, "Server stopped");
            }
            else if (enabled && isRunning)
            {
                DA.SetData(0, $"Running on port {grasshopperPort}");
                DA.SetData(2, $"Server active on localhost:{grasshopperPort}");
            }
            else
            {
                DA.SetData(0, "Stopped");
                DA.SetData(2, "Server stopped");
            }
            
            // Set last received command
            DA.SetData(1, LastCommand);
        }
        
        /// <summary>
        /// Component GUID
        /// </summary>
        public override Guid ComponentGuid => new Guid("12345678-1234-1234-1234-123456789012");
        
        /// <summary>
        /// Expose icon
        /// </summary>
        protected override Bitmap Icon => null;
        
        /// <summary>
        /// Last received command
        /// </summary>
        public static string LastCommand { get; private set; } = "None";
        
        /// <summary>
        /// Start MCP server
        /// </summary>
        public static void Start()
        {
            if (isRunning) return;
            
            try
            {
                // Initialize command registry
                GrasshopperCommandRegistry.Initialize();
                
                // Start TCP listener
                isRunning = true;
                listener = new TcpListener(IPAddress.Loopback, grasshopperPort);
                listener.Start();
                RhinoApp.WriteLine($"GrasshopperMCPBridge started on port {grasshopperPort}.");
                
                // Start accepting connections
                Task.Run(ListenerLoop);
            }
            catch (Exception ex)
            {
                isRunning = false;
                RhinoApp.WriteLine($"Failed to start GrasshopperMCPBridge: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Stop MCP server
        /// </summary>
        public static void Stop()
        {
            if (!isRunning) return;
            
            try
            {
                isRunning = false;
                listener?.Stop();
                RhinoApp.WriteLine("GrasshopperMCPBridge stopped.");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error stopping GrasshopperMCPBridge: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Listener loop to handle incoming connections
        /// </summary>
        private static async Task ListenerLoop()
        {
            try
            {
                while (isRunning)
                {
                    // Wait for client connection
                    var client = await listener.AcceptTcpClientAsync();
                    RhinoApp.WriteLine("GrasshopperMCPBridge: Client connected.");
                    
                    // Handle client connection
                    _ = Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    RhinoApp.WriteLine($"GrasshopperMCPBridge error: {ex.Message}");
                    isRunning = false;
                }
            }
        }
        
        /// <summary>
        /// Handle client connection
        /// </summary>
        /// <param name="client">TCP client</param>
        private static async Task HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
            {
                try
                {
                    // Read command
                    string commandJson = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(commandJson))
                    {
                        return;
                    }
                    
                    // Update last received command
                    LastCommand = commandJson;
                    
                    // Parse command
                    Command command = JsonConvert.DeserializeObject<Command>(commandJson);
                    RhinoApp.WriteLine($"GrasshopperMCPBridge: Received command: {command.Type} with parameters: {JsonConvert.SerializeObject(command.Parameters)}");
                    
                    // Execute command
                    Response response = GrasshopperCommandRegistry.ExecuteCommand(command);
                    
                    // Send response
                    string responseJson = JsonConvert.SerializeObject(response);
                    await writer.WriteLineAsync(responseJson);
                    
                    RhinoApp.WriteLine($"GrasshopperMCPBridge: Command {command.Type} executed with result: {(response.Success ? "Success" : "Error")}");
                    if (!response.Success)
                    {
                        RhinoApp.WriteLine($"GrasshopperMCPBridge: Error details: {response.Error}");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"GrasshopperMCPBridge error handling client: {ex.Message}");
                    
                    // Send error response
                    Response errorResponse = Response.CreateError($"Server error: {ex.Message}");
                    string errorResponseJson = JsonConvert.SerializeObject(errorResponse);
                    await writer.WriteLineAsync(errorResponseJson);
                }
            }
        }
        
        /// <summary>
        /// Get additional component menu items
        /// </summary>
        /// <param name="menu">Menu</param>
        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            
            // Add start/stop menu item
            var startStopItem = new System.Windows.Forms.ToolStripMenuItem(
                isRunning ? "Stop Server" : "Start Server",
                null,
                (System.EventHandler)((sender, e) => 
                {
                    if (isRunning)
                        Stop();
                    else
                        Start();
                    ExpireSolution();
                })
            );
            menu.Items.Add(startStopItem);
            
            // Add separator
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            
            // Add port configuration menu item
            var portItem = new System.Windows.Forms.ToolStripMenuItem(
                $"Current Port: {grasshopperPort}",
                null,
                (System.EventHandler)((sender, e) => 
                {
                    // Use simple message box to inform user about port modification
                    var result = System.Windows.Forms.MessageBox.Show(
                        $"Current port: {grasshopperPort}\n\nTo change the port, modify the 'Port' input parameter and set 'Enabled' to false, then back to true.",
                        "Port Configuration",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                })
            );
            menu.Items.Add(portItem);
            
            // Add status information menu item
            var statusItem = new System.Windows.Forms.ToolStripMenuItem(
                $"Status: {(isRunning ? "Running" : "Stopped")}",
                null
            );
            statusItem.Enabled = false;
            menu.Items.Add(statusItem);
            
            // Add reset initialization flag menu item
            var resetItem = new System.Windows.Forms.ToolStripMenuItem(
                "Reset Auto-Start",
                null,
                (System.EventHandler)((sender, e) => 
                {
                    hasInitialized = false;
                    ExpireSolution();
                })
            );
            menu.Items.Add(resetItem);
        }
    }
}
