using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using GH_MCP.Models;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Responsible for recognizing user intent and converting it to specific components and connections
    /// </summary>
    public class IntentRecognizer
    {
        private static JObject _knowledgeBase;
        private static bool _initialized = false;

        /// <summary>
        /// Initialize knowledge base
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                // Try multiple possible paths in order of preference
                var possiblePaths = new List<string>
                {
                    // Current assembly directory
                    Path.Combine(Path.GetDirectoryName(typeof(IntentRecognizer).Assembly.Location), "Resources", "ComponentKnowledgeBase.json"),
                    
                    // Relative to current assembly
                    Path.Combine(Path.GetDirectoryName(typeof(IntentRecognizer).Assembly.Location), "..", "Resources", "ComponentKnowledgeBase.json"),
                    Path.Combine(Path.GetDirectoryName(typeof(IntentRecognizer).Assembly.Location), "..", "..", "Resources", "ComponentKnowledgeBase.json"),
                    
                    // Current working directory
                    Path.Combine(Environment.CurrentDirectory, "Resources", "ComponentKnowledgeBase.json"),
                    Path.Combine(Environment.CurrentDirectory, "GH_MCP", "GH_MCP", "Resources", "ComponentKnowledgeBase.json"),
                    
                    // User's home directory (for development)
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "2025", "grasshopper-mcp-project", "grasshopper-mcp-master", "GH_MCP", "GH_MCP", "Resources", "ComponentKnowledgeBase.json"),
                    
                    // Common Rhino plugin directories
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "McNeel", "Rhinoceros", "8.0", "Plug-ins", "Grasshopper", "Libraries", "Resources", "ComponentKnowledgeBase.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "McNeel", "Rhinoceros", "8.0", "Plug-ins", "Grasshopper", "Resources", "ComponentKnowledgeBase.json"),
                    
                    // macOS specific paths
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "McNeel", "Rhinoceros", "8.0", "Plug-ins", "Grasshopper", "Libraries", "Resources", "ComponentKnowledgeBase.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "McNeel", "Rhinoceros", "8.0", "Plug-ins", "Grasshopper", "Resources", "ComponentKnowledgeBase.json")
                };

                string foundPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath != null)
                {
                    string json = File.ReadAllText(foundPath);
                    _knowledgeBase = JObject.Parse(json);
                    RhinoApp.WriteLine($"GH_MCP: Component knowledge base loaded from {foundPath}");
                    _initialized = true;
                }
                else
                {
                    RhinoApp.WriteLine($"GH_MCP: Component knowledge base not found. Tried paths:");
                    foreach (var path in possiblePaths)
                    {
                        RhinoApp.WriteLine($"  - {path}");
                    }
                    
                    // Create a minimal knowledge base with basic patterns
                    CreateMinimalKnowledgeBase();
                    _initialized = true;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"GH_MCP: Error loading component knowledge base: {ex.Message}");
                CreateMinimalKnowledgeBase();
                _initialized = true;
            }
        }

        /// <summary>
        /// Create a minimal knowledge base with basic patterns when the file is not found
        /// </summary>
        private static void CreateMinimalKnowledgeBase()
        {
            var minimalJson = @"{
                ""patterns"": [
                    {
                        ""name"": ""Circle"",
                        ""description"": ""Creates a simple circle"",
                        ""components"": [
                            {""type"": ""XY Plane"", ""x"": 100, ""y"": 100, ""id"": ""plane""},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 200, ""id"": ""radius"", ""settings"": {""min"": 0, ""max"": 50, ""value"": 10}},
                            {""type"": ""Circle"", ""x"": 400, ""y"": 150, ""id"": ""circle""}
                        ],
                        ""connections"": [
                            {""source"": ""plane"", ""sourceParam"": ""Plane"", ""target"": ""circle"", ""targetParam"": ""Plane""},
                            {""source"": ""radius"", ""sourceParam"": ""Number"", ""target"": ""circle"", ""targetParam"": ""Radius""}
                        ]
                    },
                    {
                        ""name"": ""3D Box"",
                        ""description"": ""Creates a simple 3D box"",
                        ""components"": [
                            {""type"": ""XY Plane"", ""x"": 100, ""y"": 100, ""id"": ""plane""},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 200, ""id"": ""sliderX"", ""settings"": {""min"": 0, ""max"": 50, ""value"": 20}},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 250, ""id"": ""sliderY"", ""settings"": {""min"": 0, ""max"": 50, ""value"": 20}},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 300, ""id"": ""sliderZ"", ""settings"": {""min"": 0, ""max"": 50, ""value"": 20}},
                            {""type"": ""Box"", ""x"": 400, ""y"": 200, ""id"": ""box""}
                        ],
                        ""connections"": [
                            {""source"": ""plane"", ""sourceParam"": ""Plane"", ""target"": ""box"", ""targetParam"": ""Base""},
                            {""source"": ""sliderX"", ""sourceParam"": ""Number"", ""target"": ""box"", ""targetParam"": ""X Size""},
                            {""source"": ""sliderY"", ""sourceParam"": ""Number"", ""target"": ""box"", ""targetParam"": ""Y Size""},
                            {""source"": ""sliderZ"", ""sourceParam"": ""Number"", ""target"": ""box"", ""targetParam"": ""Z Size""}
                        ]
                    },
                    {
                        ""name"": ""Grid"",
                        ""description"": ""Creates a simple grid pattern"",
                        ""components"": [
                            {""type"": ""XY Plane"", ""x"": 100, ""y"": 100, ""id"": ""plane""},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 200, ""id"": ""sizeX"", ""settings"": {""min"": 0, ""max"": 100, ""value"": 50}},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 250, ""id"": ""sizeY"", ""settings"": {""min"": 0, ""max"": 100, ""value"": 50}},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 300, ""id"": ""countX"", ""settings"": {""min"": 1, ""max"": 20, ""value"": 10}},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 350, ""id"": ""countY"", ""settings"": {""min"": 1, ""max"": 20, ""value"": 10}},
                            {""type"": ""Populate 3D"", ""x"": 400, ""y"": 250, ""id"": ""populate""}
                        ],
                        ""connections"": [
                            {""source"": ""plane"", ""sourceParam"": ""Plane"", ""target"": ""populate"", ""targetParam"": ""Base""},
                            {""source"": ""sizeX"", ""sourceParam"": ""Number"", ""target"": ""populate"", ""targetParam"": ""Size X""},
                            {""source"": ""sizeY"", ""sourceParam"": ""Number"", ""target"": ""populate"", ""targetParam"": ""Size Y""},
                            {""source"": ""countX"", ""sourceParam"": ""Number"", ""target"": ""populate"", ""targetParam"": ""Count X""},
                            {""source"": ""countY"", ""sourceParam"": ""Number"", ""target"": ""populate"", ""targetParam"": ""Count Y""}
                        ]
                    },
                    {
                        ""name"": ""Voronoi"",
                        ""description"": ""Creates a Voronoi diagram from random points"",
                        ""components"": [
                            {""type"": ""XY Plane"", ""x"": 100, ""y"": 100, ""id"": ""plane""},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 200, ""id"": ""sizeX"", ""settings"": {""min"": 0, ""max"": 100, ""value"": 50}},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 250, ""id"": ""sizeY"", ""settings"": {""min"": 0, ""max"": 100, ""value"": 50}},
                            {""type"": ""Number Slider"", ""x"": 100, ""y"": 300, ""id"": ""count"", ""settings"": {""min"": 5, ""max"": 50, ""value"": 20}},
                            {""type"": ""Populate 3D"", ""x"": 400, ""y"": 250, ""id"": ""populate""},
                            {""type"": ""Voronoi"", ""x"": 600, ""y"": 250, ""id"": ""voronoi""}
                        ],
                        ""connections"": [
                            {""source"": ""plane"", ""sourceParam"": ""Plane"", ""target"": ""populate"", ""targetParam"": ""Base""},
                            {""source"": ""sizeX"", ""sourceParam"": ""Number"", ""target"": ""populate"", ""targetParam"": ""Size X""},
                            {""source"": ""sizeY"", ""sourceParam"": ""Number"", ""target"": ""populate"", ""targetParam"": ""Size Y""},
                            {""source"": ""count"", ""sourceParam"": ""Number"", ""target"": ""populate"", ""targetParam"": ""Count X""},
                            {""source"": ""populate"", ""sourceParam"": ""Points"", ""target"": ""voronoi"", ""targetParam"": ""Points""}
                        ]
                    }
                ],
                ""intents"": [
                    {
                        ""keywords"": [""circle"", ""round"", ""disc"", ""simple"", ""basic""],
                        ""pattern"": ""Circle""
                    },
                    {
                        ""keywords"": [""box"", ""cube"", ""rectangular"", ""prism"", ""3d""],
                        ""pattern"": ""3D Box""
                    },
                    {
                        ""keywords"": [""grid"", ""pattern"", ""array"", ""matrix"", ""points""],
                        ""pattern"": ""Grid""
                    },
                    {
                        ""keywords"": [""voronoi"", ""cell"", ""diagram"", ""cellular"", ""tessellation""],
                        ""pattern"": ""Voronoi""
                    }
                ]
            }";

            _knowledgeBase = JObject.Parse(minimalJson);
            RhinoApp.WriteLine("GH_MCP: Using minimal knowledge base with basic patterns");
        }

        /// <summary>
        /// Recognize intent from user description
        /// </summary>
        /// <param name="description">User description</param>
        /// <returns>Recognized pattern name, returns null if no match</returns>
        public static string RecognizeIntent(string description)
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            if (_knowledgeBase["intents"] == null)
            {
                RhinoApp.WriteLine("GH_MCP: No intents found in knowledge base");
                return null;
            }

            // Convert description to lowercase and split into words
            string[] words = description.ToLowerInvariant().Split(
                new[] { ' ', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}' },
                StringSplitOptions.RemoveEmptyEntries
            );

            RhinoApp.WriteLine($"GH_MCP: Analyzing description: '{description}'");
            RhinoApp.WriteLine($"GH_MCP: Extracted words: [{string.Join(", ", words)}]");

            // Calculate match score for each intent
            var intentScores = new Dictionary<string, int>();

            foreach (var intent in _knowledgeBase["intents"])
            {
                string patternName = intent["pattern"].ToString();
                var keywords = intent["keywords"].ToObject<List<string>>();

                // Count matching keywords
                int matchCount = words.Count(word => keywords.Contains(word));

                if (matchCount > 0)
                {
                    intentScores[patternName] = matchCount;
                    RhinoApp.WriteLine($"GH_MCP: Pattern '{patternName}' matched {matchCount} keywords: [{string.Join(", ", keywords.Where(k => words.Contains(k)))}]");
                }
            }

            // Return the intent with the highest score
            if (intentScores.Count > 0)
            {
                var bestMatch = intentScores.OrderByDescending(pair => pair.Value).First();
                RhinoApp.WriteLine($"GH_MCP: Best match: '{bestMatch.Key}' with score {bestMatch.Value}");
                return bestMatch.Key;
            }

            RhinoApp.WriteLine("GH_MCP: No matching patterns found");
            RhinoApp.WriteLine($"GH_MCP: Available patterns: [{string.Join(", ", GetAllPatternNames())}]");
            return null;
        }

        public static List<string> GetAllPatternNames()
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            var patternNames = new List<string>();

            if (_knowledgeBase["patterns"] != null)
            {
                foreach (var pattern in _knowledgeBase["patterns"])
                {
                    patternNames.Add(pattern["name"].ToString());
                }
            }

            return patternNames;
        }

        /// <summary>
        /// Get components and connections for specified pattern
        /// </summary>
        /// <param name="patternName">Pattern name</param>
        /// <returns>Tuple containing components and connections</returns>
        public static (List<ComponentInfo> Components, List<ConnectionInfo> Connections) GetPatternDetails(string patternName)
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            var components = new List<ComponentInfo>();
            var connections = new List<ConnectionInfo>();

            if (_knowledgeBase["patterns"] == null)
            {
                return (components, connections);
            }

            // Find matching pattern
            var pattern = _knowledgeBase["patterns"].FirstOrDefault(p => p["name"].ToString() == patternName);
            if (pattern == null)
            {
                return (components, connections);
            }

            // Get component information
            foreach (var comp in pattern["components"])
            {
                var componentInfo = new ComponentInfo
                {
                    Type = comp["type"].ToString(),
                    X = comp["x"].Value<double>(),
                    Y = comp["y"].Value<double>(),
                    Id = comp["id"].ToString()
                };

                // If there are settings, add them
                if (comp["settings"] != null)
                {
                    componentInfo.Settings = comp["settings"].ToObject<Dictionary<string, object>>();
                }

                components.Add(componentInfo);
            }

            // Get connection information
            foreach (var conn in pattern["connections"])
            {
                connections.Add(new ConnectionInfo
                {
                    SourceId = conn["source"].ToString(),
                    SourceParam = conn["sourceParam"].ToString(),
                    TargetId = conn["target"].ToString(),
                    TargetParam = conn["targetParam"].ToString()
                });
            }

            return (components, connections);
        }

        /// <summary>
        /// Get all available component types
        /// </summary>
        /// <returns>List of component types</returns>
        public static List<string> GetAvailableComponentTypes()
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            var types = new List<string>();

            if (_knowledgeBase["components"] != null)
            {
                foreach (var comp in _knowledgeBase["components"])
                {
                    types.Add(comp["name"].ToString());
                }
            }

            return types;
        }

        /// <summary>
        /// Get detailed component information
        /// </summary>
        /// <param name="componentType">Component type</param>
        /// <returns>Detailed component information</returns>
        public static JObject GetComponentDetails(string componentType)
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            if (_knowledgeBase["components"] != null)
            {
                var component = _knowledgeBase["components"].FirstOrDefault(
                    c => c["name"].ToString().Equals(componentType, StringComparison.OrdinalIgnoreCase)
                );

                if (component != null)
                {
                    return JObject.FromObject(component);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Component information class
    /// </summary>
    public class ComponentInfo
    {
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string Id { get; set; }
        public Dictionary<string, object> Settings { get; set; }
    }

    /// <summary>
    /// Connection information class
    /// </summary>
    public class ConnectionInfo
    {
        public string SourceId { get; set; }
        public string SourceParam { get; set; }
        public string TargetId { get; set; }
        public string TargetParam { get; set; }
    }
}
