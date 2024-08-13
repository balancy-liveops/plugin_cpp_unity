using UnityEngine;

namespace Balancy
{
    internal class UnityFileManager
    {
        public static void Init()
        {
            // Debug.Log("Application.persistentDataPath: " + Application.persistentDataPath);
            Balancy.LibraryMethods.General.balancyInitUnityFileHelper(Application.persistentDataPath, Application.dataPath, LoadFromResources);
        }

        private static string LoadFromResources(string path)
        {
            var textFile = Resources.Load<TextAsset>(path);
            // Debug.LogError($"LoadFromResources > {path} > {textFile}");
            return textFile != null ? textFile.text : string.Empty;
        }
    }
}
