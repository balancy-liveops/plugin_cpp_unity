#if UNITY_EDITOR && !BALANCY_SERVER
using UnityEditor;
using UnityEngine;

namespace Balancy.Editor
{
    /// <summary>
    /// Minimal authentication wrapper for addressables sync.
    /// Stores the S2S private key in EditorPrefs for persistence across sessions.
    /// </summary>
    public class Balancy_EditorAuth
    {
        private const string PRIVATE_KEY_PREF = "Balancy_S2S_PrivateKey";

        public string GetPrivateKey()
        {
            return EditorPrefs.GetString(PRIVATE_KEY_PREF, "");
        }

        public void SetPrivateKey(string privateKey)
        {
            EditorPrefs.SetString(PRIVATE_KEY_PREF, privateKey);
        }

        public bool HasPrivateKey()
        {
            return !string.IsNullOrEmpty(GetPrivateKey());
        }
    }
}
#endif
