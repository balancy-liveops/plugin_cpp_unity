using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Balancy.Dictionaries;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class DataObjectsLifecycleTests
    {
        private static readonly Type ManagerType = typeof(DataObjectsManager);
        private static readonly Type PathType = ManagerType.GetNestedType("OneObjectPath", BindingFlags.NonPublic);
        private static readonly Type ViewType = ManagerType.GetNestedType("OneObjectView", BindingFlags.NonPublic);
        private static readonly MethodInfo AddPathCallback = PathType.GetMethod("AddCallback");
        private static readonly MethodInfo ProcessLoadedObject = PathType.GetMethod("ProcessLoadedObject");
        private static readonly MethodInfo AddViewCallback = ViewType.GetMethod("AddCallback");
        private static readonly MethodInfo SetViewPath = ViewType.GetMethod("SetPath");
        private static readonly IDictionary AllObjects = (IDictionary)ManagerType
            .GetField("AllObjects", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly IDictionary AllViews = (IDictionary)ManagerType
            .GetField("AllViews", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly MethodInfo CleanUp = ManagerType
            .GetMethod("CleanUp", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => CleanUp.Invoke(null, null);

        [TearDown]
        public void TearDown() => CleanUp.Invoke(null, null);

        [Test]
        public void ThrowingFileCallbackDoesNotSkipLaterCallbackOrEscape()
        {
            var pathObject = Activator.CreateInstance(PathType, true);
            var first = AsyncLoadHandler.CreateHandler();
            var second = AsyncLoadHandler.CreateHandler();
            var laterCalls = 0;
            AddPathCallback.Invoke(pathObject, new object[]
            {
                first,
                new Action<string>(_ => throw new InvalidOperationException("file callback failed"))
            });
            AddPathCallback.Invoke(pathObject, new object[]
            {
                second,
                new Action<string>(_ => laterCalls++)
            });
            LogAssert.Expect(LogType.Exception, new Regex("file callback failed"));

            Assert.DoesNotThrow(() => ProcessLoadedObject.Invoke(pathObject, new object[] { null }));
            Assert.That(first.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Finished));
            Assert.That(second.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Finished));
            Assert.That(laterCalls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingViewCallbackDoesNotSkipLaterCallbackOrEscape()
        {
            var view = Activator.CreateInstance(ViewType, true);
            var first = AsyncLoadHandler.CreateHandler();
            var second = AsyncLoadHandler.CreateHandler();
            var laterCalls = 0;
            AddViewCallback.Invoke(view, new object[]
            {
                first,
                new Action<string>(_ => throw new InvalidOperationException("view callback failed"))
            });
            AddViewCallback.Invoke(view, new object[]
            {
                second,
                new Action<string>(_ => laterCalls++)
            });
            LogAssert.Expect(LogType.Exception, new Regex("view callback failed"));

            Assert.DoesNotThrow(() => SetViewPath.Invoke(view, new object[] { "cached/view.html" }));
            Assert.That(first.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Finished));
            Assert.That(second.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Finished));
            Assert.That(laterCalls, Is.EqualTo(1));
        }

        [Test]
        public void CleanupCancelsEveryInFlightObjectAndViewHandler()
        {
            var pathObject = Activator.CreateInstance(PathType, true);
            var view = Activator.CreateInstance(ViewType, true);
            var pathHandler = AsyncLoadHandler.CreateHandler();
            var viewHandler = AsyncLoadHandler.CreateHandler();
            AddPathCallback.Invoke(pathObject, new object[] { pathHandler, new Action<string>(_ => { }) });
            AddViewCallback.Invoke(view, new object[] { viewHandler, new Action<string>(_ => { }) });
            AllObjects.Add("object", pathObject);
            AllViews.Add("view", view);

            CleanUp.Invoke(null, null);

            Assert.That(pathHandler.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Cancelled));
            Assert.That(viewHandler.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Cancelled));
            Assert.That(AllObjects.Count, Is.Zero);
            Assert.That(AllViews.Count, Is.Zero);
        }

        [Test]
        public void CancelledHandlerIsNotRevivedByLateCompletion()
        {
            var pathObject = Activator.CreateInstance(PathType, true);
            var handler = AsyncLoadHandler.CreateHandler();
            var calls = 0;
            AddPathCallback.Invoke(pathObject, new object[] { handler, new Action<string>(_ => calls++) });
            handler.Cancel();

            ProcessLoadedObject.Invoke(pathObject, new object[] { null });

            Assert.That(handler.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Cancelled));
            Assert.That(calls, Is.Zero);
        }
    }
}
