using System;

namespace Balancy.WebView
{
    // The loader injected into every page after load. Independent of Unity so the generated JavaScript can be
    // executed by tests (Tests/WebView/classic-bootstrap.test.cjs and the native harnesses).
    internal static class RuntimeBootstrap
    {
        // jsString turns a value into a JavaScript string expression; the component passes its JsonUtility encoder.
        internal static string Build(Func<string, string> jsString, bool performanceEnabled, string bridgeUrl,
            string scriptsUrl, string scriptsCode, string scriptsVersion, string shellId, string owner, string settings)
        {
            string installSource = string.IsNullOrEmpty(scriptsUrl)
                ? "install(" + jsString(scriptsCode) + ");\n"
                : "var request = new XMLHttpRequest();\n" +
                  "request.open('GET', " + jsString(scriptsUrl) + ", true);\n" +
                  "request.onload = function() { if (request.status === 0 || (request.status >= 200 && request.status < 300)) install(request.responseText); else fail(new Error('Scripts file HTTP ' + request.status)); };\n" +
                  "request.onerror = function() { fail(new Error('Failed to load scripts file')); };\n" +
                  "request.send();\n";

            return
                "window.balancyPerformanceEnabled = " + (performanceEnabled ? "true" : "false") + ";\n" +
                "window.balancyShellId = " + jsString(shellId) + ";\n" +
                "window.balancyViewOwner = JSON.parse(" + jsString(owner) + ");\n" +
                "window.balancySettings = JSON.parse(" + jsString(settings) + ");\n" +
                "(function() {\n" +
                "function fail(error) { try { if (window.balancy) window.balancy._postHostError(error); } catch (_) {} console.error(error); }\n" +
                // Upgrade the native shim without losing legacy owner/transport or callbacks, including cached references.
                // Keep the real receiver first: a legacy callback must not prevent modern responses from settling.
                "function restoreLegacy(api) {\n" +
                "var previous = legacy; if (!previous || previous === api || (typeof previous !== 'object' && typeof previous !== 'function')) return; legacy = null;\n" +
                "['owner', 'onMessage', 'sendMessageToUnity'].forEach(function(name) {\n" +
                "if (name in api || (name !== 'onMessage' && !(name in previous))) return;\n" +
                "Object.defineProperty(api, name, { configurable: true, enumerable: true, get: function() { return previous[name]; }, set: function(value) { previous[name] = value; } });\n" +
                "});\n" +
                "var receive = api._receiveMessageFromUnity, legacyReceive = previous._receiveMessageFromUnity;\n" +
                "api._receiveMessageFromUnity = function(message) {\n" +
                "var result = receive.apply(this, arguments);\n" +
                "try { if (typeof legacyReceive === 'function') legacyReceive.call(previous, message); else if (typeof previous.onMessage === 'function') previous.onMessage(message); } catch (error) { console.error(error); }\n" +
                "return result;\n" +
                "};\n" +
                "}\n" +
                "function install(code) { try { window.balancy._installScripts(code, " + jsString(scriptsVersion) + "); Promise.resolve(window.balancy.initResponseHandler()).catch(fail); } catch (error) { fail(error); } }\n" +
                "function start() { try { if (!window.balancy) throw new Error('Balancy bridge did not initialize'); restoreLegacy(window.balancy); " + installSource + " } catch (error) { fail(error); } }\n" +
                // Android's onPageFinished shim creates window.balancy on classic pages; only the real bridge has _installScripts.
                "if (window.balancy && typeof window.balancy._installScripts === 'function') { start(); return; }\n" +
                "var legacy = window.balancy;\n" +
                "var bridge = document.createElement('script'); bridge.src = " + jsString(bridgeUrl) + "; bridge.onload = start; bridge.onerror = function() { fail(new Error('Failed to load Balancy bridge')); }; (document.head || document.documentElement).appendChild(bridge);\n" +
                "})();\ntrue;";
        }
    }
}
