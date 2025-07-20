using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Balancy.WebView
{
    public class MetalWebViewRenderer : MonoBehaviour
    {
        private const string PLUGIN_NAME = "libBalancyWebViewMac";

        [DllImport(PLUGIN_NAME)]
        private static extern int _balancyIsGraphicsInitialized();
        
        [DllImport(PLUGIN_NAME)]
        private static extern void _balancySetDestinationTexture(IntPtr texturePtr, int width, int height);

        [DllImport(PLUGIN_NAME)]
        private static extern IntPtr GetRenderEventFunc();

        [DllImport(PLUGIN_NAME)]
        private static extern bool _balancyOpenWebViewEmbedded(string url, int width, int height);

        [DllImport(PLUGIN_NAME)]
        private static extern void _balancyCloseWebViewEmbedded();
        
        [DllImport(PLUGIN_NAME)]
        private static extern void _balancyTriggerRender();

        public int textureWidth = 1024;
        public int textureHeight = 768;
        // public string initialUrl = "https://unity.com";

        private Texture2D webViewTexture;
        private CommandBuffer renderCommandBuffer;

        /// <summary>
        /// Called from native code to log messages to Unity console
        /// This method is called via UnitySendMessage from the native plugin
        /// </summary>
        /// <param name="message">The log message from native code</param>
        public void LogFromNative(string message)
        {
            Debug.Log($"[BalancyWebView Native] {message}");
        }

        void Start()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal)
            {
                Debug.LogError("Metal WebView Renderer requires the Metal graphics API on macOS.");
                this.enabled = false;
                return;
            }

            // We no longer call an Initialize method.
            // The native plugin initializes itself on load.
            // CreateTextureAndCommandBuffer();

            StartCoroutine(Waait());
            // Open the webview
            // _balancyOpenWebViewEmbedded(initialUrl, textureWidth, textureHeight);
        }

        private IEnumerator Waait()
        {
            while (_balancyIsGraphicsInitialized() == 0)
            {
                yield return null; // Wait for the next frame
            }
            CreateTextureAndCommandBuffer();
        }

        private void CreateTextureAndCommandBuffer()
        {
            Debug.LogWarning("CreateTextureAndCommandBuffer");
            // 1. Create the destination texture
            bool useLinearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
            webViewTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.BGRA32, false, useLinearColorSpace);

            // Apply this texture to a material on a Quad
            var renderer = gameObject.GetComponent<RawImage>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.mainTexture = webViewTexture;
            }
            else
            {
                Debug.LogWarning("MetalWebViewRenderer: No Renderer or Material found on this GameObject.");
            }

            // 2. Pass the native texture pointer to the plugin. This is still necessary.
            _balancySetDestinationTexture(webViewTexture.GetNativeTexturePtr(), textureWidth, textureHeight);
            
            Debug.LogWarning("_balancySetDestinationTexture: " + textureWidth + " => " + textureHeight);

            // 3. Setup the command buffer to call our native render function. This is unchanged.
            renderCommandBuffer = new CommandBuffer();
            renderCommandBuffer.name = "WebViewRender";
            renderCommandBuffer.IssuePluginEvent(GetRenderEventFunc(), 1);

            if (Camera.main != null)
            {
                Camera.main.AddCommandBuffer(CameraEvent.AfterForwardOpaque, renderCommandBuffer);
            }
            else
            {
                Debug.LogError("MetalWebViewRenderer: No main camera found. Cannot add CommandBuffer.");
            }
        }
        
        void Update()
        {
            // Every frame, we tell the native plugin to take a snapshot.
            // This is the reliable trigger that the NSTimer failed to be.
            // _balancyTriggerRender();
        }

        void OnDestroy()
        {
            if (renderCommandBuffer != null && Camera.main != null)
            {
                Camera.main.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, renderCommandBuffer);
            }

            if (webViewTexture != null)
            {
                Destroy(webViewTexture);
            }

            // _balancyCloseWebViewEmbedded();
        }
    }
}