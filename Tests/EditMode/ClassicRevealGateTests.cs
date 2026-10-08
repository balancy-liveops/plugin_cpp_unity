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
