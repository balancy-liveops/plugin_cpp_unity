using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class DispatcherTests
    {
        private static readonly MethodInfo ProcessQueue = typeof(UnityMainThreadDispatcher)
            .GetMethod("ProcessQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearPendingActions = typeof(UnityMainThreadDispatcher)
            .GetMethod("ClearPendingActions", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MainThreadId = typeof(UnityMainThreadDispatcher)
            .GetField("_mainThreadId", BindingFlags.Static | BindingFlags.NonPublic);

        private GameObject _gameObject;
        private UnityMainThreadDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            ClearPendingActions.Invoke(null, null);
            MainThreadId.SetValue(null, -1);
            _gameObject = new GameObject("Balancy dispatcher test");
            _dispatcher = _gameObject.AddComponent<UnityMainThreadDispatcher>();
        }

        [TearDown]
        public void TearDown()
        {
            ClearPendingActions.Invoke(null, null);
            MainThreadId.SetValue(null, -1);
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void WorkerThreadCanQueueWithoutTouchingUnityObjects()
        {
            var calls = 0;
            Exception workerFailure = null;
            var worker = new Thread(() =>
            {
                try { UnityMainThreadDispatcher.EnqueueFromAnyThread(() => calls++); }
                catch (Exception exception) { workerFailure = exception; }
            });
            worker.Start();
            worker.Join();

            Assert.That(workerFailure, Is.Null);
            Assert.That(calls, Is.Zero);
            ProcessQueue.Invoke(_dispatcher, null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(UnityMainThreadDispatcher.IsMainThread, Is.True);
        }

        [Test]
        public void ThrowingActionDoesNotDropLaterActions()
        {
            var calls = 0;
            LogAssert.Expect(LogType.Exception, new Regex("dispatcher test failure"));
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() => throw new InvalidOperationException("dispatcher test failure"));
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() => calls++);

            ProcessQueue.Invoke(_dispatcher, null);

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantEnqueueRunsOnNextPump()
        {
            var calls = 0;
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
            {
                calls++;
                UnityMainThreadDispatcher.EnqueueFromAnyThread(() => calls++);
            });

            ProcessQueue.Invoke(_dispatcher, null);
            Assert.That(calls, Is.EqualTo(1));
            ProcessQueue.Invoke(_dispatcher, null);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void NullActionsAreIgnored()
        {
            Assert.DoesNotThrow(() => UnityMainThreadDispatcher.EnqueueFromAnyThread(null));
            Assert.DoesNotThrow(() => UnityMainThreadDispatcher.RunOnMainThreadSafe(null));
            Assert.DoesNotThrow(() => ProcessQueue.Invoke(_dispatcher, null));
        }
    }
}
