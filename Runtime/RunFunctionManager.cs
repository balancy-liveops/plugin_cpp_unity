using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System.Globalization;

namespace Balancy
{
    public static class RunFunctionManager
    {
        private static readonly Dictionary<string, PendingCallback> _pendingCallbacks = new Dictionary<string, PendingCallback>();
        
        private struct PendingCallback
        {
            public string CallbackId;
            public DateTime Timestamp;
        }
        
        private static UnityMainThreadDispatcher _mainThreadDispatcher;
        private static bool _isInitialized;

        internal static void Init()
        {
            _pendingCallbacks.Clear();
            _mainThreadDispatcher = UnityMainThreadDispatcher.Instance();
            _isInitialized = true;
            LibraryMethods.General.balancySetRunFunctionCallback(OnRunFunctionRequested);
        }

        internal static void CleanUp()
        {
            _isInitialized = false;
            _pendingCallbacks.Clear();
            _mainThreadDispatcher = null;
            if (Controller.IsNativeInitialized)
                LibraryMethods.General.balancySetRunFunctionCallback(null);
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.RunFunctionCallback))]
        private static void OnRunFunctionRequested(string callbackDataJson, string responseCallbackId)
        {
            var dispatcher = _mainThreadDispatcher;
            if (!_isInitialized || dispatcher == null)
                return;

            // Marshal everything onto the Unity main thread so user code
            // invoked via InvokeStaticMethod always runs on the main thread.
            dispatcher.Enqueue(() =>
            {
                if (!_isInitialized)
                    return;

                try
                {
                    Debug.Log($"[RunFunctionManager] Received function call request: {callbackDataJson}");

                    // Parse the callback data using JsonUtility
                    var callbackData = JsonUtility.FromJson<RunFunctionCallbackData>(callbackDataJson);

                    if (callbackData == null || string.IsNullOrEmpty(callbackData.path))
                    {
                        Debug.LogError("[RunFunctionManager] Invalid callback data received");
                        SendErrorResponse(responseCallbackId, "Invalid callback data");
                        return;
                    }

                    // Store the pending callback
                    _pendingCallbacks[responseCallbackId] = new PendingCallback
                    {
                        CallbackId = responseCallbackId,
                        Timestamp = DateTime.UtcNow
                    };

                    // Parse the path (namespace.method)
                    var pathParts = callbackData.path.Split('.');
                    if (pathParts.Length < 2)
                    {
                        Debug.LogError($"[RunFunctionManager] Invalid path format: {callbackData.path}. Expected 'Namespace.Class.Method' or 'Class.Method'");
                        SendErrorResponse(responseCallbackId, "Invalid path format");
                        return;
                    }

                    // The last part is always the method name
                    string methodName = pathParts[pathParts.Length - 1];

                    // Everything before the last part is the type name (could include namespace)
                    string typeName = string.Join(".", pathParts, 0, pathParts.Length - 1);

                    Debug.Log($"[RunFunctionManager] Parsed path - typeName: '{typeName}', methodName: '{methodName}'");

                    // Convert parameters from JsonUtility format
                    Dictionary<string, object> parameters = ConvertParametersFromJson(callbackData.parameters);

                    // Invoke the method
                    InvokeStaticMethod(typeName, methodName, parameters, responseCallbackId);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[RunFunctionManager] Error processing function call: {ex.Message}");
                    SendErrorResponse(responseCallbackId, ex.Message);
                }
            });
        }
        
        private static Dictionary<string, object> ConvertParametersFromJson(ParameterData[] parameterArray)
        {
            var parameters = new Dictionary<string, object>();
            
            if (parameterArray != null)
            {
                foreach (var param in parameterArray)
                {
                    if (!string.IsNullOrEmpty(param.key))
                    {
                        // Convert string value to appropriate type based on valueType
                        object value = ConvertStringToType(param.value, param.valueType);
                        parameters[param.key] = value;
                    }
                }
            }
            
            return parameters;
        }
        
        private static object ConvertStringToType(string value, string valueType)
        {
            if (string.IsNullOrEmpty(value))
                return null;
                
            switch (valueType?.ToLower())
            {
                case "int":
                case "integer":
                    return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case "float":
                case "single":
                    return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                case "double":
                    return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                case "bool":
                case "boolean":
                    return bool.Parse(value);
                case "long":
                    return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case "ulong":
                    return ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case "string":
                default:
                    return value;
            }
        }

        private static string FormatInvariant(object value)
        {
            if (value == null)
                return "";
            if (value is double doubleValue)
                return doubleValue.ToString("R", CultureInfo.InvariantCulture);
            if (value is float floatValue)
                return floatValue.ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        
        private static void InvokeStaticMethod(string typeName, string methodName, Dictionary<string, object> parameters, string responseCallbackId)
        {
            try
            {
                // Find the type by name
                Type targetType = FindTypeByName(typeName);
                if (targetType == null)
                {
                    Debug.LogError($"[RunFunctionManager] Type not found: {typeName}");
                    SendErrorResponse(responseCallbackId, $"Type not found: {typeName}");
                    return;
                }
                
                // Find the static method
                MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
                if (method == null)
                {
                    Debug.LogError($"[RunFunctionManager] Static method not found: {typeName}.{methodName}");
                    SendErrorResponse(responseCallbackId, $"Static method not found: {typeName}.{methodName}");
                    return;
                }
                
                // Prepare method parameters
                ParameterInfo[] parameterInfos = method.GetParameters();
                object[] methodArgs = new object[parameterInfos.Length];
                
                for (int i = 0; i < parameterInfos.Length; i++)
                {
                    string paramName = parameterInfos[i].Name;
                    Type paramType = parameterInfos[i].ParameterType;
                    
                    if (parameters.ContainsKey(paramName))
                    {
                        // Convert parameter to the correct type
                        methodArgs[i] = ConvertParameter(parameters[paramName], paramType);
                    }
                    else if (parameterInfos[i].HasDefaultValue)
                    {
                        methodArgs[i] = parameterInfos[i].DefaultValue;
                    }
                    else
                    {
                        Debug.LogError($"[RunFunctionManager] Required parameter missing: {paramName}");
                        SendErrorResponse(responseCallbackId, $"Required parameter missing: {paramName}");
                        return;
                    }
                }
                
                // Invoke the method
                object result = method.Invoke(null, methodArgs);
                
                // Send success response
                SendSuccessResponse(responseCallbackId, result, method.ReturnType);
                
                Debug.Log($"[RunFunctionManager] Successfully invoked {typeName}.{methodName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RunFunctionManager] Error invoking method: {ex.Message}");
                SendErrorResponse(responseCallbackId, ex.Message);
            }
        }
        
        private static Type FindTypeByName(string typeName)
        {
            Debug.Log($"[RunFunctionManager] Searching for type: '{typeName}'");
            
            // First try to find the exact type name
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    Debug.Log($"[RunFunctionManager] Found exact type: {type.FullName} in assembly: {assembly.GetName().Name}");
                    return type;
                }
            }
            
            // If not found, try with common namespaces
            string[] commonNamespaces = { "Balancy", "UnityEngine", "System", "" };
            
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string ns in commonNamespaces)
                {
                    string fullTypeName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
                    Type type = assembly.GetType(fullTypeName);
                    if (type != null)
                    {
                        Debug.Log($"[RunFunctionManager] Found type with namespace: {type.FullName} in assembly: {assembly.GetName().Name}");
                        return type;
                    }
                }
            }
            
            // Last resort: search through all types in all assemblies
            Debug.Log($"[RunFunctionManager] Performing exhaustive search for type: '{typeName}'");
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (type.Name == typeName || type.FullName == typeName)
                        {
                            Debug.Log($"[RunFunctionManager] Found type via exhaustive search: {type.FullName} in assembly: {assembly.GetName().Name}");
                            return type;
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Some assemblies might not be fully loaded, skip them
                    continue;
                }
            }
            
            Debug.LogError($"[RunFunctionManager] Type '{typeName}' not found in any loaded assembly");
            return null;
        }
        
        private static object ConvertParameter(object value, Type targetType)
        {
            if (value == null)
                return null;
                
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;
                
            // Handle common type conversions
            if (targetType == typeof(string))
                return value.ToString();
            else if (targetType == typeof(int))
                return Convert.ToInt32(value);
            else if (targetType == typeof(float))
                return Convert.ToSingle(value);
            else if (targetType == typeof(double))
                return Convert.ToDouble(value);
            else if (targetType == typeof(bool))
                return Convert.ToBoolean(value);
            else if (targetType == typeof(long))
                return Convert.ToInt64(value);
            else if (targetType == typeof(ulong))
                return Convert.ToUInt64(value);
            
            // For complex types, try direct conversion
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                // Fallback to string representation
                return value.ToString();
            }
        }
        
        private static void SendSuccessResponse(string responseCallbackId, object result, Type returnType)
        {
            try
            {
                RunFunctionResponse response;

                if (returnType == typeof(NodeRunFunctionReturnType) && result is NodeRunFunctionReturnType nrfrt)
                {
                    // Full contract: user controls exit port and all named outputs
                    var outputList = new System.Collections.Generic.List<OutputData>();
                    if (nrfrt.Outputs != null)
                    {
                        foreach (var kvp in nrfrt.Outputs)
                        {
                            outputList.Add(new OutputData
                            {
                                key = kvp.Key,
                                value = FormatInvariant(kvp.Value),
                                valueType = GetTypeString(kvp.Value?.GetType() ?? typeof(string))
                            });
                        }
                    }
                    response = new RunFunctionResponse
                    {
                        exitPort = nrfrt.ExitPort,
                        outputs = outputList.ToArray()
                    };
                }
                else
                {
                    // Simple return value: always exits via "Success", result exposed as "result" port
                    response = new RunFunctionResponse
                    {
                        exitPort = "Success",
                        outputs = (returnType != typeof(void) && result != null)
                            ? new OutputData[]
                            {
                                new OutputData
                                {
                                    key = "result",
                                    value = FormatInvariant(result),
                                    valueType = GetTypeString(returnType)
                                }
                            }
                            : new OutputData[0]
                    };
                }

                string responseJson = JsonUtility.ToJson(response);
                LibraryMethods.General.balancyRunFunctionResponse(responseCallbackId, responseJson);
                _pendingCallbacks.Remove(responseCallbackId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RunFunctionManager] Error sending success response: {ex.Message}");
                SendErrorResponse(responseCallbackId, ex.Message);
            }
        }
        
        private static void SendErrorResponse(string responseCallbackId, string errorMessage)
        {
            try
            {
                var response = new RunFunctionResponse
                {
                    exitPort = "Error",
                    outputs = new OutputData[]
                    {
                        new OutputData
                        {
                            key = "error",
                            value = errorMessage,
                            valueType = "string"
                        }
                    }
                };
                
                string responseJson = JsonUtility.ToJson(response);
                LibraryMethods.General.balancyRunFunctionResponse(responseCallbackId, responseJson);
                
                // Remove from pending callbacks
                _pendingCallbacks.Remove(responseCallbackId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RunFunctionManager] Error sending error response: {ex.Message}");
            }
        }
        
        private static string GetTypeString(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(string)) return "string";
            return "string"; // Default to string for complex types
        }
        
        [System.Serializable]
        private class RunFunctionCallbackData
        {
            public string path;
            public ParameterData[] parameters;
        }
        
        [System.Serializable]
        private class ParameterData
        {
            public string key;
            public string value;
            public string valueType;
        }
        
        [System.Serializable]
        private class RunFunctionResponse
        {
            public string exitPort;
            public OutputData[] outputs;
        }
        
        [System.Serializable]
        private class OutputData
        {
            public string key;
            public string value;
            public string valueType;
        }
    }
}
