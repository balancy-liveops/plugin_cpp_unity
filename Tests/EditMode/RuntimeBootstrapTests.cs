using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Balancy.WebView;
using NUnit.Framework;

namespace Balancy.Tests
{
    // native/classic-bootstrap.js.txt is the loader that classic-bootstrap.test.cjs and the Android harness execute;
    // the fixture test keeps it identical to what RuntimeBootstrap generates.
    public class RuntimeBootstrapTests
    {
        private const string BridgeUrl = "__BRIDGE_URL__", ScriptsUrl = "__SCRIPTS_URL__", ScriptsVersion = "golden-version",
            ShellId = "golden-shell", Owner = "{\"unnyIdGameEvent\":\"42\"}", Settings = "{\"trackWebViewEvents\":false}";

        // Same output as BalancyWebView.JsString (JsonUtility) for the inputs above; checked in the editor below.
        private static string TestJsString(string value)
        {
            var json = new StringBuilder("({\"value\":\"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\') json.Append('\\').Append(c);
                else if (c < ' ') json.Append("\\u").Append(((int)c).ToString("x4"));
                else json.Append(c);
            }
            return json.Append("\"}).value").ToString();
        }

        private static string Generate() =>
            RuntimeBootstrap.Build(TestJsString, false, BridgeUrl, ScriptsUrl, null, ScriptsVersion, ShellId, Owner, Settings);

        private static string FixturePath([CallerFilePath] string source = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source), "..", "WebView", "native", "classic-bootstrap.js.txt"));

        [Test] public void GeneratedLoaderMatchesExecutedFixture()
        {
            if (Environment.GetEnvironmentVariable("BALANCY_UPDATE_FIXTURES") == "1") File.WriteAllText(FixturePath(), Generate());
            Assert.That(Generate(), Is.EqualTo(File.ReadAllText(FixturePath()).Replace("\r\n", "\n")),
                "The page loader changed: run `python3 Tests/WebView/validate.py --update-fixtures`, then " +
                "`node --test Tests/WebView/classic-bootstrap.test.cjs` and the Android harness.");
        }

        [Test] public void LoaderTrustsOnlyABridgeThatCanInstallScripts()
        {
            // On Android classic pages onPageFinished creates window.balancy without _installScripts (the legacy shim).
            Assert.That(Generate(), Does.Contain(
                "if (window.balancy && typeof window.balancy._installScripts === 'function') { start(); return; }"));
        }

#if UNITY_EDITOR
        [Test] public void ComponentInjectsTheTestedLoader()
        {
            bool previous = BalancyWebView.PerformanceLoggingEnabled;
            try
            {
                BalancyWebView.PerformanceLoggingEnabled = false;
                Assert.That(BalancyWebView.BuildRuntimeBootstrap(BridgeUrl, ScriptsUrl, null, ScriptsVersion, ShellId, Owner, Settings),
                    Is.EqualTo(Generate()));
            }
            finally { BalancyWebView.PerformanceLoggingEnabled = previous; }
        }
#endif
    }
}
