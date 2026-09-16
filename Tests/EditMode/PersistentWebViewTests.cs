using System;
using System.Collections.Generic;
using Balancy.WebView;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class PersistentWebViewTests
    {
        private double now;
        private PersistentViewState state;
        private List<string> messages, events;
        private bool transport;
        [SetUp] public void SetUp()
        {
            now = 0; transport = true;
            messages = new List<string>(); events = new List<string>();
            state = new PersistentViewState(message => { messages.Add(message); return transport; },
                () => events.Add("show"), () => events.Add("hide"), () => events.Add("destroy"),
                () => events.Add("closed"), id => events.Add("release:" + id), () => now, 10);
        }
        [TearDown] public void TearDown() => state.Reset();
        private void Prepare()
        {
            state.Prepare(() => true, null, null);
            state.Receive("shellReady", null, state.ShellId, null);
        }
        [Test] public void IdleRestartDestroysContextAndWaitsForNewAck()
        {
            int starts = 0;
            state.Prepare(() => { starts++; return true; }, null, null);
            var old = state.ShellId;
            state.Receive("shellReady", null, old, null);
            state.RequestRestart(); state.Tick();
            Assert.That(starts, Is.EqualTo(2));
            Assert.That(events, Is.EqualTo(new[] { "destroy" }));
            Assert.That(state.Preparing, Is.True);
            Assert.That(state.ShellId, Is.Not.EqualTo(old));
            state.Receive("shellReady", null, old, null);
            Assert.That(state.Preparing, Is.True);
            state.Receive("shellReady", null, state.ShellId, null);
            Assert.That(state.Preparing, Is.False);
        }

        [Test] public void UpdateNeverReplacesAnActiveOrHiddenView()
        {
            Prepare(); state.Show(id => id, null, null); state.Tick();
            var active = state.CurrentId;
            state.RequestRestart(); state.Tick();
            Assert.That(events, Does.Not.Contain("destroy"));
            state.Receive("viewReady", active, null, null);
            state.Visible = false; // Hiding is not closing.
            state.Tick(); Assert.That(events, Does.Not.Contain("destroy"));
            state.Close(); state.Tick();
            Assert.That(events, Does.Not.Contain("destroy"));
            state.Receive("viewCleared", active, null, null);
            Assert.That(events, Does.Contain("destroy"));
            Assert.That(state.Preparing, Is.True);
        }

        [Test] public void QueuedViewUsesNewShellAfterCloseAcknowledgment()
        {
            Prepare(); state.Show(id => id, null, null); state.Tick();
            var oldView = state.CurrentId;
            state.RequestRestart(); state.Close();
            state.Show(id => "new:" + id, null, null);
            var next = state.CurrentId;
            state.Receive("viewCleared", oldView, null, null);
            Assert.That(messages.Count, Is.EqualTo(2));
            Assert.That(state.CurrentId, Is.EqualTo(next));
            state.Receive("shellReady", null, state.ShellId, null);
            Assert.That(messages[2], Is.EqualTo("new:" + next));
        }

        [Test] public void UpdatesDuringPreparationCoalesceAndDelayReady()
        {
            int starts = 0, ready = 0;
            state.Prepare(() => { starts++; return true; }, () => ready++, null);
            var oldShell = state.ShellId;
            state.RequestRestart(); state.RequestRestart();
            state.Prepare(() => { Assert.Fail("Must reuse the original start operation"); return false; }, () => ready++, null);
            state.Show(id => id, null, null);
            state.Receive("shellReady", null, oldShell, null);
            Assert.That(starts, Is.EqualTo(2)); Assert.That(ready, Is.Zero);
            Assert.That(messages, Is.Empty);
            state.Receive("shellReady", null, state.ShellId, null);
            Assert.That(ready, Is.EqualTo(2)); Assert.That(messages.Count, Is.EqualTo(1));
        }

        [Test] public void FailedRestartReleasesQueuedViewAndReportsBothCallbacks()
        {
            bool startOk = true; int failures = 0;
            state.Prepare(() => startOk, null, null);
            state.Receive("shellReady", null, state.ShellId, null);
            state.RequestRestart();
            state.Prepare(() => true, null, error => failures++);
            state.Show(id => id, null, error => failures++);
            var pending = state.CurrentId;
            startOk = false; state.Tick();
            Assert.That(failures, Is.EqualTo(2)); Assert.That(state.Enabled, Is.False);
            Assert.That(events, Does.Contain("release:" + pending));
            Prepare(); Assert.That(state.Enabled, Is.True);
        }

        [Test] public void RestartTimeoutFailsQueuedViewAndResetForgetsPendingUpdate()
        {
            Prepare(); state.RequestRestart(); state.Tick();
            int failed = 0;
            state.Show(id => id, null, error => failed++);
            now = 11; state.Tick();
            Assert.That(failed, Is.EqualTo(1)); Assert.That(state.Enabled, Is.False);
            Prepare(); state.RequestRestart(); state.Reset(); events.Clear(); state.Tick();
            Assert.That(events, Is.Empty);
        }

        [Test] public void CancelledUpdateDoesNotRecreateUnchangedInstalledBundle()
        {
            Prepare(); state.RequestRestart(); state.CancelRestart(); state.Tick();
            Assert.That(events, Is.Empty); Assert.That(state.Preparing, Is.False);
        }

        [Test] public void RepeatedReplacementsReleaseViewsAndDoNotReplayPreparationCallbacks()
        {
            int ready = 0;
            state.Prepare(() => true, () => ready++, null);
            state.Receive("shellReady", null, state.ShellId, null);
            for (int i = 0; i < 200; i++)
            {
                state.Show(viewId => viewId, null, null); state.Tick();
                var id = state.CurrentId;
                state.Receive("viewReady", id, null, null);
                state.RequestRestart(); state.Close(); state.Receive("viewCleared", id, null, null);
                state.Receive("shellReady", null, state.ShellId, null);
                Assert.That(state.CurrentId, Is.Null); Assert.That(state.ClosingId, Is.Null);
            }
            Assert.That(ready, Is.EqualTo(1));
            Assert.That(events.FindAll(e => e == "destroy").Count, Is.EqualTo(200));
            Assert.That(events.FindAll(e => e.StartsWith("release:")).Count, Is.EqualTo(200));
        }

        [Test] public void OpenDuringPrepareWaitsAndRejectsSecondOpen()
        {
            state.Prepare(() => true, null, null);
            Assert.That(state.Show(value => value, null, null), Is.True);
            var id = state.CurrentId;
            Assert.That(state.Show(other => other, null, null), Is.False);
            state.Tick(); Assert.That(messages, Is.Empty);
            state.Receive("shellReady", null, "stale", null); Assert.That(messages, Is.Empty);
            state.Receive("shellReady", null, state.ShellId, null);
            Assert.That(messages, Is.EqualTo(new[] { id }));
        }
        [Test] public void CloseQueuesNextViewAndIgnoresStaleMessages()
        {
            Prepare(); state.Show(value => value, null, null); state.Tick(); var a = state.CurrentId;
            state.Close(); state.Show(value => value, null, null); var b = state.CurrentId; state.Tick();
            Assert.That(messages.Count, Is.EqualTo(2)); // A and clear A
            state.Receive("viewLoadError", a, null, "late");
            state.Receive("viewReady", a, null, null);
            Assert.That(state.CurrentId, Is.EqualTo(b)); Assert.That(state.Visible, Is.False);
            state.Receive("viewCleared", a, null, null);
            Assert.That(messages[2], Is.EqualTo(b));
            state.Receive("viewReady", b, null, null);
            Assert.That(state.Visible, Is.True);
            state.Receive("viewCleared", a, null, null);
            Assert.That(state.CurrentId, Is.EqualTo(b));
            Assert.That(events.FindAll(e => e == "closed").Count, Is.EqualTo(1));
        }
        [Test] public void FailedPrepareDoesNotInvokeReadyAndCanRetry()
        {
            int ready = 0, failed = 0;
            Assert.That(state.Prepare(() => false, () => ready++, error => failed++), Is.False);
            Assert.That(ready, Is.Zero); Assert.That(failed, Is.EqualTo(1));
            Prepare(); Assert.That(state.Enabled, Is.True);
        }
        [Test] public void TransportFailureAndTimeoutReleaseState()
        {
            Prepare(); transport = false; int failed = 0;
            state.Show(value => value, null, error => failed++); state.Tick();
            Assert.That(failed, Is.EqualTo(1)); Assert.That(state.Enabled, Is.False);
            transport = true; state.Prepare(() => true, null, error => failed++);
            now = 11; state.Tick();
            Assert.That(failed, Is.EqualTo(2)); Assert.That(state.Enabled, Is.False);
        }
        [Test] public void FailedViewCanBeOpenedAgainAndClosedBeforeDispatch()
        {
            Prepare(); state.Show(value => value, null, null); state.Tick();
            state.Receive("viewLoadError", state.CurrentId, null, "failure");
            Assert.That(state.CanShow, Is.True);
            state.Show(value => value, null, null); state.Close(); state.Tick();
            Assert.That(messages.Count, Is.EqualTo(1)); Assert.That(state.ClosingId, Is.Null);
        }
        [Test] public void DuplicateReadyAndEmptyControlIdsDoNotReplayEvents()
        {
            state.Receive("shellError", null, null, "stale");
            state.Receive("viewLoadError", null, null, "stale");
            state.Receive("viewCleared", null, null, null);
            Assert.That(events, Is.Empty);
            Prepare(); state.Show(value => value, null, null); state.Tick();
            state.Receive("viewReady", state.CurrentId, null, null);
            state.Receive("viewReady", state.CurrentId, null, null);
            Assert.That(events.FindAll(e => e == "show").Count, Is.EqualTo(1));
        }
        [Test] public void RendererFailureAfterReadyReleasesTheViewAndAllowsPreparationAgain()
        {
            Prepare(); int ready = 0, failed = 0;
            state.Show(value => value, () => ready++, error => failed++); state.Tick();
            state.Receive("viewReady", state.CurrentId, null, null);
            state.Fail("Renderer terminated");
            Assert.That(state.Enabled, Is.False); Assert.That(state.CurrentId, Is.Null);
            Assert.That(ready, Is.EqualTo(1)); Assert.That(failed, Is.EqualTo(1));
            Assert.That(events.FindAll(value => value == "closed").Count, Is.EqualTo(1));
            Prepare(); Assert.That(state.CanShow, Is.True);
        }
        [Test] public void OneHundredCyclesReleaseEveryAcceptedView()
        {
            Prepare();
            for (int i = 0; i < 100; i++)
            {
                Assert.That(state.Show(value => value, null, null), Is.True); state.Tick();
                var id = state.CurrentId;
                state.Receive("viewReady", id, null, null); state.Close();
                state.Receive("viewCleared", id, null, null);
                Assert.That(state.CurrentId, Is.Null); Assert.That(state.Visible, Is.False);
            }
            Assert.That(events.FindAll(e => e.StartsWith("release:")).Count, Is.EqualTo(100));
        }
    }
}
