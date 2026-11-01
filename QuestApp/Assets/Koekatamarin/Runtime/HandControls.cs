using UnityEngine;

namespace Koekatamarin
{
    public sealed class HandControls : MonoBehaviour
    {
        public OVRHand left;
        public OVRHand right;
        public Transform head;
        bool wasRight;
        bool rightArmed;
        bool wasRecall;
        bool dragging;
        Vector3 dragOrigin;
        public bool Click { get; private set; }
        public bool Recall { get; private set; }
        public bool HasPointer { get; private set; }
        public Ray Ray { get; private set; }
        public Vector2 Move { get; private set; }
        static bool Valid(OVRHand hand) => hand != null && hand.IsTracked && hand.IsDataHighConfidence && !hand.IsSystemGestureInProgress;
        static bool Pinch(OVRHand hand, OVRHand.HandFinger finger) => Valid(hand) &&
            hand.GetFingerConfidence(finger) == OVRHand.TrackingConfidence.High && hand.GetFingerIsPinching(finger);
        public void Sample(bool allowMove)
        {
            HasPointer = Valid(right) && right.IsPointerPoseValid;
            if (HasPointer) Ray = new Ray(right.PointerPose.position, right.PointerPose.forward);
            bool pinching = HasPointer && Pinch(right, OVRHand.HandFinger.Index);
            if (!HasPointer) rightArmed = false;
            else if (!pinching) rightArmed = true;
            Click = pinching && !wasRight && rightArmed;
            wasRight = pinching;
            bool recall = Pinch(right, OVRHand.HandFinger.Middle);
            Recall = recall && !wasRecall;
            wasRecall = recall;
            Move = Vector2.zero;
            bool moving = allowMove && Pinch(left, OVRHand.HandFinger.Index);
            if (moving)
            {
                // Tracking-space coordinates avoid feedback from artificial origin motion.
                Vector3 current = left.transform.localPosition;
                // Hand prefab is under an anchor: anchor position contains the tracked wrist pose.
                if (left.transform.parent != null) current = left.transform.parent.localPosition;
                if (!dragging) dragOrigin = current;
                Vector3 delta = current - dragOrigin;
                Vector3 worldDelta = left.transform.parent.parent.TransformVector(delta);
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0, head.eulerAngles.y, 0)) * worldDelta;
                Move = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / .15f, 1);
            }
            dragging = moving;
        }
    }
}
