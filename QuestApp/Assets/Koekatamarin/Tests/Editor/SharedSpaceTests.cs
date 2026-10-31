using NUnit.Framework;
using UnityEngine;

namespace Koekatamarin.Tests
{
    public sealed class SharedSpaceTests
    {
        [Test]
        public void TwoDifferentTrackingOriginsMapTheSameShotToTheSamePhysicalLocation()
        {
            var sender = new SharedSpace();
            sender.Begin(); sender.Capture(new Vector3(2, .5f, 3), out _);
            sender.Capture(new Vector3(2, .5f, 4), out _);
            var receiver = new SharedSpace();
            receiver.Begin(); receiver.Capture(new Vector3(-4, 1, 7), out _);
            receiver.Capture(new Vector3(-5, 1, 7), out _);
            Assert.That(sender.Ready && receiver.Ready, Is.True);
            var worldShot = new Pose(new Vector3(2.7f, 1.3f, 4.2f), Quaternion.Euler(0, 30, 0));
            var encoded = sender.ToShared(worldShot);
            Assert.That(Vector3.Distance(encoded.position, new Vector3(.7f, .8f, 1.2f)), Is.LessThan(.0001));
            var decoded = receiver.ToWorld(encoded.position, Mathf.DeltaAngle(0, encoded.rotation.eulerAngles.y));
            Assert.That(Vector3.Distance(decoded.position, new Vector3(-5.2f, 1.8f, 7.7f)), Is.LessThan(.0001));
            Assert.That(Quaternion.Angle(decoded.rotation, Quaternion.Euler(0, -60, 0)), Is.LessThan(.001));
            var back = receiver.ToShared(decoded);
            Assert.That(Vector3.Distance(back.position, encoded.position), Is.LessThan(.0001));
        }

        [Test]
        public void CalibrationRejectsShortBaselineAndNonFinitePointsAndCanBeCleared()
        {
            var space = new SharedSpace();
            space.Begin();
            Assert.That(space.Capture(new Vector3(float.NaN, 0, 0), out _), Is.False);
            Assert.That(space.Current, Is.EqualTo(SharedSpace.Step.Origin));
            space.Capture(Vector3.zero, out _);
            Assert.That(space.Capture(new Vector3(.2f, 3, 0), out _), Is.False);
            Assert.That(space.Capture(new Vector3(0, 0, 6), out _), Is.False);
            Assert.That(space.Capture(new Vector3(0, .1f, 1), out _), Is.True);
            Assert.That(space.Ready, Is.True);
            space.Clear();
            Assert.That(space.Ready || space.Capturing, Is.False);
        }
    }
}
