using System;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Balancy.Data.SmartObjects;
using Balancy.Models.LiveOps.Tasks;
namespace Balancy.Tests
{
    public class ExtensibleTasksTests
    {
        private static readonly Type Bridge = typeof(Main).Assembly.GetType("Balancy.CustomTasks");
        private static object Call(string method, params object[] args) => Bridge.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static CustomTaskContext Context(string id, string run) => (CustomTaskContext)Activator.CreateInstance(typeof(CustomTaskContext), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[]{id,run}, null);
        private sealed class Quest : TaskCustom
        {
            public readonly List<string> Events = new List<string>();
            public bool ThrowOnStart;
            public override void OnStart(CustomTaskContext context) { Events.Add("start:" + context.RunId); if (ThrowOnStart) throw new InvalidOperationException("task-start-test"); }
            public override void OnStop(CustomTaskContext context) { Events.Add("stop:" + context.RunId); }
        }
        [SetUp] public void SetUp() { Call("UnregisterCore", new Action(() => {})); }
        [TearDown] public void TearDown() { Call("UnregisterCore", new Action(() => {})); }
        private static void Dispatch(string run, bool active, Quest quest) {
            Call("DispatchCore", "task", run, active, new Func<CustomTaskContext, TaskCustom>(_ => quest));
        }
        [Test] public void CustomSubclassReceivesStartStopOncePerRun() {
            var quest = new Quest();
            Dispatch("one", true, quest); Dispatch("one", true, quest);
            Dispatch("two", true, quest); Dispatch("one", false, quest); Dispatch("two", false, quest);
            CollectionAssert.AreEqual(new[]{"start:one","stop:one","start:two","stop:two"}, quest.Events);
        }
        [Test] public void ShutdownReleasesOriginalModelAfterReload() {
            var first = new Quest(); var second = new Quest();
            Dispatch("one", true, first); Dispatch("two", true, second);
            Call("UnregisterCore", new Action(() => {}));
            CollectionAssert.AreEqual(new[]{"start:one","stop:one"}, first.Events);
            CollectionAssert.AreEqual(new[]{"start:two","stop:two"}, second.Events);
        }
        [Test] public void StaleOrCompletedActivationDoesNotInvokeGameCode() {
            var quest = new Quest(); Dispatch("live", true, quest);
            Call("DispatchCore", "task", "stale", true, new Func<CustomTaskContext, TaskCustom>(_ => null));
            CollectionAssert.AreEqual(new[]{"start:live"}, quest.Events);
        }
        [Test] public void FailedStartReleasesSubscriptions() {
            var quest = new Quest { ThrowOnStart = true };
            LogAssert.Expect(LogType.Exception, new Regex("task-start-test"));
            Dispatch("one", true, quest);
            CollectionAssert.AreEqual(new[]{"start:one","stop:one"}, quest.Events);
        }
        [Test] public void RegistrationIsIdempotentAndShutdownCanRetryAfterFailure() {
            int count=0;
            Call("RegisterCore",new Action(()=>++count)); Call("RegisterCore",new Action(()=>++count));
            Assert.That(count,Is.EqualTo(1));
            Assert.Throws<TargetInvocationException>(()=>Call("UnregisterCore",new Action(()=>throw new InvalidOperationException())));
            Call("RegisterCore",new Action(()=>++count)); Assert.That(count,Is.EqualTo(2));
        }
        [Test] public void ContextRoutesEveryOperationWithCapturedActivationToken() {
            var context=Context("task","original"); var seen=new List<string>();
            var update=typeof(CustomTaskContext).GetMethod("UpdateCore",BindingFlags.Instance|BindingFlags.NonPublic);
            Func<string,string,int,int,bool> send=(id,run,op,value)=> {seen.Add(id+":"+run+":"+op+":"+value);return op!=3;};
            for(int op=0;op<4;op++) Assert.That(update.Invoke(context,new object[]{op,2,send}),Is.EqualTo(op!=3));
            CollectionAssert.AreEqual(new[]{"task:original:0:2","task:original:1:2","task:original:2:2","task:original:3:2"},seen);
            Assert.That(update.Invoke(context,new object[]{0,-1,send}),Is.False);
            Assert.That(update.Invoke(Context("task",""),new object[]{0,1,send}),Is.False);
            Assert.That(seen.Count,Is.EqualTo(4));
        }
        [Test] public void NumericProgressPreservesInt64AndUsesInvariantCulture() {
            var info=new TaskInfo(); var field=typeof(TaskInfo).GetField("_numericProgress",BindingFlags.Instance|BindingFlags.NonPublic);
            var old=CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
                field.SetValue(info,"9007199254740993");
                Assert.That(info.TryGetNumericProgress(out long integer),Is.True); Assert.That(integer,Is.EqualTo(9007199254740993L));
                field.SetValue(info,"1.25");
                Assert.That(info.TryGetNumericProgress(out double number),Is.True); Assert.That(number,Is.EqualTo(1.25));
                Assert.That(info.TryGetNumericProgress(out integer),Is.False);
            } finally {CultureInfo.CurrentCulture=old;}
        }
        [Test] public void NativeInteropRegistersHooksAndRejectsAnInactiveTask() {
            var api=typeof(Main).Assembly.GetType("Balancy.LibraryMethods+API");
            var update=api.GetMethod("balancyTasks_Update",BindingFlags.Public|BindingFlags.Static);
            var register=Bridge.GetMethod("Register",BindingFlags.NonPublic|BindingFlags.Static);
            var unregister=Bridge.GetMethod("Unregister",BindingFlags.NonPublic|BindingFlags.Static);
            try {
                Assert.DoesNotThrow(()=>register.Invoke(null,null));
                Assert.That(update.Invoke(null,new object[]{"missing-task","missing-run",0,1}),Is.False);
                Assert.That(update.Invoke(null,new object[]{"","",0,1}),Is.False);
            } finally { unregister.Invoke(null,null); }
        }
        [Test] public void GeneratedCustomFactoryPreservesTheGameSubclass() {
            var old=CMS.OnTypeRequested;
            try {
                CMS.OnTypeRequested=name=>name=="Game.MyQuest" ? new Quest() : null;
                var factory=typeof(CMS).GetMethod("InstantiateByType",BindingFlags.NonPublic|BindingFlags.Static);
                Assert.That(factory.Invoke(null,new object[]{"Game.MyQuest"}),Is.TypeOf<Quest>());
            } finally {CMS.OnTypeRequested=old;}
        }
        [Test] public void FactoriesRecognizeAllNewBuiltInModels() {
            var factory=typeof(CMS).GetMethod("InstantiateByType",BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That(factory.Invoke(null,new object[]{"LiveOps.Tasks.BaseTask"}),Is.TypeOf<BaseTask>());
            Assert.That(factory.Invoke(null,new object[]{"LiveOps.Tasks.TaskCustom"}),Is.TypeOf<TaskCustom>());
            Assert.That(factory.Invoke(null,new object[]{"LiveOps.Tasks.TaskFieldNumber"}),Is.TypeOf<TaskFieldNumber>());
            Assert.That(factory.Invoke(null,new object[]{"LiveOps.Tasks.TaskCondition"}),Is.TypeOf<TaskCondition>());
        }
    }
}
