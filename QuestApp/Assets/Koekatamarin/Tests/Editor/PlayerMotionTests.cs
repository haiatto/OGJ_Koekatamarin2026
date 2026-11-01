using NUnit.Framework;
using UnityEngine;

namespace Koekatamarin.Tests
{
    public sealed class PlayerMotionTests
    {
        [Test]
        public void VoiceOriginFollowsTranslatedRotatedAndPitchedHeadWithoutSideOffset()
        {
            var eyes = new Vector3(7, 1.7f, -3);
            var gaze = Quaternion.Euler(-25, 110, 0);
            var pose = PlayerMotion.VoicePose(eyes, gaze);
            var local = Quaternion.Inverse(gaze) * (pose.position - eyes);
            Assert.That(local.x, Is.EqualTo(0).Within(.0001));
            Assert.That(local.z, Is.EqualTo(.4f).Within(.0001));
            Assert.That(Quaternion.Angle(gaze, pose.rotation), Is.LessThan(.001));
        }

        [Test]
        public void VrMovementAndSnapTurnRestoreOriginWithoutLosingPhysicalHeadPose()
        {
            var root = new GameObject("Origin");
            var head = new GameObject("Head");
            try
            {
                head.transform.SetParent(root.transform);
                head.transform.localPosition = new Vector3(.3f, 1.6f, .2f);
                var tracked = head.transform.localPosition;
                var motion = new PlayerMotion();
                motion.Enter(root.transform);
                motion.Step(root.transform, head.transform, Vector2.up, 0, .02f, false);
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero), "MR/paused input cannot move");
                motion.Step(root.transform, head.transform, Vector2.up, 0, .02f, true);
                Assert.That(root.transform.position.z, Is.EqualTo(.03f).Within(.0001));
                var beforeTurn = head.transform.position;
                motion.Step(root.transform, head.transform, Vector2.zero, 1, .02f, true);
                Assert.That(Vector3.Distance(head.transform.position, beforeTurn), Is.LessThan(.0001));
                Assert.That(root.transform.eulerAngles.y, Is.EqualTo(30).Within(.001));
                motion.Step(root.transform, head.transform, Vector2.zero, 1, .02f, true);
                Assert.That(root.transform.eulerAngles.y, Is.EqualTo(30).Within(.001), "Held stick must not spin repeatedly");
                motion.Exit(root.transform);
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.identity), Is.LessThan(.001));
                Assert.That(head.transform.localPosition, Is.EqualTo(tracked));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
