using System;
using System.Collections;
using System.Reflection;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using Balancy.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Balancy.Tests
{
    public class SectionTests
    {
        private GameObject root, prefab;
        private Section section;
        private RectTransform content;
        private Sprite placeholder, downloaded;
        private Texture2D texture;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Model : IViewModel
        {
            public UnnyObject Icon => null;
            public int UnnyPriority { get; set; }
            public UnnyObject UnnyView => null;
            public ViewPlacement UnnyPlacement => default;
        }
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        private static object Get(object obj, string name) => obj.GetType().GetField(name, Private).GetValue(obj);
        private void Call(string name, params object[] args) => typeof(Section).GetMethod(name, Private).Invoke(section, args);
        private Element Add(string id, Func<Action<Sprite>, AsyncLoadHandler> loader = null, int priority = 0)
        {
            Call("AddIconDisplay", id, new Model { UnnyPriority = priority }, null, new Func<int>(() => 100), loader);
            foreach (var element in content.GetComponentsInChildren<Element>())
                if (element.gameObject.activeSelf && element.name == "Element(Clone)") {
                    element.name = id;
                    return element;
                }
            throw new Exception("New element missing");
        }
        private static Sprite Icon(Element element) => ((Image)Get(element, "icon")).sprite;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(2, 2);
            placeholder = Sprite.Create(texture, new Rect(0,0,1,1), Vector2.zero);
            downloaded = Sprite.Create(texture, new Rect(0,0,2,2), Vector2.zero);
            prefab = new GameObject("Element", typeof(RectTransform));
            prefab.SetActive(false);
            var image = prefab.AddComponent<Image>(); image.sprite = placeholder;
            var button = prefab.AddComponent<Button>(); button.targetGraphic = image;
            var label = new GameObject("Timer", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(prefab.transform, false);
            var element = prefab.AddComponent<Element>();
            Set(element, "icon", image); Set(element, "button", button); Set(element, "timerText", label);
            root = new GameObject("Section"); root.SetActive(false);
            section = root.AddComponent<Section>();
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root.transform, false);
            Set(section, "elementPrefab", prefab); Set(section, "content", content);
            root.SetActive(true);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(prefab);
            yield return null;
            UnityEngine.Object.Destroy(placeholder); UnityEngine.Object.Destroy(downloaded); UnityEngine.Object.Destroy(texture);
        }
        [UnityTest]
        public IEnumerator FinishedIconRemainsUntilItsInstanceIsRemoved()
        {
            yield return null; // Section.Start subscribes to the lifecycle callbacks.
            var first = Add("first-instance");
            var next = Add("next-instance");
            var info = new Balancy.Data.SmartObjects.EventInfo();
            typeof(Balancy.Data.SmartObjects.EventInfo).GetField("_instanceId", Private)
                .SetValue(info, "first-instance");
            Callbacks.OnEventDeactivated?.Invoke(info);
            Assert.That(first.gameObject.activeSelf, Is.True, "Finished must remain openable");
            Callbacks.OnEventRemoved?.Invoke(info);
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(next.gameObject.activeSelf, Is.True, "A later occurrence keeps its icon");
            Callbacks.OnEventRemoved?.Invoke(info);
            Assert.That(next.gameObject.activeSelf, Is.True);
            yield return null;
            Assert.That(content.childCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TimerReadsFinishedStateWithoutDeactivatedSubscription()
        {
            bool finished = false;
            var element = Add("timer-instance");
            var stateField = typeof(Element).GetField("_isFinished", Private);
            stateField.SetValue(element, new Func<bool>(() => finished));
            var update = typeof(Element).GetMethod("UpdateTimer", Private);
            update.Invoke(element, new object[] { 0f });
            var label = (TMP_Text)Get(element, "timerText");
            Assert.That(label.text, Is.Not.EqualTo("FINISHED"));
            finished = true;
            update.Invoke(element, new object[] { 0f });
            Assert.That(label.text, Is.EqualTo("FINISHED"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ButtonAndTimerExistBeforeIconCompletes()
        {
            Action<Sprite> complete = null;
            var handle = AsyncLoadHandler.CreateHandler();
            var element = Add("one", callback => { complete = callback; return handle; });
            Assert.That(element.gameObject.activeInHierarchy, Is.True);
            Assert.That(Icon(element), Is.SameAs(placeholder));
            Assert.That(Get(element, "_onClick"), Is.Not.Null);
            var timer = Get(element, "_cancellationTokenSource");
            Assert.That(timer, Is.Not.Null);
            complete(downloaded);
            Assert.That(Icon(element), Is.SameAs(downloaded));
            Assert.That(Get(element, "_cancellationTokenSource"), Is.SameAs(timer));
            Assert.That(content.GetComponentsInChildren<Element>().Length, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest]
        public IEnumerator FailedIconKeepsPlaceholderAndButton()
        {
            Action<Sprite> complete = null;
            var element = Add("one", callback => { complete = callback; return AsyncLoadHandler.CreateHandler(); });
            complete(null);
            Assert.That(Icon(element), Is.SameAs(placeholder));
            Assert.That(element.gameObject.activeInHierarchy, Is.True);
            yield return null;
        }
        [UnityTest]
        public IEnumerator SynchronousCacheHitUpdatesExistingButton()
        {
            var element = Add("one", callback => { callback(downloaded); return AsyncLoadHandler.CreateHandler(AsyncLoadHandler.Status.Finished); });
            Assert.That(Icon(element), Is.SameAs(downloaded));
            Assert.That(content.childCount, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest]
        public IEnumerator ReplacingSameIdCancelsAndIgnoresOldIcon()
        {
            Action<Sprite> complete = null;
            var handle = AsyncLoadHandler.CreateHandler();
            var old = Add("one", callback => { complete = callback; return handle; });
            var current = Add("one");
            Assert.That(handle.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Cancelled));
            Assert.That(old.gameObject.activeSelf, Is.False);
            complete(downloaded);
            Assert.That(Icon(current), Is.SameAs(placeholder));
            yield return null;
            Assert.That(content.childCount, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator CleanupCancelsAndLateCompletionDoesNotRecreateElement()
        {
            Action<Sprite> complete = null;
            var handle = AsyncLoadHandler.CreateHandler();
            Add("one", callback => { complete = callback; return handle; });
            Call("CleanUp");
            Assert.That(handle.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Cancelled));
            complete(downloaded);
            yield return null;
            Assert.That(content.childCount, Is.Zero);
        }
        [UnityTest]
        public IEnumerator DestroySectionCancelsPendingIcon()
        {
            Action<Sprite> complete = null;
            var handle = AsyncLoadHandler.CreateHandler();
            Add("one", callback => { complete = callback; return handle; });
            UnityEngine.Object.Destroy(root);
            yield return null;
            Assert.That(handle.GetStatus(), Is.EqualTo(AsyncLoadHandler.Status.Cancelled));
            Assert.DoesNotThrow(() => complete(downloaded));
        }
        [UnityTest]
        public IEnumerator PriorityOrderDoesNotWaitForIconCompletion()
        {
            var low = Add("low", callback => AsyncLoadHandler.CreateHandler(), 1);
            var high = Add("high", null, 10);
            Assert.That(content.GetChild(0), Is.EqualTo(high.transform));
            Assert.That(content.GetChild(1), Is.EqualTo(low.transform));
            yield return null;
        }
    }
}
