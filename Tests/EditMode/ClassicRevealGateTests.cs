using System.Collections.Generic;
using Balancy.WebView;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class ClassicRevealGateTests
    {
        private double now;
        private List<string> reveals;
        private ClassicRevealGate gate;

        [SetUp] public void SetUp()
        {
            now = 0;
            reveals = new List<string>();
            gate = new ClassicRevealGate(reason => reveals.Add(reason), () => now);
        }

        [TearDown] public void TearDown() => gate.Cancel();

        [Test] public void FirstReadySignalRevealsOnceAndClearsTheTimeout()
        {
            gate.Begin(3);
            gate.Signal("ready");
            gate.Signal("ready"); // views send BalancyIsReady twice while delayIsReady is captured by value
            now = 10; gate.Tick();
            Assert.That(reveals, Is.EqualTo(new[] { "ready" }));
            Assert.That(gate.Pending, Is.False);
        }

        [Test] public void SilentPageIsRevealedAtTheTimeout()
        {
            gate.Begin(3);
            now = 2.9; gate.Tick();
            Assert.That(reveals, Is.Empty);
            now = 3; gate.Tick();
            Assert.That(reveals, Is.EqualTo(new[] { "timeout" }));
        }

        [Test] public void SignalsWithoutAHiddenOpenDoNothing()
        {
            gate.Signal("ready"); gate.Signal("bootstrapError");
            now = 100; gate.Tick();
            Assert.That(reveals, Is.Empty);
        }

        [Test] public void ClosingBeforeTheSignalCancelsTheReveal()
        {
            gate.Begin(3);
            gate.Cancel();
            gate.Signal("ready");
            now = 10; gate.Tick();
            Assert.That(reveals, Is.Empty);
        }

        [Test] public void ErrorsRevealLikeTheReadySignal()
        {
            gate.Begin(3);
            gate.Signal("loadFailed");
            gate.Signal("ready");
            Assert.That(reveals, Is.EqualTo(new[] { "loadFailed" }));
        }

        [Test] public void TimeoutBeforeThePageFinishesShowsTheViewAgainWhenItDoes()
        {
            // Android's onPageFinished hides a WebView opened with startHidden once more, undoing the earlier reveal.
            gate.Begin(3);
            now = 3; gate.Tick();
            Assert.That(reveals, Is.EqualTo(new[] { "timeout" }));
            Assert.That(gate.LoadCompleted(), Is.True);
            Assert.That(gate.LoadCompleted(), Is.False);
        }

        [Test] public void ReadyAfterThePageFinishedNeedsNoSecondShow()
        {
            gate.Begin(3);
            Assert.That(gate.LoadCompleted(), Is.False);
            gate.Signal("ready");
            Assert.That(reveals, Is.EqualTo(new[] { "ready" }));
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

        [Test] public void ReadyRequestIsRecognisedInTheMessagesTheBridgeSends()
        {
            // Captured from a classic Car Race open (Android, SDK 1.9.6).
            const string single = "{\"type\":\"request\",\"viewId\":\"262590147493413ca62d207a26b1eb39\",\"sender\":null,\"id\":\"50\",\"action\":201,\"params\":{\"id\":\"none\"}}";
            Assert.That(ClassicRevealGate.IsRequest(single, 201), Is.True);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"batch\",\"requests\":[{\"id\":\"7\",\"action\":10},{\"id\":\"8\",\"action\":201}]}", 201), Is.True);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":2010}", 201), Is.False);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":20}", 201), Is.False);
            Assert.That(ClassicRevealGate.IsRequest("{\"type\":\"request\",\"id\":\"9\",\"action\":201", 201), Is.True);
            Assert.That(ClassicRevealGate.IsRequest(null, 201), Is.False);
        }

        [Test] public void EachOpenGetsItsOwnDeadline()
        {
            gate.Begin(3);
            gate.Signal("ready");
            now = 5; gate.Begin(3);
            now = 7.9; gate.Tick();
            Assert.That(reveals, Is.EqualTo(new[] { "ready" }));
            now = 8; gate.Tick();
            Assert.That(reveals, Is.EqualTo(new[] { "ready", "timeout" }));
        }
    }
}
