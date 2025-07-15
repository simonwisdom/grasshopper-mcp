using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace GrasshopperMCP
{
  /// <summary>
  /// Assembly information for the Grasshopper MCP component
  /// </summary>
  public class GH_MCPInfo : GH_AssemblyInfo
  {
    /// <summary>
    /// Gets the name of the assembly
    /// </summary>
    public override string Name => "GH_MCP";

    /// <summary>
    /// Gets the icon for the assembly (24x24 pixel bitmap)
    /// </summary>
    public override Bitmap Icon => null;

    /// <summary>
    /// Gets the description of the assembly
    /// </summary>
    public override string Description => "Machine Control Protocol bridge for Grasshopper, enabling natural language control through Claude Desktop";

    /// <summary>
    /// Gets the unique identifier for the assembly
    /// </summary>
    public override Guid Id => new Guid("1b472cf6-015c-496a-a0a1-7ced4df994a3");

    /// <summary>
    /// Gets the author name
    /// </summary>
    public override string AuthorName => "Simon Wisdom";

    /// <summary>
    /// Gets the author contact information
    /// </summary>
    public override string AuthorContact => "https://github.com/simonwisdom/grasshopper-mcp";

    /// <summary>
    /// Gets the assembly version
    /// </summary>
    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
  }
}