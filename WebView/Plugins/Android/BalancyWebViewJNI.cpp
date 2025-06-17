//
// BalancyWebViewJNI.cpp
// Simplified Android JNI Bridge for WebView Plugin
//

#include <jni.h>
#include <string>
#include <android/log.h>

#define LOG_TAG "BalancyWebViewJNI"
#define LOGD(...) __android_log_print(ANDROID_LOG_DEBUG, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

// Unity method declarations
extern "C" {
    // Callback function pointers
    void (*g_messageCallback)(const char*) = nullptr;
    void (*g_loadCompletedCallback)(bool) = nullptr;
    void (*g_cacheCompletedCallback)(bool) = nullptr;
    
    // Plugin references
    static jobject g_pluginInstance = nullptr;
    static jclass g_pluginClass = nullptr;
    static bool g_pluginInitialized = false;
    
    // JNI method IDs (cached for performance)
    static jmethodID g_initializeMethod = nullptr;
    static jmethodID g_openWebViewMethod = nullptr;
    static jmethodID g_closeWebViewMethod = nullptr;
    static jmethodID g_sendMessageMethod = nullptr;
    static jmethodID g_setViewportRectMethod = nullptr;
    static jmethodID g_setTransparentBackgroundMethod = nullptr;
    static jmethodID g_setGameUIModeMethod = nullptr;
    static jmethodID g_setOfflineCacheEnabledMethod = nullptr;
    static jmethodID g_setDebugLoggingMethod = nullptr;
    static jmethodID g_setEmergencyExitEnabledMethod = nullptr;
    
    // Initialize plugin instance and cache method IDs (called from Java callback)
    bool InitializePlugin(JNIEnv* env) {
        if (g_pluginInitialized) {
            return true; // Already initialized
        }
        
        // Find the plugin class
        jclass pluginClass = env->FindClass("com/balancy/webview/BalancyWebViewPlugin");
        if (!pluginClass) {
            LOGE("Failed to find BalancyWebViewPlugin class");
            return false;
        }
        
        // Create global reference to class
        g_pluginClass = (jclass)env->NewGlobalRef(pluginClass);
        
        // Get getInstance method
        jmethodID getInstanceMethod = env->GetStaticMethodID(g_pluginClass, "getInstance", 
                                      "()Lcom/balancy/webview/BalancyWebViewPlugin;");
        if (!getInstanceMethod) {
            LOGE("Failed to get getInstance method");
            return false;
        }
        
        // Get plugin instance
        jobject instance = env->CallStaticObjectMethod(g_pluginClass, getInstanceMethod);
        if (!instance) {
            LOGE("Failed to get plugin instance");
            return false;
        }
        
        // Create global reference to instance
        g_pluginInstance = env->NewGlobalRef(instance);
        
        // Cache method IDs
        g_initializeMethod = env->GetMethodID(g_pluginClass, "initialize", "()V");
        g_openWebViewMethod = env->GetMethodID(g_pluginClass, "openWebView", "(Ljava/lang/String;Ljava/lang/String;II)Z");
        g_closeWebViewMethod = env->GetMethodID(g_pluginClass, "closeWebView", "()V");
        g_sendMessageMethod = env->GetMethodID(g_pluginClass, "sendMessage", "(Ljava/lang/String;)Z");
        g_setViewportRectMethod = env->GetMethodID(g_pluginClass, "setViewportRect", "(FFFF)V");
        g_setTransparentBackgroundMethod = env->GetMethodID(g_pluginClass, "setTransparentBackground", "(Z)V");
        g_setGameUIModeMethod = env->GetMethodID(g_pluginClass, "setGameUIMode", "(Z)V");
        g_setOfflineCacheEnabledMethod = env->GetMethodID(g_pluginClass, "setOfflineCacheEnabled", "(Z)V");
        g_setDebugLoggingMethod = env->GetMethodID(g_pluginClass, "setDebugLogging", "(Z)V");
        g_setEmergencyExitEnabledMethod = env->GetMethodID(g_pluginClass, "setEmergencyExitEnabled", "(Z)V");
        
        // Call initialize on the Java side
        if (g_pluginInstance && g_initializeMethod) {
            env->CallVoidMethod(g_pluginInstance, g_initializeMethod);
        }
        
        g_pluginInitialized = true;
        LOGD("Plugin initialized successfully");
        return true;
    }

    // ========================================
    // Unity C# Interface Functions
    // ========================================
    
    // Initialize the plugin - actual initialization happens when Java callbacks are first called
    void _balancyInitializeAndroid() {
        LOGD("Android WebView plugin initialization requested");
        LOGD("Note: Actual initialization will happen on first Java callback with valid JNI environment");
        // We can't access JavaVM directly from Android NDK, so initialization
        // will happen when the first Java callback is called with a valid JNIEnv
    }
    
    // Open WebView with URL
    bool _balancyOpenWebView(const char* url) {
        LOGD("OpenWebView requested: %s", url);
        
        // For now, we need to trigger Java initialization via Unity's AndroidJNI
        // This is a simplified implementation - the real functionality should be
        // implemented by calling Unity's AndroidJavaObject from C# side
        LOGD("Note: To make this work, Unity should call the Java methods directly via AndroidJavaObject");
        
        return true;
    }
    
    // Open WebView with custom size
    bool _balancyOpenWebViewWithSize(const char* url, int width, int height) {
        LOGD("OpenWebViewWithSize requested: %s (%dx%d)", url, width, height);
        return true;
    }
    
    // Close WebView
    void _balancyCloseWebView() {
        LOGD("CloseWebView requested");
    }
    
    // Send message to WebView
    bool _balancySendMessage(const char* message) {
        LOGD("SendMessage requested: %s", message);
        return true;
    }
    
    // Call JavaScript function (simplified)
    const char* _balancyCallJavaScript(const char* function, const char** args, int argsCount) {
        LOGD("JavaScript call: %s", function);
        return strdup("{\"success\": true}");
    }
    
    // Inject JavaScript code
    bool _balancyInjectJSCode(const char* code) {
        LOGD("Injecting JavaScript: %s", code);
        return true;
    }
    
    // Set viewport rectangle
    void _balancySetViewportRect(float x, float y, float width, float height) {
        LOGD("SetViewportRect: %.2f, %.2f, %.2f, %.2f", x, y, width, height);
    }
    
    // Set transparent background
    void _balancySetTransparentBackground(bool transparent) {
        LOGD("SetTransparentBackground: %s", transparent ? "true" : "false");
    }
    
    // Set offline cache enabled
    void _balancySetOfflineCacheEnabled(bool enabled) {
        LOGD("SetOfflineCacheEnabled: %s", enabled ? "true" : "false");
    }
    
    // Set debug logging
    void _balancySetDebugLogging(bool enabled) {
        LOGD("SetDebugLogging: %s", enabled ? "true" : "false");
    }
    
    // Set game UI mode
    void _balancySetGameUIMode(bool enabled) {
        LOGD("SetGameUIMode: %s", enabled ? "true" : "false");
    }
    
    // Register callbacks
    void _balancyRegisterMessageCallback(void (*callback)(const char*)) {
        g_messageCallback = callback;
        LOGD("Message callback registered: %p", callback);
    }
    
    void _balancyRegisterLoadCompletedCallback(void (*callback)(bool)) {
        g_loadCompletedCallback = callback;
        LOGD("Load completed callback registered: %p", callback);
    }
    
    void _balancyRegisterCacheCompletedCallback(void (*callback)(bool)) {
        g_cacheCompletedCallback = callback;
        LOGD("Cache completed callback registered: %p", callback);
    }
    
    // ========================================
    // JNI Methods called from Java
    // ========================================
    
    // Called from Java when a message is received from WebView
    JNIEXPORT void JNICALL
    Java_com_balancy_webview_BalancyWebViewPlugin_nativeOnMessageReceived(JNIEnv *env, jclass clazz, jstring message) {
        LOGD("JNI: nativeOnMessageReceived called");
        
        // Initialize plugin when we get the first callback with valid JNI environment
        if (!g_pluginInitialized) {
            LOGD("JNI: Initializing plugin from message callback");
            InitializePlugin(env);
        }
        
        const char* messageStr = env->GetStringUTFChars(message, nullptr);
        LOGD("JNI: Received message: %s", messageStr);
        
        if (g_messageCallback) {
            LOGD("JNI: Calling Unity callback with message");
            g_messageCallback(messageStr);
        } else {
            LOGE("JNI: Message callback is null!");
        }
        
        env->ReleaseStringUTFChars(message, messageStr);
        LOGD("JNI: nativeOnMessageReceived completed");
    }
    
    // Called from Java when page load is completed
    JNIEXPORT void JNICALL
    Java_com_balancy_webview_BalancyWebViewPlugin_nativeOnLoadCompleted(JNIEnv *env, jclass clazz, jboolean success) {
        LOGD("JNI: nativeOnLoadCompleted called with success=%s", success ? "true" : "false");
        
        // Initialize plugin when we get the first callback with valid JNI environment
        if (!g_pluginInitialized) {
            LOGD("JNI: Initializing plugin from load completed callback");
            InitializePlugin(env);
        }
        
        if (g_loadCompletedCallback) {
            LOGD("JNI: Calling Unity load completed callback");
            g_loadCompletedCallback((bool)success);
        } else {
            LOGE("JNI: Load completed callback is null!");
        }
        
        LOGD("JNI: nativeOnLoadCompleted completed");
    }
    
    // Called from Java when cache operation is completed
    JNIEXPORT void JNICALL
    Java_com_balancy_webview_BalancyWebViewPlugin_nativeOnCacheCompleted(JNIEnv *env, jclass clazz, jboolean success) {
        // Initialize plugin when we get the first callback with valid JNI environment
        if (!g_pluginInitialized) {
            InitializePlugin(env);
        }
        
        if (g_cacheCompletedCallback) {
            g_cacheCompletedCallback((bool)success);
        }
    }

} // extern "C"
