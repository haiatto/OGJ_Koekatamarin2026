using UnityEngine;

namespace Koekatamarin
{
    // Move the tracking origin, never the tracked eye/hand anchors. MR restores the original origin.
    public sealed class PlayerMotion
    {
        Vector3 savedPosition;
        Quaternion savedRotation;
        bool active;
        bool turnReady = true;
        public void Enter(Transform origin)
        { savedPosition = origin.position; savedRotation = origin.rotation; active = true; turnReady = false; }
        public void Exit(Transform origin)
        { if (active) origin.SetPositionAndRotation(savedPosition, savedRotation); active = false; }
        public static Pose VoicePose(Vector3 eyes, Quaternion gaze)
        { return new Pose(eyes + gaze * new Vector3(0, -.08f, .4f), gaze); }
        public void Step(Transform origin, Transform head, Vector2 move, float turn, float dt, bool allowed)
        {
            if (!active || !allowed) { turnReady = false; return; }
            if (Mathf.Abs(turn) < .3f) turnReady = true;
            if (turnReady && Mathf.Abs(turn) > .7f)
            { origin.RotateAround(head.position, Vector3.up, Mathf.Sign(turn) * 30); turnReady = false; }
            move = Vector2.ClampMagnitude(move, 1);
            if (move.magnitude < .2f) return;
            var yaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
            origin.position += yaw * new Vector3(move.x, 0, move.y) * (1.5f * Mathf.Min(dt, .05f));
        }
    }
}
