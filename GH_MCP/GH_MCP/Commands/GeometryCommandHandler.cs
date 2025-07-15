using System;
using System.Collections.Generic;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Newtonsoft.Json.Linq;
using System.Linq;
using Rhino;

namespace GrasshopperMCP.Commands
{
    /// <summary>
    /// Handler for geometry-related commands
    /// </summary>
    public static class GeometryCommandHandler
    {
        /// <summary>
        /// Create a point
        /// </summary>
        /// <param name="command">Command containing point coordinates</param>
        /// <returns>Information about the created point</returns>
        public static object CreatePoint(Command command)
        {
            double x = command.GetParameter<double>("x");
            double y = command.GetParameter<double>("y");
            double z = command.GetParameter<double>("z");
            
            // Create point
            Point3d point = new Point3d(x, y, z);
            
            // Return point information
            return new
            {
                id = Guid.NewGuid().ToString(),
                x = point.X,
                y = point.Y,
                z = point.Z
            };
        }
        
        /// <summary>
        /// Create a curve
        /// </summary>
        /// <param name="command">Command containing curve points</param>
        /// <returns>Information about the created curve</returns>
        public static object CreateCurve(Command command)
        {
            var pointsData = command.GetParameter<JArray>("points");
            
            if (pointsData == null || pointsData.Count < 2)
            {
                throw new ArgumentException("At least 2 points are required to create a curve");
            }
            
            // Convert JSON point data to a list of Point3d
            List<Point3d> points = new List<Point3d>();
            foreach (var pointData in pointsData)
            {
                double x = pointData["x"].Value<double>();
                double y = pointData["y"].Value<double>();
                double z = pointData["z"]?.Value<double>() ?? 0.0;
                
                points.Add(new Point3d(x, y, z));
            }
            
            // Create curve
            Curve curve;
            if (points.Count == 2)
            {
                // If there are only two points, create a line
                curve = new LineCurve(points[0], points[1]);
            }
            else
            {
                // If there are multiple points, create an interpolated curve
                int degree = Math.Min(3, points.Count - 1);
                curve = Curve.CreateInterpolatedCurve(points, degree);
            }
            
            // Return curve information
            return new
            {
                id = Guid.NewGuid().ToString(),
                pointCount = points.Count,
                length = curve.GetLength()
            };
        }
        
        /// <summary>
        /// Create a circle
        /// </summary>
        /// <param name="command">Command containing center and radius</param>
        /// <returns>Information about the created circle</returns>
        public static object CreateCircle(Command command)
        {
            var centerData = command.GetParameter<JObject>("center");
            double radius = command.GetParameter<double>("radius");
            
            if (centerData == null)
            {
                throw new ArgumentException("Center point is required");
            }
            
            if (radius <= 0)
            {
                throw new ArgumentException("Radius must be greater than 0");
            }
            
            // Parse center
            double x = centerData["x"].Value<double>();
            double y = centerData["y"].Value<double>();
            double z = centerData["z"]?.Value<double>() ?? 0.0;
            
            Point3d center = new Point3d(x, y, z);
            
            // Create circle
            Circle circle = new Circle(center, radius);
            
            // Return circle information
            return new
            {
                id = Guid.NewGuid().ToString(),
                center = new { x = center.X, y = center.Y, z = center.Z },
                radius = circle.Radius,
                circumference = circle.Circumference
            };
        }
    }
}
