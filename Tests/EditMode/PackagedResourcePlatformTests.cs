using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using Balancy.WebView;

namespace Balancy.Tests
{
    public class PackagedResourcePlatformTests
    {
        private static readonly Type FileManager = typeof(Controller).Assembly.GetType("Balancy.UnityFileManager", true);
        private static readonly MethodInfo GetAccess = FileManager.GetMethod(
            "GetPackagedResourceAccess", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo RequiresCopy = FileManager.GetMethod(
            "RequiresStartupCopy", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo IsSynchronous = FileManager.GetMethod(
            "IsWebGlSynchronousContent", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ShouldHydrate = FileManager.GetMethod(
            "ShouldHydrateWebGlContent", BindingFlags.Static | BindingFlags.NonPublic);

        [TestCase(RuntimePlatform.Android, "AndroidAssetManager")]
        [TestCase(RuntimePlatform.IPhonePlayer, "DirectFileSystem")]
        [TestCase(RuntimePlatform.OSXPlayer, "DirectFileSystem")]
        [TestCase(RuntimePlatform.WebGLPlayer, "WebGlHydration")]
        public void PlatformUsesExpectedPackagedSnapshotProvider(RuntimePlatform platform, string expected)
        {
            Assert.That(GetAccess.Invoke(null, new object[] { platform }).ToString(), Is.EqualTo(expected));
            Assert.That(RequiresCopy.Invoke(null, new object[] { platform }), Is.False,
                "No supported player should bulk-copy StreamingAssets during startup");
        }

        [TestCase("test_versions.json", true)]
        [TestCase("Cache/do_versions_512.json", true)]
        [TestCase("Cache/Files/scripts_combined_dev_v12.js", true)]
        [TestCase("Views/store/index.html", true)]
        [TestCase("Images/icon.png", false)]
        [TestCase("Views/store.zip", false)]
        [TestCase("Audio/theme.mp3", false)]
        [TestCase("Fonts/font.woff2", false)]
        public void WebGlHydratesOnlyFilesNeededBySynchronousCoreReads(string path, bool expected)
        {
            Assert.That(IsSynchronous.Invoke(null, new object[] { path }), Is.EqualTo(expected));
        }

        [Test]
        public void WebGlCombinedBundleSuppressesLegacyScriptsButKeepsExistenceIndex()
        {
            Assert.That(ShouldHydrate.Invoke(null,
                new object[] { "Cache/Files/legacy.js", true }), Is.False);
            Assert.That(ShouldHydrate.Invoke(null,
                new object[] { "Cache/Files/legacy.js", false }), Is.True);
            Assert.That(ShouldHydrate.Invoke(null,
                new object[] { "Cache/Files/scripts_combined_dev_v12.js", true }), Is.True);
            Assert.That(ShouldHydrate.Invoke(null,
                new object[] { "Images/icon.png", true }), Is.False);
            Assert.That(ShouldHydrate.Invoke(null,
                new object[] { "balancy-webview-bridge.js", true }), Is.False);
        }

        [Test]
        public void AndroidAssetManagerInteropContractCarriesVmAndAssetManager()
        {
            var general = typeof(Controller).Assembly.GetType("Balancy.LibraryMethods+General", true);
            var method = general.GetMethod(
                "balancyInitUnityFileHelperAndroidWithAssetManager", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[]
            {
                typeof(string), typeof(string), typeof(string), typeof(IntPtr), typeof(IntPtr)
            }));
            var import = method.GetCustomAttribute<DllImportAttribute>();
            Assert.That(import, Is.Not.Null);
            Assert.That(import.CallingConvention, Is.EqualTo(CallingConvention.Cdecl));
        }

        [Test]
        public void AndroidPackagedWebViewPathStaysInsideApk()
        {
            Assert.That(BalancyWebView.ToWebViewUrl("/android_asset/Balancy/Images/icon.png"),
                Is.EqualTo("file:///android_asset/Balancy/Images/icon.png"));
        }

        [Test]
        public void IosVirtualUrlsKeepPersistentAndStreamingRootsDistinct()
        {
            const string persistent = "/var/mobile/Containers/Data/Application/ABC/Documents";
            const string streaming = "/var/containers/Bundle/Application/XYZ/Game.app/Data/Raw";

            Assert.That(BalancyWebView.TryMakeIosLocalUrl(
                persistent + "/Balancy/Cache/script v2.js", persistent, "persistent", out var persistentUrl), Is.True);
            Assert.That(persistentUrl,
                Is.EqualTo("balancy-local://local/persistent/Balancy/Cache/script%20v2.js"));

            Assert.That(BalancyWebView.TryMakeIosLocalUrl(
                streaming + "/Balancy/Images/icon.png", streaming, "streaming", out var streamingUrl), Is.True);
            Assert.That(streamingUrl,
                Is.EqualTo("balancy-local://local/streaming/Balancy/Images/icon.png"));

            Assert.That(BalancyWebView.TryMakeIosLocalUrl(
                "/tmp/outside.txt", streaming, "streaming", out _), Is.False);
            Assert.That(BalancyWebView.GetIosBridgeUrl(),
                Is.EqualTo("balancy-local://local/resources/balancy-webview-bridge.js"));
        }
    }
}
