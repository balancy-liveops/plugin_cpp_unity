/**
 * Balancy WebView Plugin for Unity WebGL
 *
 * Provides JavaScript functions for opening and interacting with the WebView
 * using the TypeScript BalancyWebView bundle.
 */

var BalancyWebViewPlugin = {
  $BalancyWebViewState: {
    webView: null,
    initializationStarted: false,
    initializationCallbacks: [],
    unityInstanceRef: null,

    /**
     * Register helper function for external JavaScript to send messages to Unity
     */
    registerMessageForwarder: function() {
      // Expose a global function that external JavaScript can call
      window.sendToUnity = function(target, method, message) {
        if (typeof SendMessage !== 'undefined') {
          SendMessage(target, method, message);
          return true;
        } else {
          console.error('[BalancyWebView] SendMessage not available in jslib context');
          return false;
        }
      };
      console.log('[BalancyWebView Plugin] Message forwarder registered');
    },

    /**
     * Load the initialization scripts
     */
    loadInitScripts: function() {
      if (this.initializationStarted) {
        return Promise.resolve();
      }

      this.initializationStarted = true;
      console.log('[BalancyWebView Plugin] Loading initialization scripts...');

      return new Promise((resolve, reject) => {
        // Check if already initialized
        if (typeof window.balancyWebView !== 'undefined') {
          console.log('[BalancyWebView Plugin] WebView already initialized');
          this.webView = window.balancyWebView;
          resolve();
          return;
        }

        // Load JSZip first
        var jszipScript = document.createElement('script');
        jszipScript.src = 'StreamingAssets/Balancy/jszip.min.js';
        jszipScript.onload = function() {
          console.log('[BalancyWebView Plugin] JSZip loaded');

          // Load WebView bundle (includes unity-entry which creates window.balancyWebView)
          var webviewScript = document.createElement('script');
          webviewScript.src = 'StreamingAssets/Balancy/balancy-webview.umd.js';
          webviewScript.onload = function() {
            console.log('[BalancyWebView Plugin] WebView bundle loaded');

            // Load bridge script
            var bridgeScript = document.createElement('script');
            bridgeScript.src = 'StreamingAssets/Balancy/balancy-webview-bridge.js';
            bridgeScript.onload = function() {
              console.log('[BalancyWebView Plugin] Bridge loaded');

              // Wait a bit for initialization to complete
              setTimeout(function() {
                if (typeof window.balancyWebView !== 'undefined') {
                  BalancyWebViewState.webView = window.balancyWebView;
                  console.log('[BalancyWebView Plugin] ✅ WebView initialized successfully');

                  // Register message forwarder
                  BalancyWebViewState.registerMessageForwarder();

                  resolve();
                } else {
                  reject(new Error('WebView not available after bundle load'));
                }
              }, 100);
            };
            bridgeScript.onerror = function() {
              reject(new Error('Failed to load bridge script'));
            };
            document.head.appendChild(bridgeScript);
          };
          webviewScript.onerror = function() {
            reject(new Error('Failed to load webview bundle'));
          };
          document.head.appendChild(webviewScript);
        };
        jszipScript.onerror = function() {
          reject(new Error('Failed to load JSZip'));
        };
        document.head.appendChild(jszipScript);
      });
    },

    /**
     * Get or initialize the WebView instance
     */
    getWebView: function(callback) {
      if (this.webView !== null) {
        callback(this.webView);
        return;
      }

      // Check if already available
      if (typeof window.balancyWebView !== 'undefined') {
        this.webView = window.balancyWebView;
        console.log('[BalancyWebView Plugin] Using existing WebView instance');
        callback(this.webView);
        return;
      }

      // Need to load scripts
      console.log('[BalancyWebView Plugin] WebView not ready, loading scripts...');
      this.initializationCallbacks.push(callback);

      this.loadInitScripts().then(function() {
        console.log('[BalancyWebView Plugin] Initialization complete, notifying callbacks');
        var callbacks = BalancyWebViewState.initializationCallbacks;
        BalancyWebViewState.initializationCallbacks = [];
        callbacks.forEach(function(cb) {
          cb(BalancyWebViewState.webView);
        });
      }).catch(function(error) {
        console.error('[BalancyWebView Plugin] Failed to initialize:', error);
        var callbacks = BalancyWebViewState.initializationCallbacks;
        BalancyWebViewState.initializationCallbacks = [];
        callbacks.forEach(function(cb) {
          cb(null);
        });
      });
    }
  },

  /**
   * Open WebView with HTML content
   */
  _balancyOpenWebViewWithHtmlContent: function(htmlContentPtr, ownerJsonPtr, additionalInfoPtr, manifestJsonPtr) {
    try {
      var htmlContent = UTF8ToString(htmlContentPtr);
      var ownerJson = UTF8ToString(ownerJsonPtr);
      var additionalInfo = UTF8ToString(additionalInfoPtr);
      var manifestJson = UTF8ToString(manifestJsonPtr);

      console.log('[BalancyWebView Plugin] Opening WebView with HTML content:', htmlContent.length, 'bytes');
      console.log('[BalancyWebView Plugin] Owner JSON:', ownerJson.substring(0, 100) + '...');
      console.log('[BalancyWebView Plugin] Manifest JSON:', manifestJson.substring(0, 100) + '...');

      // Get WebView asynchronously
      BalancyWebViewState.getWebView(function(webView) {
        if (!webView) {
          console.error('[BalancyWebView Plugin] WebView not available');
          // Notify Unity of failure
          if (typeof SendMessage !== 'undefined') {
            try {
              SendMessage('BalancyView', 'OnWebGLLoadCompleted', 'false');
            } catch (e) {
              console.error('[BalancyWebView Plugin] Failed to notify Unity:', e);
            }
          }
          return;
        }

        try {
          // Parse manifest JSON
          var manifestData = manifestJson ? JSON.parse(manifestJson) : {};

          // Call openWithHtml method on UnityBalancyWebView instance
          webView.openWithHtml(htmlContent, ownerJson, {
            additionalInfo: additionalInfo,
            manifestData: manifestData
          });

          console.log('[BalancyWebView Plugin] WebView opened successfully');

          // Notify Unity of success
          if (typeof SendMessage !== 'undefined') {
            try {
              SendMessage('BalancyView', 'OnWebGLLoadCompleted', 'true');
            } catch (e) {
              console.error('[BalancyWebView Plugin] Failed to notify Unity:', e);
            }
          }
        } catch (error) {
          console.error('[BalancyWebView Plugin] Error opening WebView:', error);
          if (typeof SendMessage !== 'undefined') {
            try {
              SendMessage('BalancyView', 'OnWebGLLoadCompleted', 'false');
            } catch (e) {}
          }
        }
      });

      // Return true to indicate request was accepted (actual result comes via callback)
      return true;

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error opening WebView with HTML:', error);
      return false;
    }
  },

  /**
   * Open WebView with URL
   */
  _balancyOpenWebView: function(urlPtr, ownerJsonPtr, additionalInfoPtr, width, height) {
    try {
      var url = UTF8ToString(urlPtr);
      var ownerJson = UTF8ToString(ownerJsonPtr);
      var additionalInfo = UTF8ToString(additionalInfoPtr);

      console.log('[BalancyWebView Plugin] Opening WebView with URL:', url);

      BalancyWebViewState.getWebView(function(webView) {
        if (!webView) {
          console.error('[BalancyWebView Plugin] WebView not available');
          return;
        }

        // Call openWebView method on UnityBalancyWebView instance
        webView.openWebView(url, ownerJson, {
          width: width,
          height: height,
          additionalInfo: additionalInfo
        });

        console.log('[BalancyWebView Plugin] WebView opened');
      });

      // Return true to indicate request was accepted (actual result comes via callback)
      return true;

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error opening WebView:', error);
      return false;
    }
  },

  /**
   * Close WebView
   */
  _balancyCloseWebView: function() {
    try {
      console.log('[BalancyWebView Plugin] Closing WebView');

      BalancyWebViewState.getWebView(function(webView) {
        if (!webView) {
          console.warn('[BalancyWebView Plugin] WebView not available');
          return;
        }

        webView.closeWebView();
        console.log('[BalancyWebView Plugin] WebView closed');
      });

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error closing WebView:', error);
    }
  },

  /**
   * Send message to WebView
   */
  _balancySendMessage: function(messagePtr) {
    try {
      var message = UTF8ToString(messagePtr);
      console.log('[BalancyWebView Plugin] Sending message to WebView:', message.substring(0, 100) + '...');

      // Try to convert image paths to blob URLs before sending
      // We use the cached blob URLs from the preload system (same as sprite loading)
      var processedMessage = message;
      try {
        var parsed = JSON.parse(message);
        console.log('[BalancyWebView Plugin] Parsed message type:', parsed.type);

        // Check if this is a batch response with image URLs
        if (parsed.type === 'batch-response' && Array.isArray(parsed.responses)) {
          console.log('[BalancyWebView Plugin] Processing batch-response with', parsed.responses.length, 'responses');

          for (var i = 0; i < parsed.responses.length; i++) {
            var resp = parsed.responses[i];
            //console.log('[BalancyWebView Plugin] Response[' + i + '] id:', resp.id, 'result type:', typeof resp.result);

            // Check if result contains a file path
            if (resp.result && typeof resp.result === 'string') {
              if (resp.result.includes('/idbfs/') || /\.(png|jpg|jpeg|gif|webp|svg)$/i.test(resp.result)) {
                console.log('[BalancyWebView Plugin] 🎯 Found image path in response[' + i + ']:', resp.result);

                // Check blob URL cache (populated by _balancyPreloadFileAsBlobUrl)
                if (window._balancyBlobUrlCache && window._balancyBlobUrlCache[resp.result]) {
                  var cachedBlobUrl = window._balancyBlobUrlCache[resp.result];
                  console.log('[BalancyWebView Plugin] ✅ Using cached blob URL:', cachedBlobUrl);
                  resp.result = cachedBlobUrl;
                } else {
                  console.warn('[BalancyWebView Plugin] ⚠️ Image not in cache, will need to be loaded asynchronously');
                  console.warn('[BalancyWebView Plugin]    Path:', resp.result);
                  console.warn('[BalancyWebView Plugin]    This image should have been preloaded via C# DataObjectsManager');
                  // Leave path as-is, WebView will need to handle loading
                }
              }
            }
          }

          // Re-stringify the modified message
          processedMessage = JSON.stringify(parsed);
          console.log('[BalancyWebView Plugin] Message processed, sending to WebView');
        }

      } catch (parseError) {
        console.log('[BalancyWebView Plugin] Message is not JSON or error parsing, sending as-is');
      }

      BalancyWebViewState.getWebView(function(webView) {
        if (!webView) {
          console.warn('[BalancyWebView Plugin] WebView not available');
          return;
        }

        webView.sendMessage(processedMessage);
      });

      return true;

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error sending message:', error);
      return false;
    }
  },

  /**
   * Check if WebView is open
   */
  _balancyIsWebViewOpen: function() {
    try {
      // Check if webView is already cached
      if (BalancyWebViewState.webView !== null) {
        return BalancyWebViewState.webView.isOpen();
      }

      // Not initialized yet
      return false;

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error checking if WebView is open:', error);
      return false;
    }
  },

  /**
   * Inject code into WebView
   */
  _balancyInjectCode: function(codePtr) {
    try {
      var code = UTF8ToString(codePtr);
      console.log('[BalancyWebView Plugin] Injecting code into WebView');

      BalancyWebViewState.getWebView(function(webView) {
        if (!webView) {
          console.warn('[BalancyWebView Plugin] WebView not available');
          return;
        }

        webView.injectCode(code);
      });

      return true;

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error injecting code:', error);
      return false;
    }
  },

  /**
   * Set viewport rect
   */
  _balancySetViewportRect: function(x, y, width, height) {
    console.log('[BalancyWebView Plugin] Set viewport rect:', x, y, width, height);
    // TODO: Implement if needed
  },

  /**
   * Set transparent background
   */
  _balancySetTransparentBackground: function(transparent) {
    console.log('[BalancyWebView Plugin] Set transparent background:', transparent);
    // Handled by WebView CSS
  },

  /**
   * Set game UI mode
   */
  _balancySetGameUIMode: function(enabled) {
    console.log('[BalancyWebView Plugin] Set game UI mode:', enabled);
    // Handled by WebView CSS
  },

  /**
   * Helper function to send Unity message from external JavaScript
   * This bridges the gap between TypeScript code and Unity's SendMessage
   */
  _balancySendUnityMessage: function(targetPtr, methodPtr, messagePtr) {
    try {
      var target = UTF8ToString(targetPtr);
      var method = UTF8ToString(methodPtr);
      var message = UTF8ToString(messagePtr);

      console.log('[BalancyWebView Plugin] Forwarding message to Unity:', target, method);

      // SendMessage is available in the Unity runtime context
      if (typeof SendMessage !== 'undefined') {
        SendMessage(target, method, message);
        return true;
      } else {
        console.error('[BalancyWebView Plugin] SendMessage not available');
        return false;
      }
    } catch (error) {
      console.error('[BalancyWebView Plugin] Error sending Unity message:', error);
      return false;
    }
  },

  /**
   * Read file from Emscripten FS and return as blob URL
   * Returns empty string if file doesn't exist or error occurs
   */
  // Synchronous version that returns cached blob URLs or empty string
  // Files must be preloaded first via _balancyPreloadFileAsBlobUrl
  _balancyReadFileAsBlobUrl: function(pathPtr) {
    try {
      var path = UTF8ToString(pathPtr);

      // Check if blob URL is already cached
      if (!window._balancyBlobUrlCache) {
        window._balancyBlobUrlCache = {};
      }

      var cachedUrl = window._balancyBlobUrlCache[path];
      if (cachedUrl) {
        console.log('[BalancyWebView Plugin] Using cached blob URL for:', path);
        var bufferSize = lengthBytesUTF8(cachedUrl) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(cachedUrl, buffer, bufferSize);
        return buffer;
      }

      console.warn('[BalancyWebView Plugin] No cached blob URL for:', path, '- file needs to be preloaded first');
      return 0;
    } catch (error) {
      console.error('[BalancyWebView Plugin] Error reading cached blob URL:', error);
      return 0;
    }
  },

  // Async version that loads from IndexedDB and caches blob URL
  _balancyPreloadFileAsBlobUrl: function(directoryPtr, fileNamePtr, callback, userData) {
    try {
      var directory = UTF8ToString(directoryPtr);
      var fileName = UTF8ToString(fileNamePtr);
      var fullPath = directory + '/' + fileName;

      console.log('[BalancyWebView Plugin] Preloading file as blob URL:', fullPath);
      console.log('[BalancyWebView Plugin]   Directory:', directory);
      console.log('[BalancyWebView Plugin]   FileName:', fileName);

      if (typeof BalancyIndexedDBFileHelper === 'undefined') {
        console.error('[BalancyWebView Plugin] BalancyIndexedDBFileHelper not available');
        {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
        return;
      }

      // Load file from IndexedDB
      BalancyIndexedDBFileHelper.loadFile(directory, fileName).then(function(data) {
        if (!data) {
          console.error('[BalancyWebView Plugin] File not found in IndexedDB:', fullPath);
          {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
          return;
        }

        console.log('[BalancyWebView Plugin] File loaded from IndexedDB, size:',
          data.byteLength || data.length, 'bytes');

        // Determine MIME type from extension
        var ext = fileName.split('.').pop().toLowerCase();
        var mimeType =
          (ext === 'jpg' || ext === 'jpeg') ? 'image/jpeg' :
          ext === 'png' ? 'image/png' :
          ext === 'gif' ? 'image/gif' :
          ext === 'webp' ? 'image/webp' :
          ext === 'svg' ? 'image/svg+xml' :
          'image/png';

        // Create blob from data (handles both ArrayBuffer and string)
        var blob;
        if (data instanceof ArrayBuffer) {
          blob = new Blob([data], { type: mimeType });
        } else if (typeof data === 'string') {
          // Convert base64 string to binary if needed
          blob = new Blob([data], { type: mimeType });
        } else {
          console.error('[BalancyWebView Plugin] Unexpected data type:', typeof data);
          {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
          return;
        }

        // Create blob URL
        var blobUrl = URL.createObjectURL(blob);
        console.log('[BalancyWebView Plugin] Blob URL created:', blobUrl);

        // Cache the blob URL
        if (!window._balancyBlobUrlCache) {
          window._balancyBlobUrlCache = {};
        }
        window._balancyBlobUrlCache[fullPath] = blobUrl;

        // Return blob URL to C++
        var bufferSize = lengthBytesUTF8(blobUrl) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(blobUrl, buffer, bufferSize);
        {{{ makeDynCall('vii', 'callback') }}}(userData, buffer);
        _free(buffer);

      }).catch(function(error) {
        console.error('[BalancyWebView Plugin] Error loading file from IndexedDB:', error);
        {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
      });

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error preloading file as blob URL:', error);
      {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
    }
  },

  /**
   * Get or create blob URL for a file (async with callback)
   * This is used by C++ getUrlCachePath to return blob URLs instead of IDBFS paths
   */
  _balancyGetOrCreateBlobUrl: function(directoryPtr, fileNamePtr, callback, userData) {
    try {
      var directory = UTF8ToString(directoryPtr);
      var fileName = UTF8ToString(fileNamePtr);
      var fullPath = directory + '/' + fileName;

      console.log('[BalancyWebView Plugin] Getting or creating blob URL for:', fullPath);

      // Check if already cached
      if (window._balancyBlobUrlCache && window._balancyBlobUrlCache[fullPath]) {
        var cachedBlobUrl = window._balancyBlobUrlCache[fullPath];
        console.log('[BalancyWebView Plugin] ✅ Blob URL already cached:', cachedBlobUrl);

        // Return cached blob URL
        var bufferSize = lengthBytesUTF8(cachedBlobUrl) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(cachedBlobUrl, buffer, bufferSize);
        {{{ makeDynCall('vii', 'callback') }}}(userData, buffer);
        _free(buffer);
        return;
      }

      // Not cached - need to load from IndexedDB
      console.log('[BalancyWebView Plugin] Blob URL not cached, loading from IndexedDB...');

      if (typeof BalancyIndexedDBFileHelper === 'undefined') {
        console.error('[BalancyWebView Plugin] BalancyIndexedDBFileHelper not available');
        {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
        return;
      }

      // Load file from IndexedDB
      BalancyIndexedDBFileHelper.loadFile(directory, fileName).then(function(data) {
        if (!data) {
          console.error('[BalancyWebView Plugin] File not found in IndexedDB:', fullPath);
          {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
          return;
        }

        console.log('[BalancyWebView Plugin] File loaded from IndexedDB, size:',
          data.byteLength || data.length, 'bytes');

        // Determine MIME type from extension
        var ext = fileName.split('.').pop().toLowerCase();
        var mimeType =
          (ext === 'jpg' || ext === 'jpeg') ? 'image/jpeg' :
          ext === 'png' ? 'image/png' :
          ext === 'gif' ? 'image/gif' :
          ext === 'webp' ? 'image/webp' :
          ext === 'svg' ? 'image/svg+xml' :
          ext === 'ttf' ? 'font/ttf' :
          ext === 'woff' ? 'font/woff' :
          ext === 'woff2' ? 'font/woff2' :
          'application/octet-stream';

        // Create blob from data
        var blob;
        if (data instanceof ArrayBuffer) {
          blob = new Blob([data], { type: mimeType });
        } else if (typeof data === 'string') {
          blob = new Blob([data], { type: mimeType });
        } else {
          console.error('[BalancyWebView Plugin] Unexpected data type:', typeof data);
          {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
          return;
        }

        // Create blob URL
        var blobUrl = URL.createObjectURL(blob);
        console.log('[BalancyWebView Plugin] ✅ Blob URL created:', blobUrl);

        // Cache the blob URL
        if (!window._balancyBlobUrlCache) {
          window._balancyBlobUrlCache = {};
        }
        window._balancyBlobUrlCache[fullPath] = blobUrl;

        // Return blob URL to C++
        var bufferSize = lengthBytesUTF8(blobUrl) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(blobUrl, buffer, bufferSize);
        {{{ makeDynCall('vii', 'callback') }}}(userData, buffer);
        _free(buffer);

      }).catch(function(error) {
        console.error('[BalancyWebView Plugin] Error loading file from IndexedDB:', error);
        {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
      });

    } catch (error) {
      console.error('[BalancyWebView Plugin] Error getting or creating blob URL:', error);
      {{{ makeDynCall('vii', 'callback') }}}(userData, 0);
    }
  }
};

// Register the library
autoAddDeps(BalancyWebViewPlugin, '$BalancyWebViewState');
mergeInto(LibraryManager.library, BalancyWebViewPlugin);
