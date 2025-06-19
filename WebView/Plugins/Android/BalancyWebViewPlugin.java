package com.balancy.webview;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.ConsoleMessage;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;

import com.unity3d.player.UnityPlayer;

/**
 * Balancy WebView Plugin for Android
 * Provides WebView functionality for Unity applications
 */
public class BalancyWebViewPlugin {
    private static final String TAG = "BalancyWebView";
    
    // Singleton instance
    private static BalancyWebViewPlugin instance;
    
    // WebView and UI components
    private WebView webView;
    private FrameLayout webViewContainer;
    private Button emergencyExitButton;
    private Activity currentActivity;
    
    // Configuration
    private boolean isWebViewOpen = false;
    private boolean debugLogging = false;
    private boolean transparentBackground = true;
    private boolean gameUIMode = true;
    private boolean offlineCacheEnabled = false;
    private float viewportX = 0f;
    private float viewportY = 0f;
    private float viewportWidth = 1f;
    private float viewportHeight = 1f;
    private String ownerJson = "";
    
    // REMOVED: JNI callbacks - now using Unity messaging exclusively
    // private static native void nativeOnMessageReceived(String message);
    // private static native void nativeOnLoadCompleted(boolean success);
    // private static native void nativeOnCacheCompleted(boolean success);
    
    /**
     * Send message to Unity via UnitySendMessage
     * This replaces JNI callbacks for better stability
     */
    private void sendUnityMessage(String methodName, String message) {
        try {
            // Use Unity's UnitySendMessage to call methods on the BalancyWebView GameObject
            // The GameObject name should be "BalancyView" as defined in C#
            com.unity3d.player.UnityPlayer.UnitySendMessage("BalancyView", methodName, message);
            logDebug("Sent Unity message: " + methodName + " = " + message);
        } catch (Exception e) {
            Log.e(TAG, "Failed to send Unity message: " + methodName, e);
        }
    }
    
    static {
        try {
            System.loadLibrary("BalancyWebViewAndroid");
            Log.d(TAG, "BalancyWebViewAndroid library loaded successfully");
        } catch (UnsatisfiedLinkError e) {
            Log.e(TAG, "Failed to load BalancyWebViewAndroid library", e);
        }
    }
    
    /**
     * Get singleton instance
     */
    public static BalancyWebViewPlugin getInstance() {
        if (instance == null) {
            instance = new BalancyWebViewPlugin();
        }
        return instance;
    }
    
    /**
     * Initialize the plugin with Unity activity
     */
    public void initialize() {
        currentActivity = UnityPlayer.currentActivity;
        if (currentActivity == null) {
            Log.e(TAG, "Unity activity is null");
            return;
        }
        
        // Run on UI thread
        currentActivity.runOnUiThread(() -> {
            setupWebViewContainer();
        });
        
        logDebug("BalancyWebViewPlugin initialized");
    }
    
    /**
     * Setup the container for WebView overlay
     */
    private void setupWebViewContainer() {
        // Create container that will hold the WebView
        webViewContainer = new FrameLayout(currentActivity);
        webViewContainer.setLayoutParams(new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.MATCH_PARENT
        ));
        webViewContainer.setVisibility(View.GONE);
        
        // Add to Unity's view hierarchy
        ViewGroup unityView = (ViewGroup) currentActivity.findViewById(android.R.id.content);
        if (unityView != null) {
            unityView.addView(webViewContainer);
        }
    }
    
    /**
     * Open WebView with URL
     */
    public boolean openWebView(String url, String ownerJson, int width, int height) {
        if (isWebViewOpen) {
            Log.w(TAG, "WebView is already open");
            return false;
        }
        
        this.ownerJson = ownerJson;
        
        currentActivity.runOnUiThread(() -> {
            createWebView();
            setupEmergencyExitButton();
            applySettings();
            loadUrl(url);
            showWebView();
        });
        
        isWebViewOpen = true;
        logDebug("Opening WebView with URL: " + url);
        return true;
    }
    
    /**
     * Create and configure the WebView
     */
    private void createWebView() {
        webView = new WebView(currentActivity);
        
        // WebView settings
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        // Note: setAppCacheEnabled is deprecated and removed in newer Android versions
        // settings.setAppCacheEnabled(true); // REMOVED - deprecated
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        
        // Enable modern web features
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }
        
        // === GAME UI MODE NATIVE SETTINGS ===
        // These settings make the WebView feel more like a native game UI
        
        // Disable all zoom functionality
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        
        // Disable text zoom (helps maintain consistent UI)
        settings.setTextZoom(100);
        
        // Disable overscroll (bounce effect)
        webView.setOverScrollMode(View.OVER_SCROLL_NEVER);
        
        // Hide scroll bars
        webView.setVerticalScrollBarEnabled(false);
        webView.setHorizontalScrollBarEnabled(false);
        webView.setScrollBarStyle(View.SCROLLBARS_INSIDE_OVERLAY);
        
        // User agent
        settings.setUserAgentString(settings.getUserAgentString() + " BalancyWebView/1.0");
        
        // === ADDITIONAL GAME UI MODE SETTINGS ===
        
        // Disable long click context menu
        webView.setOnLongClickListener(new View.OnLongClickListener() {
            @Override
            public boolean onLongClick(View v) {
                return true; // Prevent context menu
            }
        });
        
        // Disable text selection on touch
        webView.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View v, android.view.MotionEvent event) {
                // Allow normal touch events but prevent text selection
                if (event.getAction() == android.view.MotionEvent.ACTION_DOWN || 
                    event.getAction() == android.view.MotionEvent.ACTION_UP) {
                    if (!v.hasFocus()) {
                        v.requestFocus();
                    }
                }
                return false; // Don't consume the event
            }
        });
        
        // JavaScript interface for Unity communication
        webView.addJavascriptInterface(new WebViewJavaScriptInterface(), "BalancyWebView");
        
        // WebView client for navigation events
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                logDebug("Page started loading: " + url);
                super.onPageStarted(view, url, favicon);
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                logDebug("Page finished loading: " + url);
                
                // Inject transparency CSS if needed
                if (transparentBackground) {
                    logDebug("Injecting transparency CSS");
                    injectTransparencyCSS();
                }
                
                // Inject game UI mode CSS if needed
                if (gameUIMode) {
                    logDebug("Injecting game UI mode CSS");
                    injectGameUIModeCSS();
                }
                
                // Inject Balancy bridge script
                logDebug("Injecting Balancy bridge script");
                injectBalancyBridge();
                
                // Inject owner JSON if available
                if (!ownerJson.isEmpty()) {
                    logDebug("Injecting owner JSON data");
                    injectOwnerJson();
                }
                
                logDebug("WebView page loading completed successfully");
                
                // FIXED: Use Unity messaging instead of JNI callback
                sendUnityMessage("OnAndroidLoadCompleted", "true");
            }
            
            @Override
            public void onReceivedError(WebView view, int errorCode, String description, String failingUrl) {
                Log.e(TAG, "WebView error: " + description + " (" + errorCode + ") for URL: " + failingUrl);
                
                if (failingUrl != null && failingUrl.startsWith("file://")) {
                    String filePath = failingUrl.substring(7);
                    java.io.File file = new java.io.File(filePath);
                    Log.e(TAG, "File debugging - exists: " + file.exists() + 
                               ", canRead: " + file.canRead() + 
                               ", isFile: " + file.isFile() + 
                               ", parent exists: " + (file.getParentFile() != null && file.getParentFile().exists()));
                }
                
                // FIXED: Use Unity messaging instead of JNI callback
                sendUnityMessage("OnAndroidLoadCompleted", "false");
            }
            
            @Override
            public void onReceivedHttpError(WebView view, WebResourceRequest request, 
                                          android.webkit.WebResourceResponse errorResponse) {
                Log.e(TAG, "HTTP error: " + errorResponse.getStatusCode() + " for " + request.getUrl());
                super.onReceivedHttpError(view, request, errorResponse);
            }
            
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                // Allow all navigation within the WebView
                return false;
            }
        });
        
        // Chrome client for console messages and debugging
        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onConsoleMessage(ConsoleMessage consoleMessage) {
                if (debugLogging) {
                    Log.d(TAG, "WebView Console: " + consoleMessage.message() + 
                         " -- From line " + consoleMessage.lineNumber() + 
                         " of " + consoleMessage.sourceId());
                }
                return true;
            }
            
            // Disable JavaScript alerts/confirms/prompts in game UI mode
            @Override
            public boolean onJsAlert(WebView view, String url, String message, android.webkit.JsResult result) {
                if (gameUIMode) {
                    result.confirm(); // Auto-dismiss alerts in game mode
                    return true;
                }
                return super.onJsAlert(view, url, message, result);
            }
            
            @Override
            public boolean onJsConfirm(WebView view, String url, String message, android.webkit.JsResult result) {
                if (gameUIMode) {
                    result.confirm(); // Auto-confirm in game mode
                    return true;
                }
                return super.onJsConfirm(view, url, message, result);
            }
            
            @Override
            public boolean onJsPrompt(WebView view, String url, String message, 
                                    String defaultValue, android.webkit.JsPromptResult result) {
                if (gameUIMode) {
                    result.confirm(""); // Auto-dismiss prompts in game mode
                    return true;
                }
                return super.onJsPrompt(view, url, message, defaultValue, result);
            }
        });
        
        // Set layout parameters
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.MATCH_PARENT
        );
        webView.setLayoutParams(params);
        
        // Add to container
        webViewContainer.addView(webView);
    }
    
    /**
     * Setup emergency exit button (invisible button in top-right corner)
     */
    private void setupEmergencyExitButton() {
        emergencyExitButton = new Button(currentActivity);
        
        // Make button invisible but still touchable
        emergencyExitButton.setBackgroundColor(Color.TRANSPARENT);
        emergencyExitButton.setAlpha(0.01f); // Barely visible
        emergencyExitButton.setText("");
        
        // Position in top-right corner (10% of screen size)
        int screenWidth = currentActivity.getResources().getDisplayMetrics().widthPixels;
        int screenHeight = currentActivity.getResources().getDisplayMetrics().heightPixels;
        int buttonSize = Math.min(screenWidth, screenHeight) / 10; // 10% of smaller dimension
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(buttonSize, buttonSize);
        params.gravity = android.view.Gravity.TOP | android.view.Gravity.END;
        emergencyExitButton.setLayoutParams(params);
        
        // Click handler for emergency exit
        emergencyExitButton.setOnClickListener(v -> {
            logDebug("Emergency exit button tapped");
            // FIXED: Use Unity messaging instead of JNI callback
            sendUnityMessage("OnAndroidMessageReceived", "//:balancy_close_view");
        });
        
        // Add to container
        webViewContainer.addView(emergencyExitButton);
    }
    
    /**
     * Apply current settings to WebView
     */
    private void applySettings() {
        if (webView == null) return;
        
        // Apply transparency
        if (transparentBackground) {
            webView.setBackgroundColor(Color.TRANSPARENT);
            webViewContainer.setBackgroundColor(Color.TRANSPARENT);
        } else {
            webView.setBackgroundColor(Color.WHITE);
            webViewContainer.setBackgroundColor(Color.WHITE);
        }
        
        // Apply viewport settings
        applyViewportSettings();
    }
    
    /**
     * Apply viewport settings (position and size)
     */
    private void applyViewportSettings() {
        if (webView == null) return;
        
        int screenWidth = currentActivity.getResources().getDisplayMetrics().widthPixels;
        int screenHeight = currentActivity.getResources().getDisplayMetrics().heightPixels;
        
        int x = (int) (viewportX * screenWidth);
        int y = (int) (viewportY * screenHeight);
        int width = (int) (viewportWidth * screenWidth);
        int height = (int) (viewportHeight * screenHeight);
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(width, height);
        params.leftMargin = x;
        params.topMargin = y;
        webView.setLayoutParams(params);
        
        logDebug("Viewport applied: x=" + x + ", y=" + y + ", w=" + width + ", h=" + height);
    }
    
    /**
     * Load URL in WebView with validation
     */
    private void loadUrl(String url) {
        if (webView == null) {
            Log.e(TAG, "Cannot load URL: WebView is null");
            return;
        }
        
        logDebug("Attempting to load URL: " + url);
        
        // Special handling for file:// URLs
        if (url.startsWith("file://")) {
            String filePath = url.substring(7); // Remove "file://" prefix
            logDebug("Detected local file URL. File path: " + filePath);
            
            // Check if file exists
            java.io.File file = new java.io.File(filePath);
            if (!file.exists()) {
                Log.e(TAG, "File does not exist: " + filePath);
                sendUnityMessage("OnAndroidLoadCompleted", "false");
                return;
            }
            
            if (!file.canRead()) {
                Log.e(TAG, "File cannot be read (permission denied): " + filePath);
                sendUnityMessage("OnAndroidLoadCompleted", "false");
                return;
            }
            
            logDebug("File validation passed. File size: " + file.length() + " bytes");
        }
        
        try {
            webView.loadUrl(url);
            logDebug("URL loading initiated successfully");
        } catch (Exception e) {
            Log.e(TAG, "Exception while loading URL: " + url, e);
            sendUnityMessage("OnAndroidLoadCompleted", "false");
        }
    }
    
    /**
     * Show the WebView overlay
     */
    private void showWebView() {
        if (webViewContainer != null) {
            webViewContainer.setVisibility(View.VISIBLE);
            webViewContainer.bringToFront();
        }
    }
    
    /**
     * Close the WebView
     */
    public void closeWebView() {
        if (!isWebViewOpen) return;
        
        currentActivity.runOnUiThread(() -> {
            if (webViewContainer != null) {
                webViewContainer.setVisibility(View.GONE);
                webViewContainer.removeAllViews();
            }
            
            if (webView != null) {
                webView.destroy();
                webView = null;
            }
            
            emergencyExitButton = null;
        });
        
        isWebViewOpen = false;
        logDebug("WebView closed");
    }
    
    /**
     * Send message to WebView
     */
    public boolean sendMessage(String message) {
        if (webView == null || !isWebViewOpen) {
            Log.w(TAG, "Cannot send message: WebView not open");
            return false;
        }
        
        // For Android, we need to call the JavaScript function directly
        // This matches the iOS implementation where Unity calls JS functions
        String script = "if (balancy && balancy._receiveMessageFromUnity) { " +
                       "balancy._receiveMessageFromUnity('" + message.replace("'", "\\'") + "'); }";
        
        currentActivity.runOnUiThread(() -> {
            webView.evaluateJavascript(script, result -> {
                logDebug("Message sent to WebView, JS result: " + (result != null ? result : "null"));
            });
        });
        
        logDebug("Message sent to WebView: " + message.substring(0, Math.min(100, message.length())) + "...");
        return true;
    }
    
    /**
     * Inject JavaScript code into the WebView
     * Called from Unity via AndroidJavaObject
     */
    public void injectJavaScript(String jsCode) {
        if (webView == null || !isWebViewOpen) {
            Log.w(TAG, "Cannot inject JavaScript: WebView not open");
            return;
        }
        
        logDebug("Injecting JavaScript: " + jsCode.substring(0, Math.min(100, jsCode.length())) + "...");
        
        currentActivity.runOnUiThread(() -> {
            webView.evaluateJavascript(jsCode, result -> {
                logDebug("JavaScript injection completed: " + (result != null ? result : "null"));
            });
        });
    }
    
    /**
     * Call JavaScript function
     */
    public void callJavaScript(String function, String[] args, JavaScriptCallback callback) {
        if (webView == null || !isWebViewOpen) {
            if (callback != null) {
                callback.onResult("{\"error\": \"WebView not open\"}");
            }
            return;
        }
        
        StringBuilder script = new StringBuilder();
        
        if ("eval".equals(function) && args.length > 0) {
            script.append(args[0]);
        } else {
            script.append(function).append("(");
            for (int i = 0; i < args.length; i++) {
                script.append("\"").append(args[i].replace("\"", "\\\"")).append("\"");
                if (i < args.length - 1) {
                    script.append(", ");
                }
            }
            script.append(")");
        }
        
        currentActivity.runOnUiThread(() -> {
            webView.evaluateJavascript(script.toString(), result -> {
                if (callback != null) {
                    callback.onResult(result != null ? result : "null");
                }
            });
        });
    }
    
    /**
     * Inject CSS for transparent background
     */
    private void injectTransparencyCSS() {
        String css = "document.body.style.backgroundColor = 'transparent';" +
                    "document.documentElement.style.backgroundColor = 'transparent';" +
                    "var style = document.createElement('style');" +
                    "style.innerHTML = 'body, html { background-color: transparent !important; }';" +
                    "document.head.appendChild(style);";
        
        webView.evaluateJavascript(css, null);
    }
    
    /**
     * Inject CSS for game UI mode
     */
    private void injectGameUIModeCSS() {
        String css = "var style = document.createElement('style');" +
                    "style.innerHTML = `" +
                    "* { -webkit-user-select: none !important; user-select: none !important; " +
                    "-webkit-touch-callout: none !important; -webkit-tap-highlight-color: transparent !important; }" +
                    "::-webkit-scrollbar { width: 0px !important; height: 0px !important; }" +
                    "* { cursor: default !important; }" +
                    "*:focus { outline: none !important; }" +
                    "`;" +
                    "document.head.appendChild(style);" +
                    "document.addEventListener('contextmenu', function(e) { e.preventDefault(); return false; });" +
                    "document.addEventListener('selectstart', function(e) { e.preventDefault(); return false; });";
        
        webView.evaluateJavascript(css, null);
    }
    
    /**
     * Inject Balancy bridge JavaScript
     */
    private void injectBalancyBridge() {
        // This would typically load from Resources, but for now we'll inject a minimal bridge
        String bridge = "window.balancy = window.balancy || {};" +
                       "balancy._receiveMessageFromUnity = function(message) {" +
                       "  if (balancy.onMessage) balancy.onMessage(message);" +
                       "};" +
                       "balancy.sendMessageToUnity = function(message) {" +
                       "  BalancyWebView.sendMessageToUnity(message);" +
                       "};";
        
        webView.evaluateJavascript(bridge, null);
    }
    
    /**
     * Inject owner JSON data
     */
    private void injectOwnerJson() {
        String script = "try {" +
                       "  balancy.owner = JSON.parse('" + ownerJson.replace("'", "\\'") + "');" +
                       "} catch (error) {" +
                       "  console.error('Error parsing owner JSON:', error);" +
                       "  balancy.owner = null;" +
                       "}";
        
        webView.evaluateJavascript(script, null);
    }
    
    /**
     * Configuration methods
     */
    public void setViewportRect(float x, float y, float width, float height) {
        this.viewportX = Math.max(0f, Math.min(1f, x));
        this.viewportY = Math.max(0f, Math.min(1f, y));
        this.viewportWidth = Math.max(0f, Math.min(1f, width));
        this.viewportHeight = Math.max(0f, Math.min(1f, height));
        
        if (isWebViewOpen) {
            currentActivity.runOnUiThread(this::applyViewportSettings);
        }
    }
    
    public void setTransparentBackground(boolean transparent) {
        this.transparentBackground = transparent;
        
        if (isWebViewOpen && webView != null) {
            currentActivity.runOnUiThread(() -> {
                if (transparent) {
                    webView.setBackgroundColor(Color.TRANSPARENT);
                    webViewContainer.setBackgroundColor(Color.TRANSPARENT);
                    injectTransparencyCSS();
                } else {
                    webView.setBackgroundColor(Color.WHITE);
                    webViewContainer.setBackgroundColor(Color.WHITE);
                }
            });
        }
    }
    
    public void setGameUIMode(boolean enabled) {
        this.gameUIMode = enabled;
        
        if (isWebViewOpen && webView != null) {
            currentActivity.runOnUiThread(() -> {
                if (enabled) {
                    // === COMPREHENSIVE GAME UI MODE SETTINGS ===
                    
                    // Disable all scroll behavior
                    webView.setOverScrollMode(View.OVER_SCROLL_NEVER);
                    webView.setVerticalScrollBarEnabled(false);
                    webView.setHorizontalScrollBarEnabled(false);
                    webView.setScrollBarStyle(View.SCROLLBARS_INSIDE_OVERLAY);
                    
                    // Disable zoom
                    WebSettings settings = webView.getSettings();
                    settings.setBuiltInZoomControls(false);
                    settings.setDisplayZoomControls(false);
                    settings.setSupportZoom(false);
                    settings.setTextZoom(100); // Fixed text size
                    
                    // Inject CSS for game UI feel
                    injectGameUIModeCSS();
                } else {
                    // Re-enable standard web features
                    webView.setOverScrollMode(View.OVER_SCROLL_IF_CONTENT_SCROLLS);
                    webView.setVerticalScrollBarEnabled(true);
                    webView.setHorizontalScrollBarEnabled(true);
                    
                    WebSettings settings = webView.getSettings();
                    settings.setBuiltInZoomControls(true);
                    settings.setDisplayZoomControls(false); // Keep controls hidden
                    settings.setSupportZoom(true);
                    
                    // Note: To fully disable game UI mode CSS, page would need to be reloaded
                }
            });
        }
        
        logDebug("Game UI mode " + (enabled ? "enabled" : "disabled"));
    }
    
    public void setOfflineCacheEnabled(boolean enabled) {
        this.offlineCacheEnabled = enabled;
        logDebug("Offline cache " + (enabled ? "enabled" : "disabled"));
        // TODO: Implement offline caching if needed
    }
    
    public void setDebugLogging(boolean enabled) {
        this.debugLogging = enabled;
        if (enabled) {
            logDebug("Debug logging enabled");
        }
    }
    
    public void setEmergencyExitEnabled(boolean enabled) {
        if (emergencyExitButton != null) {
            currentActivity.runOnUiThread(() -> {
                emergencyExitButton.setVisibility(enabled ? View.VISIBLE : View.GONE);
            });
        }
        logDebug("Emergency exit " + (enabled ? "enabled" : "disabled"));
    }
    
    /**
     * Utility methods
     */
    private void logDebug(String message) {
        if (debugLogging) {
            Log.d(TAG, message);
        }
    }
    
    public boolean isWebViewOpen() {
        return isWebViewOpen;
    }
    
    /**
     * JavaScript interface for WebView communication
     */
    private class WebViewJavaScriptInterface {
        @JavascriptInterface
        public void sendMessageToUnity(String message) {
            logDebug("Received message from WebView: " + message.substring(0, Math.min(100, message.length())) + "...");
            
            // FIXED: Use Unity messaging instead of JNI callback
            sendUnityMessage("OnAndroidMessageReceived", message);
        }
        
        @JavascriptInterface
        public void log(String level, String message) {
            // Allow WebView to log to Android console for debugging
            switch (level.toLowerCase()) {
                case "error":
                    Log.e(TAG, "WebView: " + message);
                    break;
                case "warn":
                    Log.w(TAG, "WebView: " + message);
                    break;
                case "info":
                case "log":
                default:
                    Log.i(TAG, "WebView: " + message);
                    break;
            }
        }
    }
    
    /**
     * Callback interface for JavaScript execution results
     */
    public interface JavaScriptCallback {
        void onResult(String result);
    }
}
