using System;
using System.Collections.Generic;
using Balancy.WebView;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class ClassicRevealGateTests
    {
        private double now;
        private List<string> log;
        private ClassicRevealGate gate;

        [SetUp] public void SetUp()
        {
            now = 0;
            log = new List<string>();
            gate = new ClassicRevealGate(reason => log.Add(reason), () => now);
        }

        [TearDown] public void TearDown() => gate.Cancel();

        private void BeginWithCallbacks() => gate.Begin(30, () => log.Add("shown"), () => log.Add("failed"));

        [Test] public void FirstReadySignalRevealsOnceAndClearsTheTimeout()
        {
            gate.Begin(3);
            gate.LoadCompleted();
            gate.PageSignal("ready");
            gate.PageSignal("ready"); // views send BalancyIsReady twice while delayIsReady is captured by value
            now = 10; gate.Tick();
            Assert.That(log, Is.EqualTo(new[] { "ready" }));
            Assert.That(gate.Pending, Is.False);
        }

        [Test] public void SilentPageIsRevealedAtTheTimeout()
        {
            gate.Begin(3);
            gate.LoadCompleted();
            now = 2.9; gate.Tick();
            Assert.That(log, Is.Empty);
            now = 3; gate.Tick();
            Assert.That(log, Is.EqualTo(new[] { "timeout" }));
        }

        [Test] public void SignalsWithoutAHiddenOpenDoNothing()
        {
            gate.PageSignal("ready"); gate.PageSignal("bootstrapError"); gate.Signal("loadFailed");
            now = 100; gate.Tick();
            Assert.That(gate.Fail(), Is.Null);
            Assert.That(log, Is.Empty);
        }

        [Test] public void CleanupRetiresAnEarlyRevealStillWaitingForPageFinish()
        {
            gate.Begin(3);
            Assert.That(gate.InProgress, Is.True);
            now = 4; gate.Tick();
            Assert.That(gate.Pending, Is.False);
            Assert.That(gate.InProgress, Is.True);
            gate.LoadCompleted();
            Assert.That(gate.InProgress, Is.False);
            gate.Begin(3); gate.Cancel();
            Assert.That(gate.InProgress, Is.False);
        }

        [Test] public void ClosingBeforeTheSignalCancelsTheReveal()
        {
            gate.Begin(3);
            gate.LoadCompleted();
            gate.Cancel();
            gate.PageSignal("ready");
            now = 10; gate.Tick();
            Assert.That(log, Is.Empty);
        }

        [Test] public void ErrorsRevealLikeTheReadySignal()
        {
            gate.Begin(3);
            gate.Signal("loadFailed"); // native, so it can come before the page finishes
            gate.LoadCompleted();
            gate.PageSignal("ready");
            Assert.That(log, Is.EqualTo(new[] { "loadFailed" }));

            gate.Begin(3);
            gate.LoadCompleted();
            gate.PageSignal("bootstrapError");
            gate.PageSignal("ready");
            Assert.That(log, Is.EqualTo(new[] { "loadFailed", "bootstrapError" }));
        }

        [Test] public void TimeoutBeforeThePageFinishesShowsTheViewAgainWhenItDoes()
        {
            // Android's onPageFinished hides a WebView opened with startHidden once more, undoing the earlier reveal.
            gate.Begin(3);
            now = 3; gate.Tick();
            Assert.That(log, Is.EqualTo(new[] { "timeout" }));
            Assert.That(gate.LoadCompleted(), Is.True);
            Assert.That(gate.LoadCompleted(), Is.False);
        }

        [Test] public void ReadyAfterThePageFinishedNeedsNoSecondShow()
        {
            gate.Begin(3);
            Assert.That(gate.LoadCompleted(), Is.False);
            gate.PageSignal("ready");
            Assert.That(log, Is.EqualTo(new[] { "ready" }));
            Assert.That(gate.LoadCompleted(), Is.False);
        }

        [Test] public void ClosingOrReopeningForgetsAnEarlyReveal()
        {
            gate.Begin(3);
            now = 3; gate.Tick();
            gate.Cancel();
            Assert.That(gate.LoadCompleted(), Is.False);
            gate.Begin(3);
            now = 6; gate.Tick();
            gate.Begin(3);
            Assert.That(gate.LoadCompleted(), Is.False);
        }

        [Test] public void PageSignalsBeforeThePageFinishesAreThePreviousPagesAndAreIgnored()
        {
            // The previous view's BalancyIsReady was still queued when this one opened (review 01 #6).
            gate.Begin(30);
            gate.PageSignal("ready");
            gate.PageSignal("bootstrapError");
            Assert.That(log, Is.Empty);
            Assert.That(gate.Pending, Is.True);
            gate.LoadCompleted();
            gate.PageSignal("ready");
            Assert.That(log, Is.EqualTo(new[] { "ready" }));
        }

        [Test] public void ShownRunsOnceRightAfterTheReveal()
        {
            BeginWithCallbacks();
            gate.LoadCompleted();
            Assert.That(log, Is.Empty);
            gate.PageSignal("ready");
            gate.PageSignal("ready");
            now = 60; gate.Tick();
            Assert.That(log, Is.EqualTo(new[] { "ready", "shown" }));
        }

        [Test] public void TimeoutRevealRunsShownOnceThoughTheViewIsShownAgain()
        {
            BeginWithCallbacks();
            now = 30; gate.Tick();
            Assert.That(gate.LoadCompleted(), Is.True);
            Assert.That(log, Is.EqualTo(new[] { "timeout", "shown" }));
        }

        [Test] public void ClosingBeforeTheRevealRunsNeitherCallback()
        {
            BeginWithCallbacks();
            gate.Cancel();
            gate.LoadCompleted(); gate.PageSignal("ready");
            now = 60; gate.Tick();
            Assert.That(gate.Fail(), Is.Null);
            Assert.That(log, Is.Empty);
        }

        [Test] public void RendererLossBeforeTheRevealFailsTheOpenOnce()
        {
            BeginWithCallbacks();
            var failed = gate.Fail();
            Assert.That(log, Is.Empty); // the caller runs it after the teardown
            failed();
            Assert.That(gate.Fail(), Is.Null);
            now = 60; gate.Tick();
            Assert.That(log, Is.EqualTo(new[] { "failed" }));
            Assert.That(gate.Pending, Is.False);
        }

        [Test] public void RendererLossAfterTheRevealIsNotAFailedOpen()
        {
            BeginWithCallbacks();
            gate.LoadCompleted();
            gate.PageSignal("ready");
            Assert.That(gate.Fail(), Is.Null);
            Assert.That(log, Is.EqualTo(new[] { "ready", "shown" }));
        }

        [Test] public void ReadyDispatchKeepsItsOwnerBeforeShownClosesAndOpensAnotherView()
        {
            int owner = 111, dispatchedOwner = 0;
            gate.Begin(30, () => { gate.Cancel(); owner = 222; gate.Begin(30); });
            gate.LoadCompleted();
            gate.ProcessReadyRequest(() => { dispatchedOwner = owner; log.Add("core"); });
            Assert.That(dispatchedOwner, Is.EqualTo(111));
            Assert.That(owner, Is.EqualTo(222));
            Assert.That(log, Is.EqualTo(new[] { "core", "ready" }));
            Assert.That(gate.Pending, Is.True); // The replacement has not received its own ready.
        }

        [Test] public void ClosingAndReplacingDuringDispatchDoesNotRevealTheReplacement()
        {
            BeginWithCallbacks();
            gate.LoadCompleted();
            gate.ProcessReadyRequest(() => {
                gate.Cancel();
                gate.Begin(30, () => log.Add("replacement shown"));
                gate.LoadCompleted();
            });
            Assert.That(log, Is.Empty);
            Assert.That(gate.Pending, Is.True);
            gate.ProcessReadyRequest(() => log.Add("replacement core"));
            Assert.That(log, Is.EqualTo(new[] { "replacement core", "ready", "replacement shown" }));
        }

        [Test] public void ClosingDuringDispatchCancelsTheShownCallback()
        {
            BeginWithCallbacks();
            gate.LoadCompleted();
            gate.ProcessReadyRequest(() => gate.Cancel());
            Assert.That(log, Is.Empty);
            Assert.That(gate.Pending, Is.False);
        }

        [Test] public void ApplicationRejectionDoesNotLeaveTheCurrentPageHidden()
        {
            BeginWithCallbacks();
            gate.LoadCompleted();
            gate.ProcessReadyRequest(() => log.Add("rejected"));
            Assert.That(log, Is.EqualTo(new[] { "rejected", "ready", "shown" }));
        }

        [Test] public void ApplicationExceptionStillRevealsTheSameOpeningAndPropagates()
        {
            BeginWithCallbacks();
            gate.LoadCompleted();
            Assert.Throws<InvalidOperationException>(() => gate.ProcessReadyRequest(() => {
                throw new InvalidOperationException("application failed");
            }));
            Assert.That(log, Is.EqualTo(new[] { "ready", "shown" }));
        }

        [Test] public void NestedReadyDispatchDoesNotRevealAnOpeningCreatedByShown()
        {
            gate.Begin(30, () => { log.Add("shown"); gate.Cancel(); gate.Begin(30); gate.LoadCompleted(); });
            gate.LoadCompleted();
            gate.ProcessReadyRequest(() => gate.ProcessReadyRequest(() => log.Add("core")));
            Assert.That(log, Is.EqualTo(new[] { "core", "ready", "shown" }));
            Assert.That(gate.Pending, Is.True);
        }

        [Test] public void DispatchBeforeLoadCompletedDoesNotRevealThePage()
        {
            BeginWithCallbacks();
            gate.ProcessReadyRequest(() => log.Add("core"));
            Assert.That(log, Is.EqualTo(new[] { "core" }));
            Assert.That(gate.Pending, Is.True);
        }

#if UNITY_EDITOR
        [Test] public void ReadyRequestIsRecognisedInTheMessagesTheBridgeSends()
        {
            // Captured on Android, SDK 1.9.6: a classic page's request carries no viewId; a persistent view's does.
            const string classic = "{\"type\":\"request\",\"sender\":null,\"id\":\"53\",\"action\":201,\"params\":{\"id\":\"none\"}}";
            const string persistent = "{\"type\":\"request\",\"viewId\":\"262590147493413ca62d207a26b1eb39\",\"sender\":null,\"id\":\"50\",\"action\":201,\"params\":{\"id\":\"none\"}}";
            Assert.That(ClassicReadyRequest.IsRequest(classic, 201), Is.True);
            Assert.That(ClassicReadyRequest.IsRequest(persistent, 201), Is.False); // An old persistent view cannot reveal a classic page.
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"batch\",\"requests\":[{\"id\":\"7\",\"action\":10},{\"id\":\"8\",\"action\":201}]}", 201), Is.True);
            Assert.That(ClassicReadyRequest.IsRequest(" { \"action\" : 201, \"id\" : \"9\", \"type\" : \"request\" } ", 201), Is.True);
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":2010}", 201), Is.False);
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":20}", 201), Is.False);
            Assert.That(ClassicReadyRequest.IsRequest(null, 201), Is.False);
        }

        [Test] public void NestedActionsAndOtherMessageTypesCannotRevealThePage()
        {
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":1000,\"params\":{\"action\":201}}", 201), Is.False);
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":1000,\"params\":{\"message\":\"{\\\"action\\\":201}\"}}", 201), Is.False);
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"custom\",\"id\":\"9\",\"action\":201}", 201), Is.False);
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"batch\",\"requests\":[{\"id\":\"9\",\"action\":1000,\"params\":{\"action\":201}}]}", 201), Is.False);
            Assert.That(ClassicReadyRequest.IsRequest("{\"type\":\"batch\",\"requests\":[{\"id\":\"9\",\"action\":201},{\"id\":\"10\",\"action\":20,\"viewId\":\"old-persistent\"}]}", 201), Is.False);
        }

        [Test] public void MalformedMessagesCannotRevealThePage()
        {
            foreach (string json in new[] {
                "{\"type\":\"request\",\"id\":\"9\",\"action\":201",
                "{\"type\":\"request\",\"id\":\"9\",\"action\":201} trailing",
                "{\"type\":\"request\",\"action\":201}",
                "{\"type\":\"batch\",\"requests\":[null,{\"id\":\"9\",\"action\":201}]}",
                "{\"type\":\"batch\",\"requests\":[{\"type\":\"batch\",\"id\":\"9\",\"action\":201}]}", "[]", "" })
                Assert.That(ClassicReadyRequest.IsRequest(json, 201), Is.False, json);
        }
#endif

        [Test] public void EachOpenGetsItsOwnDeadline()
        {
            gate.Begin(3);
            gate.LoadCompleted();
            gate.PageSignal("ready");
            now = 5; gate.Begin(3);
            now = 7.9; gate.Tick();
            Assert.That(log, Is.EqualTo(new[] { "ready" }));
            now = 8; gate.Tick();
            Assert.That(log, Is.EqualTo(new[] { "ready", "timeout" }));
        }
    }
}
