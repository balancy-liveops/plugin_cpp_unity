/**
 * Balancy WebView Plugin for Unity WebGL
 *
 * This plugin bridges Unity C# DllImport calls to the TypeScript WebViewManager.
 * It uses Unity's jslib format to expose functions to C# via DllImport(__Internal).
 */

var BalancyWebViewPlugin = {
  $BalancyWebView: {
    // Internal state
    isInitialized: false,
    pendingCalls: [],

    /**
     * Initialize the WebView manager
     * Called automatically when needed
     */
    init: function() {
      if (this.isInitialized) return;

      // Check if TypeScript WebViewManager is loaded
      if (typeof window.balancyWebView === 'undefined') {
        console.error('[Balancy WebGL] WebViewManager not loaded! Make sure balancy-webview.umd.js is included.');
        return;
      }

      // Set up callbacks from TypeScript to Unity
      window.balancyWebView.on('message', function(message) {
        BalancyWebView.onMessageReceived(message);
      });

      window.balancyWebView.on('load', function(success) {
        BalancyWebView.onLoadCompleted(success);
      });

      window.balancyWebView.on('closed', function() {
        BalancyWebView.onClosed();
      });

      this.isInitialized = true;
      console.log('[Balancy WebGL] Plugin initialized');

      // Process pending calls
      while (this.pendingCalls.length > 0) {
        var call = this.pendingCalls.shift();
        call();
      }
    },

    /**
     * Send message to Unity via SendMessage
     */
    onMessageReceived: function(message) {
      try {
        // Unity's SendMessage format: SendMessage(objectName, methodName, value)
        SendMessage('BalancyView', 'OnWebGLMessageReceived', message);
      } catch (e) {
        console.error('[Balancy WebGL] Failed to send message to Unity:', e);
      }
    },

    onLoadCompleted: function(success) {
      try {
        SendMessage('BalancyView', 'OnWebGLLoadCompleted', success ? 'true' : 'false');
      } catch (e) {
        console.error('[Balancy WebGL] Failed to send load completed to Unity:', e);
      }
    },

    onClosed: function() {
      try {
        SendMessage('BalancyView', 'OnWebGLClosed', '');
      } catch (e) {
        console.error('[Balancy WebGL] Failed to send closed to Unity:', e);
      }
    },

    /**
     * Execute a function, queuing it if not initialized
     */
    execute: function(fn) {
      if (this.isInitialized) {
        fn();
      } else {
        this.pendingCalls.push(fn);
        this.init();
      }
    }
  },

  /**
   * Open a WebView with the specified URL
   *
   * @param urlPtr - Pointer to URL string
   * @param ownerJsonPtr - Pointer to owner JSON string
   * @param additionalInfoPtr - Pointer to additional info JSON string
   * @param width - Width in pixels
   * @param height - Height in pixels
   * @returns true if successful, false otherwise
   */
  _balancyOpenWebView: function(urlPtr, ownerJsonPtr, additionalInfoPtr, width, height) {
    var url = UTF8ToString(urlPtr);
    var ownerJson = UTF8ToString(ownerJsonPtr);
    var additionalInfo = UTF8ToString(additionalInfoPtr);

    var success = false;

    BalancyWebView.execute(function() {
      try {
        window.balancyWebView.openWebView(url, ownerJson, {
          width: width,
          height: height,
          additionalInfo: additionalInfo
        });
        success = true;
      } catch (e) {
        console.error('[Balancy WebGL] Failed to open WebView:', e);
      }
    });

    return success;
  },

  /**
   * Close the currently open WebView
   */
  _balancyCloseWebView: function() {
    BalancyWebView.execute(function() {
      try {
        window.balancyWebView.closeWebView();
      } catch (e) {
        console.error('[Balancy WebGL] Failed to close WebView:', e);
      }
    });
  },

  /**
   * Send a message to the WebView
   *
   * @param messagePtr - Pointer to message string
   * @returns true if successful, false otherwise
   */
  _balancySendMessage: function(messagePtr) {
    var message = UTF8ToString(messagePtr);
    var success = false;

    BalancyWebView.execute(function() {
      try {
        window.balancyWebView.sendMessage(message);
        success = true;
      } catch (e) {
        console.error('[Balancy WebGL] Failed to send message:', e);
      }
    });

    return success;
  },

  /**
   * Inject JavaScript code into the WebView
   *
   * @param codePtr - Pointer to JavaScript code string
   * @returns true if successful, false otherwise
   */
  _balancyInjectCode: function(codePtr) {
    var code = UTF8ToString(codePtr);
    var success = false;

    BalancyWebView.execute(function() {
      try {
        window.balancyWebView.injectCode(code);
        success = true;
      } catch (e) {
        console.error('[Balancy WebGL] Failed to inject code:', e);
      }
    });

    return success;
  },

  /**
   * Set the viewport rectangle
   *
   * @param x - X position (0-1)
   * @param y - Y position (0-1)
   * @param width - Width (0-1)
   * @param height - Height (0-1)
   */
  _balancySetViewportRect: function(x, y, width, height) {
    BalancyWebView.execute(function() {
      try {
        var screenWidth = window.innerWidth;
        var screenHeight = window.innerHeight;

        window.balancyWebView.setPosition(
          Math.floor(x * screenWidth),
          Math.floor(y * screenHeight),
          Math.floor(width * screenWidth),
          Math.floor(height * screenHeight)
        );
      } catch (e) {
        console.error('[Balancy WebGL] Failed to set viewport:', e);
      }
    });
  },

  /**
   * Set transparent background
   *
   * @param transparent - true to enable transparency
   */
  _balancySetTransparentBackground: function(transparent) {
    BalancyWebView.execute(function() {
      try {
        window.balancyWebView.setTransparent(transparent !== 0);
      } catch (e) {
        console.error('[Balancy WebGL] Failed to set transparency:', e);
      }
    });
  },

  /**
   * Set game UI mode
   *
   * @param enabled - true to enable game UI mode
   */
  _balancySetGameUIMode: function(enabled) {
    BalancyWebView.execute(function() {
      try {
        window.balancyWebView.setGameUIMode(enabled !== 0);
      } catch (e) {
        console.error('[Balancy WebGL] Failed to set game UI mode:', e);
      }
    });
  },

  /**
   * Check if WebView is currently open
   *
   * @returns true if open, false otherwise
   */
  _balancyIsWebViewOpen: function() {
    if (!BalancyWebView.isInitialized) return false;

    try {
      return window.balancyWebView.isOpen();
    } catch (e) {
      return false;
    }
  }
};

// Register the plugin
autoAddDeps(BalancyWebViewPlugin, '$BalancyWebView');
mergeInto(LibraryManager.library, BalancyWebViewPlugin);
