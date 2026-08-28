using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Balancy.Tests
{
    public class TasksLifecycleTests
    {
        private static readonly IList ActiveTasks = (IList)typeof(Tasks)
            .GetField("_activeTasks", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly object ActiveTasksLock = typeof(Tasks)
            .GetField("_activeTasksLock", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly MethodInfo StopAllTasks = typeof(Tasks)
            .GetMethod("StopAllTasks", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearPendingActions = typeof(UnityMainThreadDispatcher)
            .GetMethod("ClearPendingActions", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ProcessQueue = typeof(UnityMainThreadDispatcher)
            .GetMethod("ProcessQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MainThreadId = typeof(UnityMainThreadDispatcher)
            .GetField("_mainThreadId", BindingFlags.Static | BindingFlags.NonPublic);

        private GameObject _gameObject;
        private UnityMainThreadDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            StopAllTasks.Invoke(null, null);
            ClearPendingActions.Invoke(null, null);
            MainThreadId.SetValue(null, -1);
            _gameObject = new GameObject("Balancy tasks test dispatcher");
            _dispatcher = _gameObject.AddComponent<UnityMainThreadDispatcher>();
        }

        [TearDown]
        public void TearDown()
        {
            StopAllTasks.Invoke(null, null);
            ClearPendingActions.Invoke(null, null);
            MainThreadId.SetValue(null, -1);
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void NonPositiveWaitRunsSynchronouslyWithoutAllocatingTask()
        {
            var calls = 0;

            var token = Tasks.Wait(0, () => calls++);

            Assert.That(token, Is.Null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(ActiveTaskCount(), Is.Zero);
        }

        [Test]
        public void CancellingAfterWaitWasQueuedStillSuppressesItsCallback()
        {
            var calls = 0;
            var token = Tasks.Wait(0.01f, () => calls++);
            Assert.That(SpinWait.SpinUntil(() => ActiveTaskCount() == 0, 2000), Is.True,
                "wait operation did not finish in time");

            Tasks.StopTaskRemotely(token);
            ProcessQueue.Invoke(_dispatcher, null);

            Assert.That(calls, Is.Zero);
            Assert.DoesNotThrow(() => Tasks.StopTaskRemotely(token));
        }

        [Test]
        public void CancellingCompletedPeriodicBeforeQueuePumpSuppressesTicksAndDone()
        {
            var tickCalls = 0;
            var doneCalls = 0;
            var token = Tasks.Periodic(0.02f, 0.01f, _ => tickCalls++, () => doneCalls++);
            Assert.That(tickCalls, Is.EqualTo(1), "the initial tick is intentionally synchronous");
            Assert.That(SpinWait.SpinUntil(() => ActiveTaskCount() == 0, 2000), Is.True,
                "periodic operation did not finish in time");

            Tasks.StopTaskRemotely(token);
            ProcessQueue.Invoke(_dispatcher, null);

            Assert.That(tickCalls, Is.EqualTo(1));
            Assert.That(doneCalls, Is.Zero);
        }

        [Test]
        public void StopAllTasksCancelsEveryTrackedOperationAndIsIdempotent()
        {
            var first = Tasks.Wait(10, () => Assert.Fail("cancelled wait must not run"));
            var second = Tasks.Periodic(10, _ => { });
            Assert.That(ActiveTaskCount(), Is.EqualTo(2));

            Assert.DoesNotThrow(() => StopAllTasks.Invoke(null, null));
            Assert.That(ActiveTaskCount(), Is.Zero);
            Assert.That(first.IsCancellationRequested, Is.True);
            Assert.That(second.IsCancellationRequested, Is.True);
            Assert.DoesNotThrow(() => StopAllTasks.Invoke(null, null));
        }

        private static int ActiveTaskCount()
        {
            lock (ActiveTasksLock)
                return ActiveTasks.Count;
        }
    }
}
