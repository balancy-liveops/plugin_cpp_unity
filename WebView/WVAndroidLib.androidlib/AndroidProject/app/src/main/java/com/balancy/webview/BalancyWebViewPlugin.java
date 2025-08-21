package com.balancy.webview;

import android.animation.Animator;
import android.animation.AnimatorListenerAdapter;
import android.app.Activity;
import android.content.Context;
import android.graphics.Color;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.View;
import android.view.ViewGroup;
import android.view.animation.DecelerateInterpolator;
import android.webkit.ConsoleMessage;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;

/**
 * Balancy WebView Plugin for Android
 * Thread-safe version with proper UI thread handling
 */
public class BalancyWebViewPlugin {
    private static final String TAG = "BalancyWebView";
    
    private static BalancyWebViewPlugin instance;
    private WebView webView;
    private FrameLayout webViewContainer;
    private Button emergencyExitButton;
    private Activity currentActivity;
    
    private boolean isWebViewOpen = false;
    private boolean debugLogging = true; // Enable debug by default for testing
    private boolean transparentBackground = true;
    private boolean gameUIMode = true;
    private float viewportX = 0f;
    private float viewportY = 0f;
    private float viewportWidth = 1f;
    private float viewportHeight = 1f;
    private String ownerJson = "";
    
    private float showDelay = 0.1f;
    private float animationDuration = 0.1f;
    private boolean unityAvailable = false;
    
    /**
     * Ensure code runs on UI thread - CRITICAL for Android UI operations
     */
    private void runOnUIThread(Runnable action) {
        if (currentActivity != null) {
            if (Looper.myLooper() == Looper.getMainLooper()) {
                // Already on UI thread
                action.run();
            } else {
                // Switch to UI thread
                currentActivity.runOnUiThread(action);
            }
        } else {
            logDebug("Cannot run on UI thread: no activity available");
        }
    }
    
    private void sendUnityMessage(String methodName, String message) {
        if (!unityAvailable) {
            logDebug("Unity not available, message not sent: " + methodName);
            return;
        }
        
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            java.lang.reflect.Method sendMessageMethod = unityPlayerClass.getMethod(
                "UnitySendMessage", String.class, String.class, String.class
            );
            
            sendMessageMethod.invoke(null, "BalancyView", methodName, message);
            logDebug("Sent Unity message: " + methodName + " = " + message);
        } catch (Exception e) {
            Log.e(TAG, "Failed to send Unity message: " + methodName, e);
            unityAvailable = false;
        }
    }
    
    private void checkUnityAvailability() {
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            java.lang.reflect.Field currentActivityField = unityPlayerClass.getField("currentActivity");
            Activity unityActivity = (Activity) currentActivityField.get(null);
            
            if (unityActivity != null) {
                unityAvailable = true;
                currentActivity = unityActivity;
                logDebug("Unity is available and running");
            } else {
                unityAvailable = false;
                logDebug("Unity class found but not running");
            }
        } catch (Exception e) {
            unityAvailable = false;
            logDebug("Unity not available: " + e.getMessage());
        }
    }
    
    static {
        try {
            System.loadLibrary("BalancyWebViewAndroid");
            Log.d(TAG, "BalancyWebViewAndroid library loaded successfully");
        } catch (UnsatisfiedLinkError e) {
            Log.w(TAG, "BalancyWebViewAndroid library not found - using standalone mode");
        }
    }
    
    public static BalancyWebViewPlugin getInstance() {
        if (instance == null) {
            instance = new BalancyWebViewPlugin();
        }
        return instance;
    }
    
    public void initialize() {
        checkUnityAvailability();
        
        if (currentActivity == null) {
            Log.e(TAG, "No activity available for initialization");
            return;
        }
        
        runOnUIThread(() -> {
            setupWebViewContainer();
        });
        
        logDebug("BalancyWebViewPlugin initialized " + 
                (unityAvailable ? "with Unity" : "in standalone mode"));
    }
    
    public void initialize(Activity activity) {
        this.currentActivity = activity;
        this.unityAvailable = false;
        
        if (currentActivity == null) {
            Log.e(TAG, "Activity is null");
            return;
        }
        
        runOnUIThread(() -> {
            setupWebViewContainer();
        });
        
        logDebug("BalancyWebViewPlugin initialized in standalone mode with custom activity");
    }
    
    private void setupWebViewContainer() {
        webViewContainer = new FrameLayout(currentActivity);
        webViewContainer.setLayoutParams(new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.MATCH_PARENT
        ));
        webViewContainer.setVisibility(View.GONE);
        
        ViewGroup rootView = (ViewGroup) currentActivity.findViewById(android.R.id.content);
        if (rootView != null) {
            rootView.addView(webViewContainer);
        }
    }
    
    public boolean openWebView(String url, String ownerJson, int width, int height) {
        return openWebView(url, ownerJson, width, height, false);
    }
    
    public boolean openWebView(String url, String ownerJson, int width, int height, boolean startHidden) {
        if (isWebViewOpen) {
            Log.w(TAG, "WebView is already open");
            return false;
        }
        
        if (currentActivity == null) {
            Log.e(TAG, "Cannot open WebView: no activity available");
            return false;
        }
        
        this.ownerJson = ownerJson;
        
        runOnUIThread(() -> {
            createWebView();
            setupEmergencyExitButton();
            applySettings();
            loadUrl(url);
            
            if (!startHidden) {
                showWebViewInternal();
            } else {
                logDebug("WebView created but hidden");
            }
        });
        
        isWebViewOpen = true;
        logDebug("Opening WebView with URL: " + url + ", startHidden: " + startHidden);
        return true;
    }
    
    private void createWebView() {
        webView = new WebView(currentActivity);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            webView.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        }
        
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setRenderPriority(WebSettings.RenderPriority.HIGH);
        settings.setCacheMode(WebSettings.LOAD_CACHE_ELSE_NETWORK);
        settings.setLoadsImagesAutomatically(true);
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }
        
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setSupportZoom(false);
        settings.setTextZoom(100);
        
        webView.setOverScrollMode(View.OVER_SCROLL_NEVER);
        webView.setVerticalScrollBarEnabled(false);
        webView.setHorizontalScrollBarEnabled(false);
        webView.setScrollBarStyle(View.SCROLLBARS_INSIDE_OVERLAY);
        
        webView.setDrawingCacheEnabled(true);
        webView.setDrawingCacheQuality(View.DRAWING_CACHE_QUALITY_HIGH);
        
        settings.setUserAgentString(settings.getUserAgentString() + " BalancyWebView/1.0");
        
        webView.setOnLongClickListener(v -> true);
        webView.setOnTouchListener((v, event) -> {
            if (event.getAction() == android.view.MotionEvent.ACTION_DOWN || 
                event.getAction() == android.view.MotionEvent.ACTION_UP) {
                if (!v.hasFocus()) {
                    v.requestFocus();
                }
            }
            return false;
        });
        
        webView.addJavascriptInterface(new WebViewJavaScriptInterface(), "BalancyWebView");
        
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                logDebug("Page started loading: " + url);
                super.onPageStarted(view, url, favicon);
            }
            
            @Override
            public void onPageFinished(WebView view, String url) {
                logDebug("Page finished loading: " + url);
                
                injectRenderingOptimizations();
                
                if (transparentBackground) {
                    injectTransparencyCSS();
                }
                
                if (gameUIMode) {
                    injectGameUIModeCSS();
                }
                
                injectBalancyBridge();
                
                if (!ownerJson.isEmpty()) {
                    injectOwnerJson();
                }
                
                logDebug("WebView page loading completed");
                startShowAnimation();
                sendUnityMessage("OnAndroidLoadCompleted", "true");
            }
            
            @Override
            public void onReceivedError(WebView view, int errorCode, String description, String failingUrl) {
                Log.e(TAG, "WebView error: " + description + " (" + errorCode + ") for URL: " + failingUrl);
                sendUnityMessage("OnAndroidLoadCompleted", "false");
            }
            
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                return false;
            }
        });
        
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
            
            @Override
            public boolean onJsAlert(WebView view, String url, String message, android.webkit.JsResult result) {
                if (gameUIMode) {
                    result.confirm();
                    return true;
                }
                return super.onJsAlert(view, url, message, result);
            }
        });
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.MATCH_PARENT
        );
        webView.setLayoutParams(params);
        webView.setAlpha(0.0f);
        webViewContainer.addView(webView);
        
        logDebug("WebView created with optimizations");
    }
    
    private void setupEmergencyExitButton() {
        emergencyExitButton = new Button(currentActivity);
        emergencyExitButton.setBackgroundColor(Color.TRANSPARENT);
        emergencyExitButton.setAlpha(0.01f);
        emergencyExitButton.setText("");
        
        int screenWidth = currentActivity.getResources().getDisplayMetrics().widthPixels;
        int screenHeight = currentActivity.getResources().getDisplayMetrics().heightPixels;
        int buttonSize = Math.min(screenWidth, screenHeight) / 10;
        
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(buttonSize, buttonSize);
        params.gravity = android.view.Gravity.TOP | android.view.Gravity.END;
        emergencyExitButton.setLayoutParams(params);
        
        emergencyExitButton.setOnClickListener(v -> {
            logDebug("Emergency exit button tapped");
            sendUnityMessage("OnAndroidMessageReceived", "{\"action\":200, \"params\":{}}");
        });
        
        webViewContainer.addView(emergencyExitButton);
    }
    
    private void startShowAnimation() {
        if (webView == null) return;
        
        logDebug("Starting show animation");
        webView.setAlpha(0.0f);
        
        Handler mainHandler = new Handler(Looper.getMainLooper());
        mainHandler.postDelayed(() -> {
            if (webView != null) {
                webView.animate()
                    .alpha(1.0f)
                    .setDuration((long)(animationDuration * 1000))
                    .setInterpolator(new DecelerateInterpolator())
                    .start();
            }
        }, (long)(showDelay * 1000));
    }
    
    // Private method for internal use (already on UI thread)
    private void showWebViewInternal() {
        if (webViewContainer != null) {
            webViewContainer.setVisibility(View.VISIBLE);
            webViewContainer.bringToFront();
        }
    }
    
    // ✅ THREAD-SAFE PUBLIC METHODS - These can be called from Unity thread
    public void showWebView() {
        logDebug("showWebView() called from thread: " + Thread.currentThread().getName());
        runOnUIThread(() -> {
            if (webViewContainer != null) {
                webViewContainer.setVisibility(View.VISIBLE);
                webViewContainer.bringToFront();
                logDebug("WebView shown on UI thread");
            } else {
                logDebug("WebView container is null");
            }
        });
    }
    
    public void hideWebView() {
        logDebug("hideWebView() called from thread: " + Thread.currentThread().getName());
        runOnUIThread(() -> {
            if (webViewContainer != null) {
                webViewContainer.setVisibility(View.INVISIBLE);
                logDebug("WebView hidden on UI thread");
            } else {
                logDebug("WebView container is null");
            }
        });
    }
    
    public void closeWebView() {
        if (!isWebViewOpen) return;
        
        logDebug("closeWebView() called from thread: " + Thread.currentThread().getName());
        runOnUIThread(() -> {
            if (webViewContainer != null) {
                webViewContainer.setVisibility(View.GONE);
                webViewContainer.removeAllViews();
            }
            
            if (webView != null) {
                webView.destroy();
                webView = null;
            }
            
            emergencyExitButton = null;
            logDebug("WebView closed on UI thread");
        });
        
        isWebViewOpen = false;
    }
    
    private void loadUrl(String url) {
        if (webView == null) {
            Log.e(TAG, "Cannot load URL: WebView is null");
            return;
        }
        
        logDebug("Loading URL: " + url);
        
        if (url.startsWith("file://")) {
            String filePath = url.substring(7);
            java.io.File file = new java.io.File(filePath);
            if (!file.exists()) {
                Log.e(TAG, "File does not exist: " + filePath);
                sendUnityMessage("OnAndroidLoadCompleted", "false");
                return;
            }
        }
        
        try {
            webView.loadUrl(url);
            logDebug("URL loading initiated");
        } catch (Exception e) {
            Log.e(TAG, "Exception while loading URL: " + url, e);
            sendUnityMessage("OnAndroidLoadCompleted", "false");
        }
    }
    
    public boolean sendMessage(String message) {
        if (webView == null || !isWebViewOpen) {
            Log.w(TAG, "Cannot send message: WebView not open");
            return false;
        }
        
        String script = "if (balancy && balancy._receiveMessageFromUnity) { " +
                       "balancy._receiveMessageFromUnity('" + message.replace("'", "\\'").replace("\"", "\\\"") + "'); }";
        
        runOnUIThread(() -> {
            if (webView != null) {
                webView.evaluateJavascript(script, result -> {
                    logDebug("Message sent to WebView");
                });
            }
        });
        
        return true;
    }
    
    public void injectJavaScript(String jsCode) {
        if (webView == null || !isWebViewOpen) {
            Log.w(TAG, "Cannot inject JavaScript: WebView not open");
            return;
        }
        
        runOnUIThread(() -> {
            if (webView != null) {
                webView.evaluateJavascript(jsCode, null);
            }
        });
    }
    
    private void injectTransparencyCSS() {
        String css = "document.body.style.backgroundColor = 'transparent';" +
                    "document.documentElement.style.backgroundColor = 'transparent';";
        webView.evaluateJavascript(css, null);
    }
    
    private void injectGameUIModeCSS() {
        String css = "var style = document.createElement('style');" +
                    "style.innerHTML = '" +
                    "* { -webkit-user-select: none !important; user-select: none !important; " +
                    "-webkit-touch-callout: none !important; -webkit-tap-highlight-color: transparent !important; }" +
                    "::-webkit-scrollbar { width: 0px !important; }" +
                    "';" +
                    "document.head.appendChild(style);";
        webView.evaluateJavascript(css, null);
    }
    
    private void injectBalancyBridge() {
        String bridge = "window.balancy = window.balancy || {};" +
                       "balancy._receiveMessageFromUnity = function(message) {" +
                       "  if (balancy.onMessage) balancy.onMessage(message);" +
                       "};" +
                       "balancy.sendMessageToUnity = function(message) {" +
                       "  BalancyWebView.sendMessageToUnity(message);" +
                       "};";
        webView.evaluateJavascript(bridge, null);
    }
    
    private void injectOwnerJson() {
        String script = "try {" +
                       "  balancy.owner = JSON.parse('" + ownerJson.replace("'", "\\'") + "');" +
                       "} catch (error) {" +
                       "  console.error('Error parsing owner JSON:', error);" +
                       "}";
        webView.evaluateJavascript(script, null);
    }
    
    private void injectRenderingOptimizations() {
        String css = "var style = document.createElement('style');" +
                "style.innerHTML = '" +
                "body, html { transform: translateZ(0) !important; backface-visibility: hidden !important; } " +
                "img, canvas, video { transform: translateZ(0) !important; } " +
                "* { text-shadow: none !important; filter: none !important; box-shadow: none !important; } " +
                "';" +
                "document.head.appendChild(style);";
        webView.evaluateJavascript(css, null);
    }
    
    private void applySettings() {
        if (webView == null) return;
        
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.HONEYCOMB) {
            webView.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        }
        
        if (transparentBackground) {
            webView.setBackgroundColor(Color.TRANSPARENT);
            webViewContainer.setBackgroundColor(Color.TRANSPARENT);
        } else {
            webView.setBackgroundColor(Color.WHITE);
            webViewContainer.setBackgroundColor(Color.WHITE);
        }
    }
    
    public void setDebugLogging(boolean enabled) {
        this.debugLogging = enabled;
        if (enabled) {
            logDebug("Debug logging enabled");
        }
    }
    
    private void logDebug(String message) {
        if (debugLogging) {
            Log.d(TAG, message);
        }
    }
    
    public boolean isWebViewOpen() {
        return isWebViewOpen;
    }
    
    public boolean isUnityAvailable() {
        return unityAvailable;
    }
    
    private class WebViewJavaScriptInterface {
        @JavascriptInterface
        public void sendMessageToUnity(String message) {
            logDebug("Received message from WebView");
            sendUnityMessage("OnAndroidMessageReceived", message);
        }
        
        @JavascriptInterface
        public void log(String level, String message) {
            switch (level.toLowerCase()) {
                case "error":
                    Log.e(TAG, "WebView: " + message);
                    break;
                case "warn":
                    Log.w(TAG, "WebView: " + message);
                    break;
                default:
                    Log.i(TAG, "WebView: " + message);
                    break;
            }
        }
    }
}
