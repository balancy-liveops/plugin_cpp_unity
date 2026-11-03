using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Balancy.Models;
using Balancy.Models.SmartObjects.Conditions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class CustomConditionSubscriptionTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const string Id = "subscription-lifecycle-probe";
        private static readonly Dictionary<string, BaseModel> Models =
            (Dictionary<string, BaseModel>)typeof(CMS).GetField("AllModels", PrivateStatic).GetValue(null);
        private static readonly FieldInfo CmsReady = typeof(CMS).GetField("IsReadyToUse", PrivateStatic);
        private static readonly FieldInfo Initialized = typeof(Controller).GetField("_isInitialized", PrivateStatic);
        private static readonly MethodInfo Subscribe = typeof(CustomConditions).GetMethod("OnSubscribeCallback", PrivateStatic);
        private static readonly MethodInfo Unsubscribe = typeof(CustomConditions).GetMethod("OnUnsubscribeCallback", PrivateStatic);
        private readonly List<ProbeCondition> _created = new List<ProbeCondition>();
        private bool _originalOptionsEnabled;
        private EnterPlayModeOptions _originalOptions;
        private SceneSetup[] _originalScenes;
        private Action<PlayModeStateChange> _editorStop;

        // Represents a game's static event and persisted local list. These must
        // survive Play Mode when Domain Reload is disabled.
        private static class Game
        {
            public static event Action Changed;
            public static readonly List<string> LocalData = new List<string>();
            public static void Fire() => Changed?.Invoke();
            public static void Reset() { Changed = null; LocalData.Clear(); }
        }

        private sealed class ProbeCondition : Custom
        {
            public int Subscribes;
            public int Unsubscribes;
            public Action AfterSubscribe;
            public Action AfterUnsubscribe;
            public override void Subscribe() { Subscribes++; Game.Changed += OnChanged; AfterSubscribe?.Invoke(); }
            public override void Unsubscribe() { Unsubscribes++; Game.Changed -= OnChanged; AfterUnsubscribe?.Invoke(); }
            private void OnChanged() => Game.LocalData.Add("entry");
        }

        [SetUp]
        public void SetUp()
        {
            Main.Stop();
            Game.Reset();
            _created.Clear();
            _originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _originalOptions = EditorSettings.enterPlayModeOptions;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
            Main.Stop();
            if (_editorStop != null)
                EditorApplication.playModeStateChanged -= _editorStop;
            _editorStop = null;
            // Also remove leaked callbacks when running against the broken SDK.
            Game.Reset();
            Models.Remove(Id);
            CmsReady.SetValue(null, false);
            EditorSettings.enterPlayModeOptions = _originalOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _originalOptionsEnabled;
            var originalScenes = _originalScenes;
            _originalScenes = null;
            if (originalScenes != null)
            {
                if (Array.Exists(originalScenes, scene => scene.isLoaded && !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private ProbeCondition StartSubscribedSession(ProbeCondition condition = null)
        {
            // Seed an active custom condition at the native-to-managed boundary.
            // The real bridge and Controller.Stop run; no cloud credentials or
            // native profile are needed to reproduce the subscription leak.
            typeof(CustomConditions).GetMethod("Register", PrivateStatic).Invoke(null, null);
            Initialized.SetValue(null, true);
            CmsReady.SetValue(null, true);
            condition = condition ?? new ProbeCondition();
            _created.Add(condition);
            Models[Id] = condition;
            Subscribe.Invoke(null, new object[] { Id });
            return condition;
        }

        [Test]
        public void StopDetachesGameEventsBeforeClearingCms()
        {
            var condition = StartSubscribedSession();
            Game.Fire();
            Assert.That(Game.LocalData.Count, Is.EqualTo(1));

            Main.Stop();
            Game.Fire();

            Assert.That(condition.Unsubscribes, Is.EqualTo(1), "Stop must call the original condition's Unsubscribe");
            Assert.That(Game.LocalData.Count, Is.EqualTo(1), "Stopped conditions must not keep changing local game data");
            Main.Stop();
            Assert.That(condition.Unsubscribes, Is.EqualTo(1), "Stop must be idempotent");
        }

        [Test]
        public void NativeUnsubscribeUsesSubscribedInstanceWhenCmsIsUnavailable()
        {
            var condition = StartSubscribedSession();
            CmsReady.SetValue(null, false);
            Models.Remove(Id);

            Unsubscribe.Invoke(null, new object[] { Id });
            Game.Fire();

            Assert.That(condition.Unsubscribes, Is.EqualTo(1));
            Assert.That(Game.LocalData, Is.Empty);
        }

        [Test]
        public void RepeatedNativeCallbacksBalanceTheSubscriptionOnce()
        {
            var condition = StartSubscribedSession();
            Subscribe.Invoke(null, new object[] { Id });
            Game.Fire();
            Assert.That(condition.Subscribes, Is.EqualTo(1));
            Assert.That(Game.LocalData.Count, Is.EqualTo(1));

            Unsubscribe.Invoke(null, new object[] { Id });
            Unsubscribe.Invoke(null, new object[] { Id });
            Main.Stop();
            Assert.That(condition.Unsubscribes, Is.EqualTo(1));
            Game.Fire();
            Assert.That(Game.LocalData.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReplacingCmsModelStillUnsubscribesTheOriginalInstance()
        {
            var original = StartSubscribedSession();
            var replacement = new ProbeCondition();
            Models[Id] = replacement;
            Unsubscribe.Invoke(null, new object[] { Id });
            Assert.That(original.Unsubscribes, Is.EqualTo(1));
            Assert.That(replacement.Unsubscribes, Is.Zero);
            Game.Fire();
            Assert.That(Game.LocalData, Is.Empty);
        }

        [Test]
        public void ReplacementSubscriptionClosesThePreviousInstance()
        {
            var original = StartSubscribedSession();
            var replacement = new ProbeCondition();
            Models[Id] = replacement;
            Subscribe.Invoke(null, new object[] { Id });
            Assert.That(original.Unsubscribes, Is.EqualTo(1));
            Assert.That(replacement.Subscribes, Is.EqualTo(1));
            Game.Fire();
            Assert.That(Game.LocalData.Count, Is.EqualTo(1));
            Main.Stop();
            Assert.That(replacement.Unsubscribes, Is.EqualTo(1));
        }

        [Test]
        public void FailedSubscribeClosesThePartiallyAddedGameHandler()
        {
            var condition = new ProbeCondition { AfterSubscribe = () => throw new InvalidOperationException("probe subscribe failed") };
            LogAssert.Expect(LogType.Error, new Regex("Exception in Subscribe.*probe subscribe failed"));
            StartSubscribedSession(condition);
            Game.Fire();
            Assert.That(condition.Unsubscribes, Is.EqualTo(1));
            Assert.That(Game.LocalData, Is.Empty);
            Main.Stop();
            Assert.That(condition.Unsubscribes, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingUnsubscribeDoesNotSkipOtherConditionsOrSubscribeDuringStop()
        {
            var first = StartSubscribedSession();
            var second = new ProbeCondition();
            Models[Id + "-second"] = second;
            Subscribe.Invoke(null, new object[] { Id + "-second" });
            first.AfterUnsubscribe = () =>
            {
                Subscribe.Invoke(null, new object[] { Id });
                Main.Stop();
                throw new InvalidOperationException("probe unsubscribe failed");
            };
            LogAssert.Expect(LogType.Error, new Regex("Exception in Unsubscribe.*probe unsubscribe failed"));
            Main.Stop();
            Game.Fire();
            Assert.That(first.Unsubscribes, Is.EqualTo(1));
            Assert.That(first.Subscribes, Is.EqualTo(1));
            Assert.That(second.Unsubscribes, Is.EqualTo(1));
            Assert.That(Game.LocalData, Is.Empty);
        }

        [Test]
        public void StopInsideSubscribeClosesThatSubscriptionOnce()
        {
            var condition = StartSubscribedSession(new ProbeCondition { AfterSubscribe = Main.Stop });
            Assert.That(condition.Unsubscribes, Is.EqualTo(1));
            Game.Fire();
            Assert.That(Game.LocalData, Is.Empty);
        }

        [Test]
        public void StopInsideReplacementUnsubscribeDoesNotStartAnotherHandler()
        {
            var original = StartSubscribedSession();
            var replacement = new ProbeCondition();
            Models[Id] = replacement;
            original.AfterUnsubscribe = Main.Stop;
            Subscribe.Invoke(null, new object[] { Id });
            Assert.That(original.Unsubscribes, Is.EqualTo(1));
            Assert.That(replacement.Subscribes, Is.Zero);
            Game.Fire();
            Assert.That(Game.LocalData, Is.Empty);
        }

        [Test]
        public void FailedNativeUnregisterStillClosesGameSubscriptions()
        {
            var condition = StartSubscribedSession();
            var unregister = typeof(CustomConditions).GetMethod("UnregisterCore", PrivateStatic);
            var failure = Assert.Throws<TargetInvocationException>(() => unregister.Invoke(null, new object[]
            {
                new Action(() => throw new InvalidOperationException("probe native unregister failed"))
            }));
            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(condition.Unsubscribes, Is.EqualTo(1));
            Game.Fire();
            Assert.That(Game.LocalData, Is.Empty);
            // The simulated failed native call did not actually detach handlers.
            // Restore the flag so TearDown performs that native cleanup too.
            typeof(CustomConditions).GetField("_registered", PrivateStatic).SetValue(null, true);
        }

        [Test]
        public void UnsubscribeRunsWhileCmsIsStillAvailable()
        {
            var condition = StartSubscribedSession();
            bool? cmsWasReady = null;
            condition.AfterUnsubscribe = () => cmsWasReady = (bool)CmsReady.GetValue(null);
            Main.Stop();
            Assert.That(cmsWasReady, Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatedPlayModeWithoutDomainReloadDoesNotMultiplyLocalData()
        {
            return VerifyPlayModeCycles(EnterPlayModeOptions.DisableDomainReload);
        }

        [UnityTest]
        public IEnumerator RepeatedPlayModeWithoutDomainOrSceneReloadDoesNotMultiplyLocalData()
        {
            return VerifyPlayModeCycles(EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload);
        }

        private IEnumerator VerifyPlayModeCycles(EnterPlayModeOptions options)
        {
            _originalScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = options;

            // Hook the SDK's actual editor stop callback, as Controller.Init does.
            // Avoid full application init so this test is independent of CDN data.
            if (!(bool)typeof(Controller).GetField("_editorCallbackRegistered", PrivateStatic).GetValue(null))
            {
                _editorStop = (Action<PlayModeStateChange>)Delegate.CreateDelegate(
                    typeof(Action<PlayModeStateChange>),
                    typeof(Controller).GetMethod("OnEditorPlayModeChanged", PrivateStatic));
                EditorApplication.playModeStateChanged += _editorStop;
            }

            for (var cycle = 0; cycle < 3; cycle++)
            {
                yield return new EnterPlayMode(expectDomainReload: false);
                Assert.That(EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload,
                    Is.EqualTo(EnterPlayModeOptions.DisableDomainReload));
                StartSubscribedSession();
                Game.Fire();
                yield return new ExitPlayMode();
                Assert.That(Initialized.GetValue(null), Is.False, "Unity must have invoked Controller's exit callback");
            }

            Debug.Log($"Custom condition probe: local entries={Game.LocalData.Count}, unsubscribes={_created.ConvertAll(c => c.Unsubscribes).FindAll(n => n == 1).Count}/3");
            Assert.That(Game.LocalData.Count, Is.EqualTo(3), "One game event per session must produce one entry, without old handlers");
            foreach (var condition in _created)
                Assert.That(condition.Unsubscribes, Is.EqualTo(1));
            Game.Fire();
            Assert.That(Game.LocalData.Count, Is.EqualTo(3), "No handlers may survive the final Play Mode exit");
        }
    }
}
