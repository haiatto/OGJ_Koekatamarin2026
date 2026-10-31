namespace Koekatamarin
{
    // A fresh MR/resume attempt waits for asynchronous passthrough initialization.
    // Cancellation prevents a late completion from suppressing the boundary during a VR transition.
    public sealed class MrBoundaryGate
    {
        public enum State { Idle, Waiting, Ready, TimedOut }
        public State Current { get; private set; }
        float deadline;
        public void Begin(float now) { deadline = now + 10f; Current = State.Waiting; }
        public void Cancel() { Current = State.Idle; }
        public bool Tick(float now, bool passthroughReady)
        {
            if (Current != State.Waiting) return false;
            if (now >= deadline) { Current = State.TimedOut; return false; }
            if (!passthroughReady) return false;
            Current = State.Ready;
            return true;
        }
    }
}
