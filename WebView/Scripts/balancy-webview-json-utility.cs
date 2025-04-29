using System;
using System.Collections.Generic;
using UnityEngine;

namespace Balancy.WebView.Utilities
{
    /// <summary>
    /// Utility class for handling JSON data in WebView communication
    /// </summary>
    public static class WebViewJsonUtility
    {
        /// <summary>
        /// Creates a simple JSON message with action and data
        /// </summary>
        /// <param name="action">The action name</param>
        /// <param name="data">The data object (will be serialized to JSON)</param>
        /// <returns>JSON string</returns>
        public static string CreateMessage(string action, object data)
        {
            // Create a dynamic message object
            var message = new WebViewMessage
            {
                action = action,
                data = data
            };
            
            // Serialize to JSON
            return JsonUtility.ToJson(message);
        }
        
        /// <summary>
        /// Creates a simple JSON message with action only
        /// </summary>
        /// <param name="action">The action name</param>
        /// <returns>JSON string</returns>
        public static string CreateMessage(string action)
        {
            return $"{{\"action\":\"{action}\",\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
        }
        
        /// <summary>
        /// Tries to parse a WebView message and extract the action
        /// </summary>
        /// <param name="jsonMessage">The JSON message</param>
        /// <param name="action">Output action if successful</param>
        /// <returns>True if parsing was successful</returns>
        public static bool TryGetAction(string jsonMessage, out string action)
        {
            action = null;
            
            try
            {
                // Quick check if it's JSON
                if (!jsonMessage.StartsWith("{") || !jsonMessage.EndsWith("}"))
                {
                    return false;
                }
                
                // Parse the action using JsonUtility
                WebViewActionOnly actionObj = JsonUtility.FromJson<WebViewActionOnly>(jsonMessage);
                
                if (!string.IsNullOrEmpty(actionObj.action))
                {
                    action = actionObj.action;
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Tries to parse a WebView message and extract data of a specific type
        /// </summary>
        /// <typeparam name="T">The type of data to extract</typeparam>
        /// <param name="jsonMessage">The JSON message</param>
        /// <param name="data">Output data if successful</param>
        /// <returns>True if parsing was successful</returns>
        public static bool TryGetData<T>(string jsonMessage, out T data)
        {
            data = default;
            
            try
            {
                // Quick check if it's JSON
                if (!jsonMessage.StartsWith("{") || !jsonMessage.EndsWith("}"))
                {
                    return false;
                }
                
                // Parse using JsonUtility
                WebViewMessageGeneric<T> message = JsonUtility.FromJson<WebViewMessageGeneric<T>>(jsonMessage);
                
                if (message.data != null)
                {
                    data = message.data;
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        /// <returns>JSON string containing the error</returns>
        public static string CreateErrorResponse(string errorMessage)
        {
            return $"{{\"error\":\"{errorMessage}\"}}";
        }
        
        /// <summary>
        /// Creates a success response with data
        /// </summary>
        /// <param name="data">The data object (will be serialized to JSON)</param>
        /// <returns>JSON string containing the success result</returns>
        public static string CreateSuccessResponse(object data)
        {
            return $"{{\"success\":true,\"data\":{JsonUtility.ToJson(data)}}}";
        }
        
        /// <summary>
        /// Creates a simple success response
        /// </summary>
        /// <returns>JSON string indicating success</returns>
        public static string CreateSuccessResponse()
        {
            return "{\"success\":true}";
        }
        
        // Helper classes for serialization/deserialization
        
        [Serializable]
        private class WebViewMessage
        {
            public string action;
            public object data;
            public long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        
        [Serializable]
        private class WebViewActionOnly
        {
            public string action;
        }
        
        [Serializable]
        private class WebViewMessageGeneric<T>
        {
            public string action;
            public T data;
        }
    }
}
