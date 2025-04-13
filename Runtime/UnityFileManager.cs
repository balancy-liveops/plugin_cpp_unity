using System;
using System.IO;
using UnityEngine;
using AOT;
using Object = UnityEngine.Object;

namespace Balancy
{
    internal class UnityFileManager
    {
        public static void Init()
        {
            // Debug.Log("Application.persistentDataPath: " + Application.persistentDataPath);
            Balancy.LibraryMethods.General.balancyInitUnityFileHelper(Application.persistentDataPath, Application.dataPath, LoadFromResources, IsFileExistsInResources);
        }

        [MonoPInvokeCallback(typeof(LibraryMethods.General.IsFileExistsCallback))]
        private static bool IsFileExistsInResources(string path)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);

            Object asset = Resources.Load(fileNameWithoutExtension);
            return asset != null;
        }

        [MonoPInvokeCallback(typeof(LibraryMethods.General.LoadFileCallback))]
        private static string LoadFromResources(string path)
        {
            try
            {
                var textFile = Resources.Load<TextAsset>(path);
                // Debug.LogError($"LoadFromResources > {path} > {textFile}");
                return textFile != null ? textFile.text : string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return string.Empty;
            }
        }
    }
}