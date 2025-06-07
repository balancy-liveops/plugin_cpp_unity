using UnityEditor;
using UnityEngine;

namespace Balancy.Editor
{
    public static class PrefabFinder
    {
        public static GameObject FindPrefabByName(string prefabName)
        {
            // Search for all prefab assets in the project
            string[] guids = AssetDatabase.FindAssets($"t:Prefab {prefabName}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab.name == prefabName)
                {
                    return prefab;
                }
            }

            Debug.LogWarning($"Prefab with name '{prefabName}' not found.");
            return null;
        }
        
        public static GameObject FindPrefabByName<T>(string prefabName) where T: MonoBehaviour
        {
            // Search for all prefab assets in the project
            string[] guids = AssetDatabase.FindAssets($"t:Prefab {prefabName}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab.name == prefabName && prefab.GetComponent<T>() != null)
                {
                    return prefab;
                }
            }

            Debug.LogWarning($"Prefab with name '{prefabName}' not found.");
            return null;
        }
    }
}
