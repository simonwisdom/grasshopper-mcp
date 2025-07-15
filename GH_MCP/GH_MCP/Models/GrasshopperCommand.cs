using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GrasshopperMCP.Models
{
    /// <summary>
    /// Represents a command sent from Python server to Grasshopper
    /// </summary>
    public class Command
    {
        /// <summary>
        /// Command type
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Command parameters
        /// </summary>
        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Create a new command instance
        /// </summary>
        /// <param name="type">Command type</param>
        /// <param name="parameters">Command parameters</param>
        public Command(string type, Dictionary<string, object> parameters = null)
        {
            Type = type;
            Parameters = parameters ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Get the value of the specified parameter
        /// </summary>
        /// <typeparam name="T">Parameter type</typeparam>
        /// <param name="name">Parameter name</param>
        /// <returns>Parameter value</returns>
        public T GetParameter<T>(string name)
        {
            if (Parameters.TryGetValue(name, out object value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                
                // Try conversion
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // If it's Newtonsoft.Json.Linq.JObject, try conversion
                    if (value is Newtonsoft.Json.Linq.JObject jObject)
                    {
                        return jObject.ToObject<T>();
                    }
                    
                    // If it's Newtonsoft.Json.Linq.JArray, try conversion
                    if (value is Newtonsoft.Json.Linq.JArray jArray)
                    {
                        return jArray.ToObject<T>();
                    }
                }
            }
            
            // If parameter cannot be retrieved or converted, return default value
            return default;
        }
    }

    /// <summary>
    /// Represents a response sent from Grasshopper to Python server
    /// </summary>
    public class Response
    {
        /// <summary>
        /// Whether the response is successful
        /// </summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>
        /// Response data
        /// </summary>
        [JsonProperty("data")]
        public object Data { get; set; }

        /// <summary>
        /// Error message, if any
        /// </summary>
        [JsonProperty("error")]
        public string Error { get; set; }

        /// <summary>
        /// Create a successful response
        /// </summary>
        /// <param name="data">Response data</param>
        /// <returns>Response instance</returns>
        public static Response Ok(object data = null)
        {
            return new Response
            {
                Success = true,
                Data = data
            };
        }

        /// <summary>
        /// Create an error response
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <returns>Response instance</returns>
        public static Response CreateError(string errorMessage)
        {
            return new Response
            {
                Success = false,
                Data = null,
                Error = errorMessage
            };
        }
    }
}
