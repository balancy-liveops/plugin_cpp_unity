using UnityEngine;
using UnityEditor;
using Balancy.Editor;
using System.Reflection;

namespace Balancy.Editor
{
    [InitializeOnLoad]
    public static class BalancyPlayModeHandler
    {
        private static bool _isCleaningUp = false;

        // Static constructor that gets called when Unity loads/reloads scripts
        static BalancyPlayModeHandler()
        {
            // Unsubscribe first to avoid multiple registrations
            EditorApplication.update -= CheckPlayModeChange;
            EditorApplication.update += CheckPlayModeChange;
        }
        
        private static bool _wasInEditMode = true;
        
        // Check for play mode changes by polling rather than using events
        private static void CheckPlayModeChange()
        {
            bool isInEditMode = !EditorApplication.isPlayingOrWillChangePlaymode;
            
            // Detect transition from edit mode to play mode
            if (_wasInEditMode && !isInEditMode && !_isCleaningUp)
            {
                _isCleaningUp = true;
                try
                {
                    // Only clean up if a BalancyConfigEditor window is open
                    if (HasOpenBalancyConfigWindow())
                    {
                        Debug.Log("Balancy Play Mode Handler: Cleaning up before entering Play mode");
                        PerformCleanup();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error during Balancy cleanup: {e}");
                }
                finally
                {
                    _isCleaningUp = false;
                }
            }
            
            // Reset when going back to edit mode
            if (!_wasInEditMode && isInEditMode)
            {
                // Reset cleanup flag
                BalancyConfigEditor.ResetCleanupFlag();
            }
            
            _wasInEditMode = isInEditMode;
        }
        
        // Perform cleanup directly without using EditorApplication events
        private static void PerformCleanup()
        {
            BalancyConfigEditor.CloseAllWindowsAndCleanup();
        }
        
        // Check if any BalancyConfigEditor windows are open
        private static bool HasOpenBalancyConfigWindow()
        {
            return Resources.FindObjectsOfTypeAll<BalancyConfigEditor>().Length > 0;
        }
    }
}
