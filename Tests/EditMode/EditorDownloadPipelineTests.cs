using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Balancy.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Balancy.Tests
{
    public class EditorDownloadPipelineTests
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private string _root;

        // Model an idle Unity player loop: posted synchronization-context work is
        // deliberately never executed. Editor downloads must still finish their I/O.
        private sealed class IdlePlayerLoopContext : SynchronizationContext
        {
            public int Posts;
            public override void Post(SendOrPostCallback callback, object state) => Interlocked.Increment(ref Posts);
        }

        private static void ClearQueue() => typeof(UnityMainThreadDispatcher)
            .GetMethod("ClearPendingActions", StaticPrivate).Invoke(null, null);

        private static int QueueCount()
        {
            var queue = (ICollection)typeof(UnityMainThreadDispatcher)
                .GetField("_executionQueue", StaticPrivate).GetValue(null);
            lock (queue) return queue.Count;
        }

        [SetUp]
        public void SetUp()
        {
            ClearQueue();
            typeof(UnzipBridge).GetMethod("StartSession", StaticPrivate).Invoke(null, null);
            _root = Path.Combine(Path.GetTempPath(), "balancy-editor-download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            UnzipBridge.Cleanup();
            ClearQueue();
            Directory.Delete(_root, true);
        }

        private string CreateArchive(string entryName = "manifest.json")
        {
            var path = Path.Combine(_root, "view.zip");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(entryName);
                entry.LastWriteTime = new DateTimeOffset(2020, 1, 2, 3, 4, 6, TimeSpan.Zero);
                using (var writer = new StreamWriter(entry.Open())) writer.Write("{\"assets\":[]}");
            }
            return path;
        }

        [Test]
        public void EditorExtractionFinishesWithIdlePlayerLoopAndKeepsCurrentFileTimestamp()
        {
            var archive = CreateArchive();
            var context = new IdlePlayerLoopContext();
            var previous = SynchronizationContext.Current;
            var started = DateTime.UtcNow;
            Task extraction;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                extraction = (Task)typeof(UnzipBridge).GetMethod("UnzipInEditor", StaticPrivate)
                    .Invoke(null, new object[] { "test-view", archive, UnzipGeneration() });
                Assert.That(extraction.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "Extraction must finish without a coroutine or player-loop tick");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }

            var manifest = Path.Combine(_root, "view", "manifest.json");
            Assert.That(File.ReadAllText(manifest), Is.EqualTo("{\"assets\":[]}"));
            Assert.That(File.GetLastWriteTimeUtc(manifest), Is.GreaterThanOrEqualTo(started.AddSeconds(-2)));
            Assert.That(context.Posts, Is.Zero);
            Assert.That(QueueCount(), Is.EqualTo(1), "Native completion is queued for the editor update thread");
        }

        [Test]
        public void ExtractionStillRejectsEntriesOutsideTheViewDirectory()
        {
            var archive = CreateArchive("../escape.json");
            var result = typeof(UnzipBridge).GetMethod("ExtractFileOnWorker", StaticPrivate)
                .Invoke(null, new object[] { archive, Path.Combine(_root, "view"), CancellationToken.None });
            Assert.That(result.GetType().GetField("Success", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(result), Is.False);
            Assert.That(File.Exists(Path.Combine(_root, "escape.json")), Is.False);
        }

        [Test]
        public void DownloadProgressRepaintsWithoutImportingHalfWrittenAssets()
        {
            var type = Type.GetType("Balancy.Editor.BalancyConfigEditor, Balancy.Editor", true);
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var marker = "Assets/BalancyProgressImportProbe-" + Guid.NewGuid().ToString("N") + ".txt";
            try
            {
                type.GetMethod("BeginAssetWrite", flags).Invoke(window, null);
                type.GetField("_downloading", flags).SetValue(window, true);
                File.WriteAllText(marker, "still writing assets");
                for (int i = 0; i < 20; ++i)
                {
                    type.GetMethod("OnProgressUpdate", flags).Invoke(window, new object[] { "Assets " + i, 0.8f });
                    type.GetMethod("update", flags).Invoke(window, null);
                }
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(marker), Is.Null,
                    "Progress updates must not import assets while the download is writing them");
                type.GetField("_downloading", flags).SetValue(window, false);
                type.GetField("_assetsNeedRefresh", flags).SetValue(window, true);
                type.GetMethod("EndAssetWrite", flags).Invoke(window, null);
                type.GetMethod("update", flags).Invoke(window, null);
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(marker), Is.Not.Null,
                    "Completed writes must still be imported once");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                AssetDatabase.DeleteAsset(marker);
                if (File.Exists(marker)) File.Delete(marker);
            }
        }

        private static int UnzipGeneration() => (int)typeof(UnzipBridge)
            .GetField("_generation", StaticPrivate).GetValue(null);

        [TestCase(false)]
        [TestCase(true)]
        public void CancelOrCloseWaitsForFinalWriteBeforeAllowingImport(bool closeWindow)
        {
            var type = Type.GetType("Balancy.Editor.BalancyConfigEditor, Balancy.Editor", true);
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var blocked = type.GetField("_autoRefreshBlocked", flags);
            var started = new ManualResetEventSlim(false);
            var marker = Path.Combine(_root, "last-write.txt");
            bool importBlockedDuringFinalWrite = false;
            try
            {
                type.GetMethod("BeginAssetWrite", flags).Invoke(window, null);
                type.GetField("_downloading", flags).SetValue(window, true);
                var cancellation = (CancellationTokenSource)typeof(UnzipBridge)
                    .GetField("_extractionCancellation", StaticPrivate).GetValue(null);
                var token = cancellation.Token;
                var writer = Task.Run(() =>
                {
                    started.Set();
                    if (!token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("Cancel did not reach the writer");
                    // Model the filesystem work still finishing after cancellation.
                    importBlockedDuringFinalWrite = (bool)blocked.GetValue(window);
                    File.WriteAllText(marker, "finished");
                });
                var sync = typeof(UnzipBridge).GetField("ExtractionLock", StaticPrivate).GetValue(null);
                var workers = (System.Collections.Generic.List<Task>)typeof(UnzipBridge)
                    .GetField("ExtractionTasks", StaticPrivate).GetValue(null);
                lock (sync) workers.Add(writer);
                Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);

                if (closeWindow) UnityEngine.Object.DestroyImmediate(window);
                else type.GetMethod("CancelDownload", flags).Invoke(window, null);

                Assert.That(writer.IsCompleted, Is.True, "Cancel/close must join actual writers");
                Assert.That(importBlockedDuringFinalWrite, Is.True,
                    "Import must stay blocked while a canceled worker can still write");
                Assert.That((bool)blocked.GetValue(window), Is.False);
                Assert.That(File.ReadAllText(marker), Is.EqualTo("finished"));
                Assert.That(type.GetField("_downloading", flags).GetValue(window), Is.False);
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                started.Dispose();
            }
        }

        [Test]
        public void CancelBalancesImportEvenWhenACompletedWorkerFailed()
        {
            var type = Type.GetType("Balancy.Editor.BalancyConfigEditor, Balancy.Editor", true);
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                type.GetMethod("BeginAssetWrite", flags).Invoke(window, null);
                type.GetField("_downloading", flags).SetValue(window, true);
                var workers = (System.Collections.Generic.List<Task>)typeof(UnzipBridge)
                    .GetField("ExtractionTasks", StaticPrivate).GetValue(null);
                var failure = new TaskCompletionSource<bool>();
                failure.SetException(new IOException("Failed writer"));
                workers.Add(failure.Task);
                Assert.Throws<TargetInvocationException>(() => type.GetMethod("CancelDownload", flags).Invoke(window, null));
                Assert.That(type.GetField("_autoRefreshBlocked", flags).GetValue(window), Is.False);
                Assert.That(type.GetField("_downloading", flags).GetValue(window), Is.False);
                Assert.That(workers.Count, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void CleanupCancelsExtractionWaitingForTheZipGate()
        {
            var archive = CreateArchive();
            var gate = (SemaphoreSlim)typeof(UnzipBridge).GetField("UnzipGate", StaticPrivate).GetValue(null);
            gate.Wait();
            try
            {
                var extraction = (Task)typeof(UnzipBridge).GetMethod("UnzipInEditor", StaticPrivate)
                    .Invoke(null, new object[] { "canceled-view", archive, UnzipGeneration() });
                UnzipBridge.Cleanup();
                Assert.That(extraction.Wait(TimeSpan.FromSeconds(2)), Is.True,
                    "Cleanup must cancel workers waiting on the gate without a Unity tick");
                Assert.That(Directory.Exists(Path.Combine(_root, "view")), Is.False);
            }
            finally { gate.Release(); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldRuntimeCoroutineStopsAfterSessionRestart(bool completedAfterRestart)
        {
            var resultType = typeof(UnzipBridge).GetNestedType("ExtractResult", BindingFlags.NonPublic);
            var completionType = typeof(TaskCompletionSource<>).MakeGenericType(resultType);
            var completion = Activator.CreateInstance(completionType);
            var task = completionType.GetProperty("Task").GetValue(completion);
            var coroutine = (IEnumerator)typeof(UnzipBridge).GetMethod("AwaitExtraction", StaticPrivate)
                .Invoke(null, new[] { (object)"old-view", task, UnzipGeneration() });
            Assert.That(coroutine.MoveNext(), Is.True, "The current session waits for extraction");
            typeof(UnzipBridge).GetMethod("StartSession", StaticPrivate).Invoke(null, null);
            if (completedAfterRestart)
                completionType.GetMethod("SetResult").Invoke(completion, new[] { Activator.CreateInstance(resultType, true) });
            Assert.That(coroutine.MoveNext(), Is.False,
                "A previous session must exit before waiting or notifying the new native session");
        }

        [Test]
        public void OldWebGLReplyCannotCompleteANewSessionRequest()
        {
            var requests = (System.Collections.Generic.Dictionary<string, Action<string>>)typeof(UnzipBridge)
                .GetField("_pendingRequests", StaticPrivate).GetValue(null);
            int oldCalls = 0, newCalls = 0;
            requests["old-session-request"] = path => ++oldCalls;
            typeof(UnzipBridge).GetMethod("StartSession", StaticPrivate).Invoke(null, null);
            requests["new-session-request"] = path => ++newCalls;
            var reply = typeof(UnzipBridge).GetMethod("CompleteWebGLReply", StaticPrivate);
            reply.Invoke(null, new object[] { "old-session-request|/old/" });
            reply.Invoke(null, new object[] { "new-session-request|/new/" });
            reply.Invoke(null, new object[] { "new-session-request|/new/" });
            Assert.That(oldCalls, Is.Zero);
            Assert.That(newCalls, Is.EqualTo(1));
        }

        private sealed class CancelOnReadStream : MemoryStream
        {
            private readonly CancellationTokenSource _cancellation;
            public CancelOnReadStream(CancellationTokenSource cancellation) : base(new byte[256 * 1024])
            { _cancellation = cancellation; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = base.Read(buffer, offset, count);
                _cancellation.Cancel();
                return read;
            }
        }

        [Test]
        public void CancellationInterruptsCopyInsideALargeZipEntry()
        {
            using (var cancellation = new CancellationTokenSource())
            using (var input = new CancelOnReadStream(cancellation))
            using (var output = new MemoryStream())
            {
                var copy = typeof(UnzipBridge).GetMethod("CopyWithCancellation", StaticPrivate);
                var exception = Assert.Throws<TargetInvocationException>(() => copy.Invoke(null,
                    new object[] { input, output, cancellation.Token }));
                Assert.That(exception.InnerException, Is.InstanceOf<OperationCanceledException>());
                Assert.That(output.Length, Is.Zero, "Canceled data must not continue writing the entry");
            }
        }

        [Test]
        public void EditorCompletionDropsStoppedAndPreviousSessions()
        {
            var stopped = typeof(UnityWebRequestBridge).GetField("_isStopped", StaticPrivate);
            var generation = (int)typeof(UnityWebRequestBridge).GetField("_generation", StaticPrivate).GetValue(null);
            var enqueue = typeof(UnityWebRequestBridge).GetMethod("QueueEditorCompletion", StaticPrivate);
            var original = stopped.GetValue(null);
            var queue = (System.Collections.Generic.Queue<Action>)typeof(UnityMainThreadDispatcher)
                .GetField("_executionQueue", StaticPrivate).GetValue(null);
            int calls = 0;
            Action callback = () => ++calls;
            try
            {
                stopped.SetValue(null, false);
                enqueue.Invoke(null, new object[] { generation - 1, callback });
                enqueue.Invoke(null, new object[] { generation, callback });
                queue.Dequeue().Invoke();
                queue.Dequeue().Invoke();
                Assert.That(calls, Is.EqualTo(1));
                enqueue.Invoke(null, new object[] { generation, callback });
                stopped.SetValue(null, true);
                queue.Dequeue().Invoke();
                Assert.That(calls, Is.EqualTo(1));
            }
            finally { stopped.SetValue(null, original); }
        }

        [Test]
        public void EditorHttpResponseArrivesWithoutPumpingThePlayerLoop()
        {
            var server = new TcpListener(IPAddress.Loopback, 0);
            server.Start();
            var port = ((IPEndPoint)server.LocalEndpoint).Port;
            var releaseResponse = new ManualResetEventSlim(false);
            var served = Task.Run(async () =>
            {
                using (var client = await server.AcceptTcpClientAsync().ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[4096];
                    await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (!releaseResponse.Wait(TimeSpan.FromSeconds(5))) return;
                    var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nok");
                    await stream.WriteAsync(response, 0, response.Length).ConfigureAwait(false);
                }
            });
            var gameObject = new GameObject("Editor download contract test");
            var bridge = gameObject.AddComponent<UnityWebRequestBridge>();
            var context = new IdlePlayerLoopContext();
            var previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                typeof(UnityWebRequestBridge).GetMethod("ProcessFileLoadWithHttpClient", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(bridge, new object[] { -123, "http://127.0.0.1:" + port + "/asset", 3 });
                releaseResponse.Set();
                Assert.That(SpinWait.SpinUntil(() => QueueCount() == 1, TimeSpan.FromSeconds(5)), Is.True,
                    "HTTP completion must reach the editor queue even if Unity's player loop is idle");
                Assert.That(served.Wait(TimeSpan.FromSeconds(1)), Is.True);
                Assert.That(context.Posts, Is.Zero);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
                releaseResponse.Set();
                server.Stop();
                // This fixture never registers a real native bridge.
                typeof(UnityWebRequestBridge).GetField("_resourcesCleaned", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(bridge, true);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
