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
            Assert.That(log, Is.Empty);
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

        [Test] public void ReadyRequestIsRecognisedInTheMessagesTheBridgeSends()
        {
            // Captured on Android, SDK 1.9.6: a classic page's request carries no viewId; a persistent view's does.
            const string classic = "{\"type\":\"request\",\"sender\":null,\"id\":\"53\",\"action\":201,\"params\":{\"id\":\"none\"}}";
            const string persistent = "{\"type\":\"request\",\"viewId\":\"262590147493413ca62d207a26b1eb39\",\"sender\":null,\"id\":\"50\",\"action\":201,\"params\":{\"id\":\"none\"}}";
            Assert.That(ClassicRevealGate.IsRequest(classic, 201), Is.True);
            Assert.That(ClassicRevealGate.IsRequest(persistent, 201), Is.True);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"batch\",\"requests\":[{\"id\":\"7\",\"action\":10},{\"id\":\"8\",\"action\":201}]}", 201), Is.True);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":2010}", 201), Is.False);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":20}", 201), Is.False);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":201", 201), Is.True);
            Assert.That(ClassicRevealGate.IsRequest(null, 201), Is.False);
        }

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
