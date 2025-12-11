/**
 * Balancy WebGL Initialization Script
 *
 * This script loads and initializes the TypeScript WebViewManager
 * for use with Unity WebGL builds.
 */

(function() {
  'use strict';

  // Configuration
  const CONFIG = {
    jszipPath: 'StreamingAssets/Balancy/jszip.min.js',
    webviewBundlePath: 'StreamingAssets/Balancy/balancy-webview.umd.js',
    bridgePath: 'StreamingAssets/Balancy/balancy-webview-bridge.js',
    debug: true
  };

  /**
   * Log helper
   */
  function log(message, level = 'log') {
    if (CONFIG.debug || level === 'error') {
      console[level]('[Balancy WebGL Init]', message);
    }
  }

  /**
   * Load script dynamically
   */
  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = src;
      script.async = false; // Ensure order

      script.addEventListener('load', () => {
        log(`Loaded: ${src}`);
        resolve();
      });

      script.addEventListener('error', () => {
        const error = `Failed to load: ${src}`;
        log(error, 'error');
        reject(new Error(error));
      });

      document.head.appendChild(script);
    });
  }

  /**
   * Initialize Balancy WebView
   */
  async function initializeBalancyWebView() {
    try {
      log('Initializing Balancy WebView for Unity WebGL...');

      // Load JSZip library if not already loaded (needed for unzipping)
      if (typeof JSZip === 'undefined') {
        log('Loading JSZip library...');
        await loadScript(CONFIG.jszipPath);
      } else {
        log('JSZip already loaded, skipping');
      }

      // Load WebView bundle
      await loadScript(CONFIG.webviewBundlePath);

      // Check if WebViewManager is available from the bundle
      // The bundle exports as UMD, so it should be available as a global
      if (typeof window.BalancyWebViewLib === 'undefined' && typeof window.WebViewManager === 'undefined') {
        log('⚠️ WebViewManager not found as global, trying to extract from module...');
      }

      // Create a simple wrapper that uses the WebViewManager from the bundle
      window.balancyWebView = {
        webViewManager: null,

        /**
         * Initialize the WebViewManager
         */
        init: function() {
          if (this.webViewManager) {
            return this.webViewManager;
          }

          // Try to get WebViewManager from the global scope
          // The UMD bundle exports to window.BalancyWebView namespace
          var WebViewManager = window.WebViewManager ||
                               (window.BalancyWebView && window.BalancyWebView.WebViewManager) ||
                               (window.BalancyWebViewLib && window.BalancyWebViewLib.WebViewManager);

          if (!WebViewManager) {
            console.error('[Balancy WebView] WebViewManager class not found in bundle!');
            console.error('[Balancy WebView] Available:', Object.keys(window.BalancyWebView || {}));
            return null;
          }

          // Create WebViewManager instance
          this.webViewManager = new WebViewManager({
            transparent: true,
            allowScripts: true,
            autoResize: true,
            debugMode: CONFIG.debug,
            zIndex: 999999,
            enablePointerEvents: true
          });

          log('✅ WebViewManager initialized');
          return this.webViewManager;
        },

        /**
         * Open WebView with HTML content
         */
        openHtml: function(htmlContent, ownerData, additionalData, manifestData) {
          var manager = this.init();
          if (!manager) {
            return false;
          }

          try {
            // Use the WebViewManager's openWithHtml method
            manager.openWithHtml(htmlContent, {
              ownerData: ownerData,
              additionalData: additionalData,
              manifestData: manifestData
            });
            return true;
          } catch (error) {
            console.error('[Balancy WebView] Error opening HTML:', error);
            return false;
          }
        },

        /**
         * Open WebView with URL
         */
        open: function(url, ownerData, additionalData) {
          var manager = this.init();
          if (!manager) {
            return false;
          }

          try {
            manager.open(url, {
              ownerData: ownerData,
              additionalData: additionalData
            });
            return true;
          } catch (error) {
            console.error('[Balancy WebView] Error opening URL:', error);
            return false;
          }
        },

        /**
         * Close the WebView
         */
        close: function() {
          if (this.webViewManager) {
            this.webViewManager.close();
          }
        },

        /**
         * Send message to WebView
         */
        sendMessage: function(message) {
          if (this.webViewManager) {
            this.webViewManager.sendMessage(message);
          }
        },

        /**
         * Check if WebView is open
         */
        isOpen: function() {
          return this.webViewManager ? this.webViewManager.isOpen() : false;
        },

        /**
         * Inject code into WebView
         */
        injectCode: function(code) {
          if (this.webViewManager) {
            this.webViewManager.injectCode(code);
          }
        }
      };

      log('✅ Balancy WebView wrapper initialized successfully!');
      log('API available at: window.balancyWebView');

      // Notify Unity that WebView is ready (if Unity is loaded)
      if (typeof SendMessage !== 'undefined') {
        try {
          SendMessage('BalancyView', 'OnWebGLWebViewReady', '');
        } catch (e) {
          // Ignore if Unity isn't ready yet
          log('Unity not ready yet, will retry when WebView is opened');
        }
      }

    } catch (error) {
      log('❌ Failed to initialize Balancy WebView: ' + error.message, 'error');
      throw error;
    }
  }

  /**
   * Wait for DOM to be ready
   */
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeBalancyWebView);
  } else {
    initializeBalancyWebView();
  }

})();
