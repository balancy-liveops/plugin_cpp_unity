#!/usr/bin/env python3
"""Execute the committed message-routing methods with fake native transport, without opening Unity.
The JSON classifier is covered separately by the Unity EditMode fixture; this checks the real caller's ordering.
"""
from pathlib import Path
import argparse
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--mcs', default=shutil.which('mcs') or '/Library/Frameworks/Mono.framework/Commands/mcs')
parser.add_argument('--mono', default=shutil.which('mono') or '/Library/Frameworks/Mono.framework/Commands/mono')
args = parser.parse_args()

def method(source, signature):
    start = source.index(signature)
    at = source.index('{', start)
    depth = 1
    end = at + 1
    while depth:
        if source[end] == '{': depth += 1
        elif source[end] == '}': depth -= 1
        end += 1
    return source[start:end]

manager = (ROOT / 'Runtime/RenderViewsManager.cs').read_text()
component = (ROOT / 'WebView/Scripts/balancy-webview-c-sharp.cs').read_text()
routing = '\n'.join(method(manager, signature) for signature in [
    'private static void OnMessageReceived(string msg)',
    'private static void DispatchWebViewRequest(string msg, int? readyOwnerEpoch = null)',
    'private static void RunRequestInTheCorePlugin(string requestData)'])
component_routing = method(component, 'internal void ProcessClassicReadyRequest(Action dispatch)')
harness = r'''
using System; using System.Collections.Generic; using Balancy.WebView;
class ReadyRoutingChecks {
    const string Ready = "{\"type\":\"request\",\"id\":\"7\",\"action\":201}";
    static readonly List<string> log = new List<string>();
    static View _webView; static Func<string,bool> _onMessageReceived;
    static IntPtr m_LastOpenedOwnerPtr; static int _ownerEpoch;
    static readonly Dictionary<string,IntPtr> ViewOwners = new Dictionary<string,IntPtr>();
    static Action inCore;
    enum RequestAction { BalancyIsReady = 201 }
    class Request { public string viewId; }
    // Classifier outcomes are tested in real Unity; the boundary here supplies a known classic request.
    static class ClassicReadyRequest { public static bool IsRequest(string message,int action) { return message == Ready; } }
    class View {
        internal ClassicRevealGate _classicReveal = new ClassicRevealGate(reason => log.Add("reveal"), () => 0);
        internal bool ClassicPageReadyPending => _classicReveal.Pending;
        public void SendMessageToWebView(string message) { log.Add("rejected"); }
        COMPONENT_ROUTING
    }
    static class XsollaPurchasing {
        public static bool ReceivePresentationMessage(string message) {
            if (message != "payment-choice-fixture") return false;
            log.Add("payment"); return true;
        }
    }
    static class Debug { public static void Log(string message) {} }
    static class LibraryMethods { public static class General {
        public static void balancyWebViewRequest(IntPtr owner,string request,Action<string> callback) {
            log.Add("core:" + owner.ToInt64()); inCore?.Invoke();
        }
    } }
    static Request[] ParseBridgeRequests(string request) { return new[] { new Request { viewId = null } }; }
    static string RequestError(string request,string error) { return error; }
    static void CoreResponseCallback(string response) { throw new Exception("Unexpected error: " + response); }
    static void Reset(Action shown = null) {
        log.Clear(); inCore = null; _onMessageReceived = null; m_LastOpenedOwnerPtr = (IntPtr)111; _ownerEpoch = 0;
        _webView = new View(); _webView._classicReveal.Begin(30, shown ?? (() => log.Add("shown")));
        _webView._classicReveal.LoadCompleted();
    }
    static void Check(string name,params string[] expected) {
        if (string.Join(",",log) != string.Join(",",expected))
            throw new Exception(name + ": " + string.Join(",",log));
        Console.WriteLine("PASS " + name);
    }
    static int Main() {
        Reset(() => { log.Add("shown"); _webView._classicReveal.Cancel(); ++_ownerEpoch; m_LastOpenedOwnerPtr = (IntPtr)222; _webView._classicReveal.Begin(30); });
        OnMessageReceived(Ready);
        Check("onShown cannot change the owner of an already received ready", "core:111", "reveal", "shown");
        if (!_webView._classicReveal.Pending) throw new Exception("Replacement was revealed");

        Reset();
        inCore = () => { _webView._classicReveal.Cancel(); m_LastOpenedOwnerPtr = (IntPtr)222; _webView._classicReveal.Begin(30); _webView._classicReveal.LoadCompleted(); };
        OnMessageReceived(Ready);
        Check("core close/reopen does not reveal the replacement", "core:111");

        Reset(); _onMessageReceived = _ => false; OnMessageReceived(Ready);
        Check("application rejection still reveals the same opening", "rejected", "reveal", "shown");

        Reset(); _onMessageReceived = _ => { ++_ownerEpoch; _webView._classicReveal.Cancel(); _webView._classicReveal.Begin(30); _webView._classicReveal.LoadCompleted(); return false; };
        OnMessageReceived(Ready);
        Check("application rejection after close/reopen does not reach the replacement");

        Reset(); _onMessageReceived = _ => { ++_ownerEpoch; _webView._classicReveal.Cancel(); m_LastOpenedOwnerPtr = (IntPtr)222; _webView._classicReveal.Begin(30); _webView._classicReveal.LoadCompleted(); return true; };
        OnMessageReceived(Ready);
        Check("accepted application close/reopen does not forward the old ready");

        Reset(); OnMessageReceived("payment-choice-fixture");
        Check("payment presentation bypasses classic ready and ordinary request routing", "payment");

        Reset(); OnMessageReceived("not-ready");
        Check("non-ready request does not reveal the page", "core:111");

        Reset(); OnMessageReceived(Ready); OnMessageReceived(Ready);
        Check("duplicate ready does not repeat onShown", "core:111", "reveal", "shown", "core:111");
        return 0;
    }
    MANAGER_ROUTING
}
'''.replace('COMPONENT_ROUTING', component_routing).replace('MANAGER_ROUTING', routing)

with tempfile.TemporaryDirectory(prefix='balancy-ready-routing-') as directory:
    out = Path(directory)
    (out / 'Checks.cs').write_text(harness)
    subprocess.run([args.mcs, '-out:' + str(out / 'checks.exe'), str(out / 'Checks.cs'),
                    str(ROOT / 'WebView/Scripts/ClassicRevealGate.cs')], check=True)
    subprocess.run([args.mono, str(out / 'checks.exe')], check=True)
