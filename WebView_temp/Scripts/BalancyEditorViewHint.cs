#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Balancy.WebView
{
    public class BalancyEditorViewHint : MonoBehaviour
    {
        private string _message;
        private string _buttonText;
        private Action _clickCallback;

        public static void ShowUIMessage(string message, string buttonText, Action clickCallback = null)
        {
            var go = new GameObject("BalancyEditorViewHint");
            var script = go.AddComponent<BalancyEditorViewHint>();
            script._message = message;
            script._buttonText = buttonText;
            script._clickCallback = clickCallback;
        }

        private void OnGUI()
        {
            Color originalColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", GUI.skin.button);
            GUI.color = originalColor;
            
            int btnWidth = Screen.width / 4;
            int btnHeight = btnWidth / 4;
            int centerX = Screen.width / 2;
            int centerY = Screen.height / 2;

            int fontSize = Screen.width / 30;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = fontSize;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Label(
                new Rect(50, centerY - btnHeight * 3f, Screen.width - 100, 2 * btnHeight),
                _message,
                labelStyle
            );

            if (GUI.Button(new Rect(centerX - btnWidth / 2, centerY - btnHeight / 2, btnWidth, btnHeight), _buttonText))
            {
                Destroy(gameObject);
                _clickCallback?.Invoke();
            }
        }

    }
}
#endif