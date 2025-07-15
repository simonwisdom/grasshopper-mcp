using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Utility class providing fuzzy matching functionality
    /// </summary>
    public static class FuzzyMatcher
    {
        // Component name mapping dictionary, maps commonly used simplified names to actual Grasshopper component names
        private static readonly Dictionary<string, string> ComponentNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Plane components
            { "plane", "XY Plane" },
            { "xyplane", "XY Plane" },
            { "xy", "XY Plane" },
            { "xzplane", "XZ Plane" },
            { "xz", "XZ Plane" },
            { "yzplane", "YZ Plane" },
            { "yz", "YZ Plane" },
            { "plane3pt", "Plane 3Pt" },
            { "3ptplane", "Plane 3Pt" },
            
            // Basic geometry components
            { "box", "Box" },
            { "cube", "Box" },
            { "rectangle", "Rectangle" },
            { "rect", "Rectangle" },
            { "circle", "Circle" },
            { "circ", "Circle" },
            { "sphere", "Sphere" },
            { "cylinder", "Cylinder" },
            { "cyl", "Cylinder" },
            { "cone", "Cone" },
            
            // Parameter components
            { "slider", "Number Slider" },
            { "numberslider", "Number Slider" },
            { "panel", "Panel" },
            { "point", "Point" },
            { "pt", "Point" },
            { "line", "Line" },
            { "ln", "Line" },
            { "curve", "Curve" },
            { "crv", "Curve" },
            
            // Data management components
            { "listitem", "List Item" },
            { "list_item", "List Item" },
            { "item", "List Item" },
            { "cullpattern", "Cull Pattern" },
            { "cull_pattern", "Cull Pattern" },
            { "cull", "Cull Pattern" },
            { "graft", "Graft" },
            { "flatten", "Flatten" },
            { "series", "Series" },
            
            // Curve operations
            { "offsetcurve", "Offset" },
            { "offset_curve", "Offset" },
            { "offset", "Offset" },
            { "shatter", "Shatter" },
            { "joincurves", "Join Curves" },
            { "join_curves", "Join Curves" },
            { "join", "Join Curves" },
            { "polyline", "Polyline" },
            { "poly_line", "Polyline" },
            
            // Surface operations
            { "loft", "Loft" },
            
            // Transformation operations
            { "rotate", "Rotate" },
            
            // Geometry creation
            { "ellipse", "Ellipse" },
            { "polygon", "Polygon" },
            
            // Curve operations
            { "arc", "Arc" },
            { "arcsed", "Arc SED" },
            { "arc_sed", "Arc SED" },
            { "evaluatecurve", "Evaluate Curve" },
            { "evaluate_curve", "Evaluate Curve" },
            { "evalcurve", "Evaluate Curve" },
            { "pointoncurve", "Point On Curve" },
            { "point_on_curve", "Point On Curve" },
            { "ptoncurve", "Point On Curve" },
            { "fillet", "Fillet" },
            { "trim", "Trim" },
            
            // Number operations
            { "remapnumbers", "Remap Numbers" },
            { "remap_numbers", "Remap Numbers" },
            { "remap", "Remap Numbers" },
            
            // Surface operations
            { "sweep1", "Sweep1" },
            { "sweep_1", "Sweep1" },
            { "sweep2", "Sweep2" },
            { "sweep_2", "Sweep2" },
            { "revolve", "Revolve" },
            { "capholes", "Cap Holes" },
            { "cap_holes", "Cap Holes" },
            { "offsetsurface", "Offset Surface" },
            { "offset_surface", "Offset Surface" },
            
            // Transform operations
            { "mirror", "Mirror" },
            { "scale", "Scale" },
            { "orient", "Orient" },
            
            // Analysis operations
            { "boundingbox", "Bounding Box" },
            { "bounding_box", "Bounding Box" },
            { "bbox", "Bounding Box" },
            { "area", "Area" },
            { "volume", "Volume" },
            { "centerbox", "Center Box" },
            { "center_box", "Center Box" },
            
            // Data management
            { "dispatch", "Dispatch" },
            { "shiftlist", "Shift List" },
            { "shift_list", "Shift List" },
            { "flipmatrix", "Flip Matrix" },
            { "flip_matrix", "Flip Matrix" }
        };
        
        // Parameter name mapping dictionary, maps commonly used simplified parameter names to actual Grasshopper parameter names
        private static readonly Dictionary<string, string> ParameterNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Plane parameters
            { "plane", "Plane" },
            { "base", "Base" },
            { "origin", "Origin" },
            
            // Dimension parameters
            { "radius", "Radius" },
            { "r", "Radius" },
            { "size", "Size" },
            { "xsize", "X Size" },
            { "ysize", "Y Size" },
            { "zsize", "Z Size" },
            { "width", "X Size" },
            { "length", "Y Size" },
            { "height", "Z Size" },
            { "x", "X" },
            { "y", "Y" },
            { "z", "Z" },
            
            // Point parameters
            { "point", "Point" },
            { "pt", "Point" },
            { "center", "Center" },
            { "start", "Start" },
            { "end", "End" },
            
            // Numeric parameters
            { "number", "Number" },
            { "num", "Number" },
            { "value", "Value" },
            
            // Output parameters
            { "result", "Result" },
            { "output", "Output" },
            { "geometry", "Geometry" },
            { "geo", "Geometry" },
            { "brep", "Brep" },
            
            // Data management parameters
            { "list", "List" },
            { "index", "Index" },
            { "pattern", "Pattern" },
            { "mask", "Mask" },
            { "data", "Data" },
            { "tree", "Tree" },
            { "path", "Path" },
            { "count", "Count" },
            { "steps", "Steps" },
            
            // Curve operation parameters
            { "distance", "Distance" },
            { "side", "Side" },
            { "corners", "Corners" },
            { "tolerance", "Tolerance" },
            { "angle", "Angle" },
            { "axis", "Axis" },
            { "vertices", "Vertices" },
            { "degree", "Degree" },
            { "periodic", "Periodic" },
            
            // Surface operation parameters
            { "profiles", "Profiles" },
            { "rails", "Rails" },
            { "closed", "Closed" },
            { "type", "Type" },
            
            // Geometry creation parameters
            { "major", "Major" },
            { "minor", "Minor" },
            { "sides", "Sides" },
            { "startangle", "Start Angle" },
            { "start_angle", "Start Angle" },
            { "endangle", "End Angle" },
            { "end_angle", "End Angle" },
            { "startpoint", "Start Point" },
            { "start_point", "Start Point" },
            { "endpoint", "End Point" },
            { "end_point", "End Point" },
            { "direction", "Direction" },
            { "midpoint", "Mid Point" },
            { "mid_point", "Mid Point" },
            
            // Curve evaluation parameters
            { "parameter", "Parameter" },
            { "param", "Parameter" },
            { "t", "Parameter" },
            { "point", "Point" },
            { "tangent", "Tangent" },
            { "normal", "Normal" },
            { "curvature", "Curvature" },
            
            // Number operation parameters
            { "source", "Source" },
            { "target", "Target" },
            { "source_domain", "Source Domain" },
            { "target_domain", "Target Domain" },
            { "source_dom", "Source Domain" },
            { "target_dom", "Target Domain" },
            
            // Surface operation parameters
            { "path", "Path" },
            { "profile", "Profile" },
            { "rail", "Rail" },
            { "axis", "Axis" },
            { "angle", "Angle" },
            { "holes", "Holes" },
            { "distance", "Distance" },
            { "side", "Side" },
            { "corners", "Corners" },
            { "tolerance", "Tolerance" },
            
            // Transform operation parameters
            { "mirror_plane", "Mirror Plane" },
            { "mirrorplane", "Mirror Plane" },
            { "factor", "Factor" },
            { "factors", "Factors" },
            { "reference", "Reference" },
            { "target_plane", "Target Plane" },
            { "targetplane", "Target Plane" },
            
            // Analysis operation parameters
            { "bounding_box", "Bounding Box" },
            { "boundingbox", "Bounding Box" },
            { "bbox", "Bounding Box" },
            { "area", "Area" },
            { "volume", "Volume" },
            { "center_box", "Center Box" },
            { "centerbox", "Center Box" },
            
            // Data management parameters
            { "dispatch", "Dispatch" },
            { "shift", "Shift" },
            { "wrap", "Wrap" },
            { "flip", "Flip" },
            { "matrix", "Matrix" },
            { "rows", "Rows" },
            { "columns", "Columns" }
        };
        
        /// <summary>
        /// Get the closest component name
        /// </summary>
        /// <param name="input">Input component name</param>
        /// <returns>Mapped component name</returns>
        public static string GetClosestComponentName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;
                
            // Try direct mapping
            string normalizedInput = input.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (ComponentNameMap.TryGetValue(normalizedInput, out string mappedName))
                return mappedName;
                
            // If no direct mapping, return original input
            return input;
        }
        
        /// <summary>
        /// Get the closest parameter name
        /// </summary>
        /// <param name="input">Input parameter name</param>
        /// <returns>Mapped parameter name</returns>
        public static string GetClosestParameterName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;
                
            // Try direct mapping
            string normalizedInput = input.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (ParameterNameMap.TryGetValue(normalizedInput, out string mappedName))
                return mappedName;
                
            // If no direct mapping, return original input
            return input;
        }
        
        /// <summary>
        /// Find the closest string from a list
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="candidates">Candidate string list</param>
        /// <returns>Closest string</returns>
        public static string FindClosestMatch(string input, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(input) || candidates == null || !candidates.Any())
                return input;
                
            // First try exact match
            var exactMatch = candidates.FirstOrDefault(c => string.Equals(c, input, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;
                
            // Try contains match
            var containsMatches = candidates.Where(c => c.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (containsMatches.Count == 1)
                return containsMatches[0];
                
            // Try prefix match
            var prefixMatches = candidates.Where(c => c.StartsWith(input, StringComparison.OrdinalIgnoreCase)).ToList();
            if (prefixMatches.Count == 1)
                return prefixMatches[0];
                
            // If there are multiple matches, return the shortest one
            if (containsMatches.Any())
                return containsMatches.OrderBy(c => c.Length).First();
                
            // If no match, return original input
            return input;
        }
    }
}
