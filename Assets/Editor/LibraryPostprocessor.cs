// using System.IO;
// using System.Linq;
// using UnityEditor;
// using UnityEngine;
//
// [InitializeOnLoad]
// public class DylibWatcher
// {
//     private static FileSystemWatcher fileWatcher;
//     private static string libraryPath = "Assets/Plugins/libBalancy.dylib";
//     private static bool isReimporting = false;
//
//     static DylibWatcher()
//     {
//         // Initialize the FileSystemWatcher
//         fileWatcher = new FileSystemWatcher
//         {
//             Path = Path.GetDirectoryName(Path.GetFullPath(libraryPath)),
//             Filter = Path.GetFileName(libraryPath),
//             NotifyFilter = NotifyFilters.LastWrite
//         };
//
//         // Add event handlers
//         fileWatcher.Changed += OnChanged;
//         fileWatcher.Created += OnChanged;
//         fileWatcher.Renamed += OnChanged;
//
//         // Begin watching
//         fileWatcher.EnableRaisingEvents = true;
//     }
//
//     private static void OnChanged(object source, FileSystemEventArgs e)
//     {
//         if (isReimporting)
//             return;
//
//         isReimporting = true;
//
//         // Log the change
//         Debug.Log($"Detected change in {libraryPath}, reimporting...");
//
//         // Delay the import to ensure the file is fully written
//         EditorApplication.delayCall += () => {
//             AssetDatabase.ImportAsset(libraryPath, ImportAssetOptions.ForceUpdate);
//             Debug.Log("libBalancy.dylib reloaded.");
//             isReimporting = false;
//         };
//     }
// }
//
// public class AssetPostprocessorScript : AssetPostprocessor
// {
//     private static bool isImporting = false;
//
//     static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
//     {
//         if (isImporting)
//             return;
//
//         foreach (string asset in importedAssets)
//         {
//             if (asset == "Assets/Plugins/libBalancy.dylib")
//             {
//                 isImporting = true;
//                 AssetDatabase.ImportAsset("Assets/Plugins/libBalancy.dylib", ImportAssetOptions.ForceUpdate);
//                 Debug.Log("libBalancy.dylib reloaded.");
//                 isImporting = false;
//                 break;
//             }
//         }
//     }
// }
