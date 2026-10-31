using NUnit.Framework;

namespace Koekatamarin.Tests
{
    public sealed class MrBoundaryGateTests
    {
        [Test]
        public void DelayedPassthroughAfterResumeCanRequestSuppressionOnce()
        {
            var gate = new MrBoundaryGate();
            gate.Begin(100);
            Assert.That(gate.Tick(100, false), Is.False);
            Assert.That(gate.Tick(102, false), Is.False);
            Assert.That(gate.Tick(104, true), Is.True);
            Assert.That(gate.Tick(105, true), Is.False);
            Assert.That(gate.Current, Is.EqualTo(MrBoundaryGate.State.Ready));
        }

        [Test]
        public void VrTransitionOrSleepCancelsOldInitialization()
        {
            var gate = new MrBoundaryGate();
            gate.Begin(100); gate.Cancel();
            Assert.That(gate.Tick(102, true), Is.False);
            Assert.That(gate.Current, Is.EqualTo(MrBoundaryGate.State.Idle));
            gate.Begin(500);
            Assert.That(gate.Tick(503, true), Is.True, "Resume starts a fresh wait, not the cancelled deadline");
        }

        [Test]
        public void TimeoutNeverTriggersALateBoundaryRequest()
        {
            var gate = new MrBoundaryGate();
            gate.Begin(100);
            Assert.That(gate.Tick(110, false), Is.False);
            Assert.That(gate.Current, Is.EqualTo(MrBoundaryGate.State.TimedOut));
            Assert.That(gate.Tick(112, true), Is.False);
            gate.Begin(200);
            Assert.That(gate.Tick(201, true), Is.True);
        }
    }
}
