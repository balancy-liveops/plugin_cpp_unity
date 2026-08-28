using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class CustomConditionsLifecycleTests
    {
        private static readonly Type Subject = typeof(CustomConditions);
        private static readonly FieldInfo Registered = Subject.GetField("_registered", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo RegisterCore = Subject.GetMethod("RegisterCore", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo UnregisterCore = Subject.GetMethod("UnregisterCore", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            Main.Stop();
            Registered.SetValue(null, false);
        }

        [TearDown]
        public void TearDown()
        {
            Registered.SetValue(null, false);
            Main.Stop();
        }

        [Test]
        public void PartialRegistrationRollsNativeHandlerBackAndCanRetry()
        {
            var registrations = 0;
            var rollbacks = 0;
            var failure = Assert.Throws<TargetInvocationException>(() => RegisterCore.Invoke(null, new object[]
            {
                new Action(() => registrations++),
                new Action(() => throw new InvalidOperationException("second registration failed")),
                new Action(() => rollbacks++)
            }));

            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(registrations, Is.EqualTo(1));
            Assert.That(rollbacks, Is.EqualTo(1));
            Assert.That(Registered.GetValue(null), Is.False);

            Assert.DoesNotThrow(() => RegisterCore.Invoke(null, new object[]
            {
                new Action(() => registrations++),
                new Action(() => registrations++),
                new Action(() => rollbacks++)
            }));
            Assert.That(Registered.GetValue(null), Is.True);
        }

        [Test]
        public void FailedUnregisterStillAllowsNextRegistration()
        {
            Registered.SetValue(null, true);
            var failure = Assert.Throws<TargetInvocationException>(() => UnregisterCore.Invoke(null, new object[]
            {
                new Action(() => throw new InvalidOperationException("native cleanup failed"))
            }));

            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(Registered.GetValue(null), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        public void EmptyForceUpdateNeverCrossesNativeBoundary(string unnyId)
        {
            LogAssert.Expect(LogType.Error, new Regex("non-empty condition ID"));
            Assert.DoesNotThrow(() => CustomConditions.ForceUpdate(unnyId));
        }

        [Test]
        public void ForceUpdateBeforeInitializationNeverCrossesNativeBoundary()
        {
            LogAssert.Expect(LogType.Error, new Regex("SDK is not initialized"));
            Assert.DoesNotThrow(() => CustomConditions.ForceUpdate("condition.id"));
        }
    }
}
