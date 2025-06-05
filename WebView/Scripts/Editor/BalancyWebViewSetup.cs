using UnityEngine;
using UnityEditor;
using Balancy.WebView;

namespace Balancy.WebView.Editor
{
    /// <summary>
    /// Editor utility to create WebView prefabs and scene setups
    /// </summary>
    public class BalancyWebViewSetup
    {
        [MenuItem("GameObject/Balancy/WebView Embedded Quad", false, 10)]
        public static void CreateEmbeddedWebViewQuad()
        {
            // Create a quad
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "WebView Embedded Quad";
            
            // Add the embedded WebView component
            var embeddedWebView = quad.AddComponent<BalancyWebViewEmbedded>();
            
            // Add the example component
            quad.AddComponent<BalancyWebViewEmbeddedExample>();
            
            // Create a material for the quad
            Material webViewMaterial = new Material(Shader.Find("Unlit/Texture"));
            webViewMaterial.name = "WebView Material";
            
            // Assign the material
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.material = webViewMaterial;
            
            // Position the quad
            quad.transform.position = new Vector3(0, 0, 5);
            quad.transform.localScale = new Vector3(5, 3, 1); // 16:9-ish ratio
            
            // Select the created object
            Selection.activeGameObject = quad;
            
            Debug.Log("Created WebView Embedded Quad. Configure the URL and texture size in the inspector.");
        }
        
        [MenuItem("GameObject/Balancy/WebView Embedded Cube", false, 11)]
        public static void CreateEmbeddedWebViewCube()
        {
            // Create a cube
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "WebView Embedded Cube";
            
            // Add the embedded WebView component
            var embeddedWebView = cube.AddComponent<BalancyWebViewEmbedded>();
            
            // Add the example component
            cube.AddComponent<BalancyWebViewEmbeddedExample>();
            
            // Create a material for the cube
            Material webViewMaterial = new Material(Shader.Find("Unlit/Texture"));
            webViewMaterial.name = "WebView Material";
            
            // Assign the material
            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.material = webViewMaterial;
            
            // Position the cube
            cube.transform.position = new Vector3(0, 0, 5);
            cube.transform.localScale = Vector3.one * 2;
            
            // Select the created object
            Selection.activeGameObject = cube;
            
            Debug.Log("Created WebView Embedded Cube. Configure the URL and texture size in the inspector.");
        }
        
        [MenuItem("GameObject/Balancy/WebView Example Scene", false, 12)]
        public static void CreateExampleScene()
        {
            // Create the main WebView object
            CreateEmbeddedWebViewQuad();
            GameObject webViewQuad = Selection.activeGameObject;
            
            // Create a camera if none exists
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObj = new GameObject("Main Camera");
                mainCamera = cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
                cameraObj.transform.position = new Vector3(0, 0, -10);
            }
            
            // Select the WebView object
            Selection.activeGameObject = webViewQuad;
            
            Debug.Log("Created WebView example scene. Use the inspector to configure the WebView settings.");
        }
    }
}
