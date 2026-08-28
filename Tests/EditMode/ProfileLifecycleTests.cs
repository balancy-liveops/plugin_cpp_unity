using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Balancy.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class ProfileLifecycleTests
    {
        private sealed class TestProfile : ParentBaseData
        {
            public int InitCalls { get; private set; }
            public bool ThrowOnInit { get; set; }
            public override void InitData()
            {
                InitCalls++;
                if (ThrowOnInit)
                    throw new InvalidOperationException("profile init failure");
            }
        }

        private static readonly FieldInfo CachedProfiles = typeof(Profiles)
            .GetField("_cachedProfiles", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MainThreadId = typeof(Profiles)
            .GetField("_mainThreadId", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ProfileReset = typeof(Profiles)
            .GetMethod("ProfileReset", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo DataDestroyed = typeof(Profiles)
            .GetMethod("OnBaseDataDestroyed", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ParamChanged = typeof(Profiles)
            .GetMethod("OnBaseDataParamChanged", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo OnResetComplete = typeof(Profiles)
            .GetMethod("OnResetComplete", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo AddParamSubscription = typeof(Profiles)
            .GetMethod("AddDataSubscription", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo AddWildcardSubscription = typeof(Profiles)
            .GetMethod("AddWildcardSubscription", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo UserResetCallbacks = typeof(Profiles)
            .GetField("_userResetCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo ResetInProgress = typeof(Profiles)
            .GetField("_resetInProgress", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo AllSubscriptions = typeof(Profiles)
            .GetField("AllBaseDataSubscriptions", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ProcessQueue = typeof(UnityMainThreadDispatcher)
            .GetMethod("ProcessQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearPendingActions = typeof(UnityMainThreadDispatcher)
            .GetMethod("ClearPendingActions", BindingFlags.Static | BindingFlags.NonPublic);

        private GameObject _gameObject;
        private UnityMainThreadDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            ClearState();
            MainThreadId.SetValue(null, Thread.CurrentThread.ManagedThreadId);
            _gameObject = new GameObject("Balancy profile lifecycle test");
            _dispatcher = _gameObject.AddComponent<UnityMainThreadDispatcher>();
        }

        [TearDown]
        public void TearDown()
        {
            ClearState();
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ProfileResetKeepsManagedIdentityAndRebindsNativePointer()
        {
            var profile = CacheProfile("TestProfile", new IntPtr(101));

            ProfileReset.Invoke(null, new object[] { "TestProfile", new IntPtr(202) });

            Assert.That(GetCached("TestProfile"), Is.SameAs(profile));
            Assert.That(profile.Equals(new IntPtr(202)), Is.True);
            Assert.That(profile.InitCalls, Is.EqualTo(2));
        }

        [Test]
        public void WorkerThreadProfileResetIsDeferredUntilMainThreadPump()
        {
            var profile = CacheProfile("TestProfile", new IntPtr(301));
            Exception failure = null;
            var worker = new Thread(() =>
            {
                try { ProfileReset.Invoke(null, new object[] { "TestProfile", new IntPtr(302) }); }
                catch (Exception exception) { failure = exception; }
            });
            worker.Start();
            worker.Join();

            Assert.That(failure, Is.Null);
            Assert.That(profile.Equals(new IntPtr(301)), Is.True);
            ProcessQueue.Invoke(_dispatcher, null);
            Assert.That(profile.Equals(new IntPtr(302)), Is.True);
            Assert.That(profile.InitCalls, Is.EqualTo(2));
        }

        [Test]
        public void NativeDestructionInvalidatesEveryWrapperImmediatelyOnWorkerThread()
        {
            var first = new TestProfile();
            var second = new TestProfile();
            var pointer = new IntPtr(401);
            first.SetData(pointer);
            second.SetData(pointer);

            var worker = new Thread(() => DataDestroyed.Invoke(null, new object[] { pointer }));
            worker.Start();
            worker.Join();

            Assert.That(first.IsValid, Is.False);
            Assert.That(second.IsValid, Is.False);
            Assert.That(first.Equals(pointer), Is.False);
            Assert.That(second.Equals(pointer), Is.False);
            ProcessQueue.Invoke(_dispatcher, null); // deferred subscription cleanup remains safe
        }

        [Test]
        public void DestroyingOldPointerAfterRefreshDoesNotInvalidateReboundProfile()
        {
            var profile = CacheProfile("TestProfile", new IntPtr(501));
            ProfileReset.Invoke(null, new object[] { "TestProfile", new IntPtr(502) });

            DataDestroyed.Invoke(null, new object[] { new IntPtr(501) });

            Assert.That(profile.IsValid, Is.True);
            Assert.That(profile.Equals(new IntPtr(502)), Is.True);
        }

        [Test]
        public void UnknownProfileResetDoesNotCreateAWrapper()
        {
            ProfileReset.Invoke(null, new object[] { "MissingProfile", new IntPtr(601) });
            Assert.That(GetCached("MissingProfile"), Is.Null);
        }

        [Test]
        public void ThrowingProfileRefreshCannotEscapeNativeCallback()
        {
            var profile = CacheProfile("TestProfile", new IntPtr(701));
            profile.ThrowOnInit = true;
            LogAssert.Expect(LogType.Exception, new Regex("profile init failure"));

            Assert.DoesNotThrow(() => ProfileReset.Invoke(null,
                new object[] { "TestProfile", new IntPtr(702) }));
            Assert.That(profile.Equals(new IntPtr(702)), Is.True);
        }

        [Test]
        public void ResetCompletionClearsStateAndIsolatesEverySubscriber()
        {
            var finishCalls = 0;
            var completionCalls = 0;
            Callbacks.OnProfileResetFinish += () => throw new InvalidOperationException("finish callback failure");
            Callbacks.OnProfileResetFinish += () => finishCalls++;
            var callbacks = (IList)UserResetCallbacks.GetValue(null);
            callbacks.Add((Action)(() => throw new InvalidOperationException("completion callback failure")));
            callbacks.Add((Action)(() => completionCalls++));
            ResetInProgress.SetValue(null, true);
            LogAssert.Expect(LogType.Exception, new Regex("finish callback failure"));
            LogAssert.Expect(LogType.Exception, new Regex("completion callback failure"));

            Assert.DoesNotThrow(() => OnResetComplete.Invoke(null, null));

            Assert.That(finishCalls, Is.EqualTo(1));
            Assert.That(completionCalls, Is.EqualTo(1));
            Assert.That(callbacks.Count, Is.Zero);
            Assert.That((bool)ResetInProgress.GetValue(null), Is.False);
        }

        [Test]
        public void ThrowingFieldSubscriberDoesNotSkipOtherOrWildcardSubscribers()
        {
            var pointer = new IntPtr(801);
            var regularCalls = 0;
            var wildcardCalls = 0;
            AddParamSubscription.Invoke(null,
                new object[] { pointer, "level", (Action)(() => throw new InvalidOperationException("field callback failure")) });
            AddParamSubscription.Invoke(null,
                new object[] { pointer, "level", (Action)(() => regularCalls++) });
            AddWildcardSubscription.Invoke(null,
                new object[] { pointer, (Action<string>)(_ => wildcardCalls++) });
            LogAssert.Expect(LogType.Exception, new Regex("field callback failure"));

            Assert.DoesNotThrow(() => ParamChanged.Invoke(null, new object[] { pointer, "level" }));
            Assert.That(regularCalls, Is.EqualTo(1));
            Assert.That(wildcardCalls, Is.EqualTo(1));
        }

        private static TestProfile CacheProfile(string name, IntPtr pointer)
        {
            var profile = new TestProfile();
            profile.SetData(pointer);
            profile.InitData();
            ((IDictionary)CachedProfiles.GetValue(null)).Add(name, profile);
            return profile;
        }

        private static object GetCached(string name)
        {
            var profiles = (IDictionary)CachedProfiles.GetValue(null);
            return profiles.Contains(name) ? profiles[name] : null;
        }

        private static void ClearState()
        {
            ClearPendingActions.Invoke(null, null);
            var profiles = (IDictionary)CachedProfiles.GetValue(null);
            foreach (DictionaryEntry entry in profiles)
                ((ParentBaseData)entry.Value).SetData(IntPtr.Zero);
            profiles.Clear();
            ((IDictionary)AllSubscriptions.GetValue(null)).Clear();
            ((IList)UserResetCallbacks.GetValue(null)).Clear();
            ResetInProgress.SetValue(null, false);
            var clearCallbacks = typeof(Callbacks).GetMethod("ClearAll", BindingFlags.Static | BindingFlags.NonPublic);
            clearCallbacks.Invoke(null, null);
        }
    }
}
