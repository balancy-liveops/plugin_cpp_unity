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
import android.graphics.Canvas;
import android.graphics.Paint;
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
    private View emergencyExitButton;
    private Handler emergencyExitHideHandler;
    private Runnable emergencyExitHideRunnable;
    private long lastTapTime = 0;
    private int rapidTapCount = 0;
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
    private boolean emergencyExitEnabled = true;
    private boolean offlineCacheEnabled = false;
    
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
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.JELLY_BEAN) {
            settings.setAllowFileAccessFromFileURLs(true);
        }
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
            if (event.getAction() == android.view.MotionEvent.ACTION_DOWN) {
                if (!v.hasFocus()) {
                    v.requestFocus();
                }
                // Track rapid taps for triple-tap emergency exit
                long now = System.currentTimeMillis();
                if (now - lastTapTime < 400) {
                    rapidTapCount++;
                } else {
                    rapidTapCount = 1;
                }
                lastTapTime = now;
                if (rapidTapCount >= 3) {
                    rapidTapCount = 0;
                    showEmergencyExitButton();
                }
            } else if (event.getAction() == android.view.MotionEvent.ACTION_UP) {
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
        // Emergency exit is now triggered by triple-tap on the webView.
        // The button is created on-demand when triple-tap is detected.
        emergencyExitHideHandler = new Handler(Looper.getMainLooper());
        logDebug("Triple-tap emergency exit gesture installed");
    }

    private void showEmergencyExitButton() {
        if (!emergencyExitEnabled) return;
        if (webViewContainer == null || currentActivity == null) return;

        // Cancel pending hide
        if (emergencyExitHideHandler != null && emergencyExitHideRunnable != null) {
            emergencyExitHideHandler.removeCallbacks(emergencyExitHideRunnable);
            emergencyExitHideRunnable = null;
        }

        if (emergencyExitButton == null) {
            final int btnSizeDp = 32;
            final float density = currentActivity.getResources().getDisplayMetrics().density;
            final int btnSizePx = (int) (btnSizeDp * density);
            final int marginPx = (int) (12 * density);

            // Custom view that draws a red circle with white X
            emergencyExitButton = new View(currentActivity) {
                private final Paint circlePaint = new Paint(Paint.ANTI_ALIAS_FLAG);
                private final Paint xPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
                {
                    circlePaint.setColor(Color.argb(230, 230, 25, 25));
                    circlePaint.setStyle(Paint.Style.FILL);
                    xPaint.setColor(Color.WHITE);
                    xPaint.setStrokeWidth(2.5f * density);
                    xPaint.setStrokeCap(Paint.Cap.ROUND);
                    xPaint.setStyle(Paint.Style.STROKE);
                }

                @Override
                protected void onDraw(Canvas canvas) {
                    super.onDraw(canvas);
                    float cx = getWidth() / 2f;
                    float cy = getHeight() / 2f;
                    float radius = Math.min(cx, cy);
                    canvas.drawCircle(cx, cy, radius, circlePaint);

                    float inset = radius * 0.38f;
                    canvas.drawLine(cx - inset, cy - inset, cx + inset, cy + inset, xPaint);
                    canvas.drawLine(cx + inset, cy - inset, cx - inset, cy + inset, xPaint);
                }
            };

            FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(btnSizePx, btnSizePx);
            params.gravity = android.view.Gravity.TOP | android.view.Gravity.END;
            params.topMargin = marginPx;
            params.rightMargin = marginPx;
            emergencyExitButton.setLayoutParams(params);
            emergencyExitButton.setElevation(100f);

            emergencyExitButton.setOnClickListener(v -> {
                logDebug("Emergency exit button tapped");
                // Cancel auto-hide
                if (emergencyExitHideHandler != null && emergencyExitHideRunnable != null) {
                    emergencyExitHideHandler.removeCallbacks(emergencyExitHideRunnable);
                    emergencyExitHideRunnable = null;
                }
                sendUnityMessage("OnAndroidMessageReceived", "{\"action\":200, \"params\":{}}");
            });

            webViewContainer.addView(emergencyExitButton);
        }

        emergencyExitButton.setVisibility(View.VISIBLE);
        emergencyExitButton.setAlpha(1.0f);

        // Auto-hide after 3 seconds
        emergencyExitHideRunnable = this::hideEmergencyExitButton;
        emergencyExitHideHandler.postDelayed(emergencyExitHideRunnable, 3000);

        logDebug("Emergency exit button shown at top-right");
    }

    private void hideEmergencyExitButton() {
        emergencyExitHideRunnable = null;
        if (emergencyExitButton != null) {
            emergencyExitButton.animate()
                .alpha(0.0f)
                .setDuration(300)
                .setListener(new AnimatorListenerAdapter() {
                    @Override
                    public void onAnimationEnd(Animator animation) {
                        if (emergencyExitButton != null) {
                            emergencyExitButton.setVisibility(View.GONE);
                        }
                    }
                })
                .start();
        }
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
            // Clean up emergency exit
            if (emergencyExitHideHandler != null && emergencyExitHideRunnable != null) {
                emergencyExitHideHandler.removeCallbacks(emergencyExitHideRunnable);
                emergencyExitHideRunnable = null;
            }

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
        
        if (url.startsWith("file://") && !url.startsWith("file:///android_asset/")) {
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
        
        // Apply transparency settings
        if (transparentBackground) {
            webView.setBackgroundColor(Color.TRANSPARENT);
            webViewContainer.setBackgroundColor(Color.TRANSPARENT);
        } else {
            webView.setBackgroundColor(Color.WHITE);
            webViewContainer.setBackgroundColor(Color.WHITE);
        }
        
        // Apply cache settings
        WebSettings settings = webView.getSettings();
        if (offlineCacheEnabled) {
            settings.setCacheMode(WebSettings.LOAD_DEFAULT);
            settings.setDomStorageEnabled(true);
            settings.setDatabaseEnabled(true);
            // Note: setAppCacheEnabled() is deprecated and removed in API 33+
            // Modern caching is handled by DOM storage and regular cache
        } else {
            settings.setCacheMode(WebSettings.LOAD_CACHE_ELSE_NETWORK); // Keep some caching for performance
            settings.setDomStorageEnabled(true);
            settings.setDatabaseEnabled(true);
        }
        
        // Apply viewport settings
        applyViewportSettings();
        
        logDebug("Settings applied: transparent=" + transparentBackground + 
                ", cache=" + offlineCacheEnabled + ", gameUI=" + gameUIMode);
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
    
    // ================================================================================
    // ✅ NEW METHODS - Missing methods that C# tries to call
    // ================================================================================
    
    /**
     * Sets the viewport rectangle for the WebView
     * @param x X position (0.0-1.0, percentage of screen width from left)
     * @param y Y position (0.0-1.0, percentage of screen height from top)
     * @param width Width (0.0-1.0, percentage of screen width)
     * @param height Height (0.0-1.0, percentage of screen height)
     */
    public void setViewportRect(float x, float y, float width, float height) {
        // Clamp values to valid range (0.0-1.0)
        this.viewportX = Math.max(0f, Math.min(1f, x));
        this.viewportY = Math.max(0f, Math.min(1f, y));
        this.viewportWidth = Math.max(0f, Math.min(1f, width));
        this.viewportHeight = Math.max(0f, Math.min(1f, height));
        
        logDebug(String.format("Viewport set to: x=%.2f, y=%.2f, width=%.2f, height=%.2f", 
                x, y, width, height));
        
        // Apply immediately if WebView is open
        if (webView != null && isWebViewOpen) {
            runOnUIThread(this::applyViewportSettings);
        }
    }
    
    /**
     * Applies viewport settings to the WebView
     */
    private void applyViewportSettings() {
        if (webView == null || currentActivity == null) return;
        
        android.util.DisplayMetrics metrics = new android.util.DisplayMetrics();
        currentActivity.getWindowManager().getDefaultDisplay().getRealMetrics(metrics);
        
        int screenWidth = metrics.widthPixels;
        int screenHeight = metrics.heightPixels;
        
        int x = (int)(viewportX * screenWidth);
        int y = (int)(viewportY * screenHeight);
        int width = (int)(viewportWidth * screenWidth);
        int height = (int)(viewportHeight * screenHeight);
        
        FrameLayout.LayoutParams params = (FrameLayout.LayoutParams) webView.getLayoutParams();
        if (params != null) {
            params.leftMargin = x;
            params.topMargin = y;
            params.width = width;
            params.height = height;
            webView.setLayoutParams(params);
            
            logDebug(String.format("Viewport applied: x=%d, y=%d, width=%d, height=%d (screen: %dx%d)", 
                    x, y, width, height, screenWidth, screenHeight));
        }
    }
    
    /**
     * Sets transparent background for the WebView
     * @param transparent True for transparent background, false for opaque
     */
    public void setTransparentBackground(boolean transparent) {
        this.transparentBackground = transparent;
        logDebug("Transparent background " + (transparent ? "enabled" : "disabled"));
        
        if (webView != null && isWebViewOpen) {
            runOnUIThread(() -> {
                if (transparent) {
                    webView.setBackgroundColor(Color.TRANSPARENT);
                    if (webViewContainer != null) {
                        webViewContainer.setBackgroundColor(Color.TRANSPARENT);
                    }
                    
                    // Inject CSS for transparent background
                    String transparentCSS = 
                        "(function() {" +
                        "  document.body.style.backgroundColor = 'transparent';" +
                        "  document.documentElement.style.backgroundColor = 'transparent';" +
                        "  var style = document.createElement('style');" +
                        "  style.innerHTML = 'body, html { background-color: transparent !important; }';" +
                        "  document.head.appendChild(style);" +
                        "})();";
                    
                    webView.evaluateJavascript(transparentCSS, null);
                } else {
                    webView.setBackgroundColor(Color.WHITE);
                    if (webViewContainer != null) {
                        webViewContainer.setBackgroundColor(Color.WHITE);
                    }
                }
            });
        }
    }
    
    /**
     * Enables or disables offline caching of web content
     * @param enabled True to enable offline caching, false to disable
     */
    public void setOfflineCacheEnabled(boolean enabled) {
        this.offlineCacheEnabled = enabled;
        logDebug("Offline cache " + (enabled ? "enabled" : "disabled"));
        
        final WebView tmp = webView;
        
        runOnUIThread(() -> {
            if (tmp == null || tmp != webView) return;

            WebSettings settings = tmp.getSettings();
            if (enabled) {
                settings.setCacheMode(WebSettings.LOAD_DEFAULT);
                settings.setDomStorageEnabled(true);
                settings.setDatabaseEnabled(true);
                // Note: setAppCacheEnabled() is deprecated and removed in API 33+
                // Modern caching is handled by DOM storage and regular cache
            } else {
                settings.setCacheMode(WebSettings.LOAD_NO_CACHE);
                settings.setDomStorageEnabled(false);
                settings.setDatabaseEnabled(false);
            }
        });
    }
    
    /**
     * Sets the delay before showing the WebView after page loads
     * @param delaySeconds Delay in seconds (default: 0.1)
     */
    public void setShowDelay(float delaySeconds) {
        this.showDelay = Math.max(0f, delaySeconds);
        logDebug(String.format("Show delay set to: %.3f seconds", showDelay));
    }
    
    /**
     * Sets the duration of the fade-in animation when showing the WebView
     * @param durationSeconds Animation duration in seconds (default: 0.1)
     */
    public void setAnimationDuration(float durationSeconds) {
        this.animationDuration = Math.max(0f, durationSeconds);
        logDebug(String.format("Animation duration set to: %.3f seconds", animationDuration));
    }
    
    /**
     * Enables or disables the emergency exit button
     * @param enabled True to enable emergency exit, false to disable
     */
    public void setEmergencyExitEnabled(boolean enabled) {
        this.emergencyExitEnabled = enabled;
        logDebug("Emergency exit " + (enabled ? "enabled" : "disabled"));

        if (!enabled) {
            runOnUIThread(() -> {
                if (emergencyExitHideHandler != null && emergencyExitHideRunnable != null) {
                    emergencyExitHideHandler.removeCallbacks(emergencyExitHideRunnable);
                    emergencyExitHideRunnable = null;
                }
                if (emergencyExitButton != null) {
                    emergencyExitButton.setVisibility(View.GONE);
                    webViewContainer.removeView(emergencyExitButton);
                    emergencyExitButton = null;
                }
            });
        }
        // When enabled, the triple-tap on webView handles showing the button
    }
    
    /**
     * Sets game UI mode - disables browser features for game-like feel
     * @param enabled True to enable game UI mode, false for standard web browsing
     */
    public void setGameUIMode(boolean enabled) {
        this.gameUIMode = enabled;
        logDebug("Game UI mode " + (enabled ? "enabled" : "disabled"));
        
        if (webView != null && isWebViewOpen) {
            runOnUIThread(() -> {
                if (enabled) {
                    injectGameUIModeCSS();
                } else {
                    // Re-enable standard web behaviors
                    String standardWebScript = 
                        "(function() {" +
                        "  document.documentElement.style.webkitUserSelect = 'auto';" +
                        "  document.documentElement.style.userSelect = 'auto';" +
                        "  document.documentElement.oncontextmenu = null;" +
                        "  document.documentElement.style.webkitTouchCallout = 'default';" +
                        "})();";
                    
                    webView.evaluateJavascript(standardWebScript, null);
                }
            });
        }
    }
    
    // ================================================================================
    // ✅ EXISTING JAVASCRIPT INTERFACE (UNCHANGED)
    // ================================================================================
    
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
