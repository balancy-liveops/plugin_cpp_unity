#if UNITY_ANDROID
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Balancy.Editor
{
    public class BalancyManifestInjector : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        private string CombinePaths(string[] paths) {
            var path = "";
            foreach (var item in paths) {
                path = Path.Combine(path, item);
            }
            return path;
        }

        private string GetManifestFilePath(string root) {
            string[] comps = {root, "src", "main", "AndroidManifest.xml"};
            return CombinePaths(comps);
        }
        
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestFilePath = GetManifestFilePath(path);
            var manifest = new BalancyWebViewAndroidManifest(manifestFilePath);
            if (manifest.SetHardwareAccelerated())
            {
                manifest.Save();
            }
        }
    }

    internal class BalancyWebViewAndroidXmlDocument : XmlDocument
    {
        // ReSharper disable once InconsistentNaming
        private readonly string path;
        // ReSharper disable once InconsistentNaming
        protected readonly XmlNamespaceManager nameSpaceManager;
        protected const string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

        protected BalancyWebViewAndroidXmlDocument(string path)
        {
            this.path = path;
            using (var reader = new XmlTextReader(path))
            {
                reader.Read();
                // ReSharper disable once VirtualMemberCallInConstructor
                Load(reader);
            }

            nameSpaceManager = new XmlNamespaceManager(NameTable);
            nameSpaceManager.AddNamespace("android", AndroidXmlNamespace);
        }

        public void Save()
        {
            SaveAs(path);
        }

        private void SaveAs(string p)
        {
            using var writer = new XmlTextWriter(p, new UTF8Encoding(false));
            writer.Formatting = Formatting.Indented;
            Save(writer);
        }
    }

    internal class BalancyWebViewAndroidManifest : BalancyWebViewAndroidXmlDocument
    {
        public BalancyWebViewAndroidManifest(string path) : base(path)
        {

        }

        private XmlNodeList GetActivitiesWithLaunchIntent()
        {
            return SelectNodes(
                "/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN' " +
                "and intent-filter/category/@android:name='android.intent.category.LAUNCHER']", nameSpaceManager
            );
        }

        internal bool SetHardwareAccelerated()
        {
            var changed = false;
            var nodes = GetActivitiesWithLaunchIntent();
            if (nodes.Count == 0)
            {
                Debug.LogError(
                    "There is no launch intent activity in the AndroidManifest.xml." +
                    " Please check your AndroidManifest.xml file and make sure it has a main activity with intent filter");
                return false;
            }

            foreach (var node in nodes)
            {
                var activity = node as XmlElement;
                if (activity == null)
                {
                    Debug.LogError(
                        "The node item is not an XmlElement: " + node);
                    continue;
                }

                if (activity.GetAttribute("hardwareAccelerated", AndroidXmlNamespace) != "true")
                {
                    activity.SetAttribute("hardwareAccelerated", AndroidXmlNamespace, "true");
                    changed = true;
                }
            }

            return changed;
        }
    }
}
#endif