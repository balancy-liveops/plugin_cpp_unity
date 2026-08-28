using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class ScriptCompletionManagerTests
    {
        private static readonly MethodInfo OnScriptCompleted = typeof(ScriptCompletionManager)
            .GetMethod("OnScriptCompleted", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo CleanUp = typeof(ScriptCompletionManager)
            .GetMethod("CleanUp", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => CleanUp?.Invoke(null, null);

        [TearDown]
        public void TearDown() => CleanUp?.Invoke(null, null);

        [Test]
        public void CompletionCallbackRunsExactlyOnce()
        {
            var calls = 0;
            string observedPort = null;
            string observedOutputs = null;
            ScriptCompletionManager.Register("script-1", (port, outputs) =>
            {
                calls++;
                observedPort = port;
                observedOutputs = outputs;
            });

            Dispatch("script-1", "Success", "{\"score\":42}");
            Dispatch("script-1", "Ignored", "{}");

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(observedPort, Is.EqualTo("Success"));
            Assert.That(observedOutputs, Is.EqualTo("{\"score\":42}"));
        }

        [Test]
        public void DuplicateInstanceRegistrationReplacesOldCallback()
        {
            var oldCalls = 0;
            var newCalls = 0;
            ScriptCompletionManager.Register("same-script", (_, __) => oldCalls++);
            ScriptCompletionManager.Register("same-script", (_, __) => newCalls++);

            Dispatch("same-script", "Done", "{}");

            Assert.That(oldCalls, Is.Zero);
            Assert.That(newCalls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingGameCallbackCannotEscapeNativeCompletionBoundary()
        {
            ScriptCompletionManager.Register("throwing-script", (_, __) =>
                throw new InvalidOperationException("script completion callback failed"));
            LogAssert.Expect(LogType.Error, new Regex("script completion callback failed"));

            Assert.DoesNotThrow(() => Dispatch("throwing-script", "Done", "{}"));
            Assert.DoesNotThrow(() => Dispatch("throwing-script", "DoneAgain", "{}"));
        }

        [Test]
        public void CleanupDropsCallbacksFromPreviousSdkSession()
        {
            var calls = 0;
            ScriptCompletionManager.Register("old-session-script", (_, __) => calls++);

            Assert.That(CleanUp, Is.Not.Null, "manager must expose teardown for Controller.Stop");
            CleanUp.Invoke(null, null);
            Dispatch("old-session-script", "LateNativeCompletion", "{}");

            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void InvalidRegistrationsDoNotCreateCallableEntries()
        {
            var calls = 0;
            ScriptCompletionManager.Register(null, (_, __) => calls++);
            ScriptCompletionManager.Register("", (_, __) => calls++);
            ScriptCompletionManager.Register("null-callback", null);

            Dispatch(null, "Done", "{}");
            Dispatch("", "Done", "{}");
            Dispatch("null-callback", "Done", "{}");

            Assert.That(calls, Is.Zero);
        }

        private static void Dispatch(string instanceId, string exitPort, string outputsJson)
        {
            OnScriptCompleted.Invoke(null, new object[] { instanceId, exitPort, outputsJson });
        }
    }
}
