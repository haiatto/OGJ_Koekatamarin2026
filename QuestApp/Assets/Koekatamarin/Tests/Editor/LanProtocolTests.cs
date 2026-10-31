using System;
using NUnit.Framework;

namespace Koekatamarin.Tests
{
    public sealed class LanProtocolTests
    {
        static LanPacket Shot() => new LanPacket { room = 3, peer = Guid.NewGuid().ToString("N"),
            kind = "shot", id = Guid.NewGuid().ToString("N"), text = "こんにちは", style = 2 };

        [Test] public void ValidJapaneseShotIsAccepted()
        { Assert.That(LanProtocol.Valid(Shot(), 3, ""), Is.True); }

        [Test] public void WrongRoomVersionAndSelfAreRejected()
        {
            var p = Shot();
            Assert.That(LanProtocol.Valid(p, 2, ""), Is.False);
            Assert.That(LanProtocol.Valid(p, 3, p.peer), Is.False);
            p.version = 100;
            Assert.That(LanProtocol.Valid(p, 3, ""), Is.False);
        }

        [Test] public void InvalidPayloadCannotReachRenderer()
        {
            var p = Shot();
            foreach (var text in new[] { "", " \t", "line\nbreak", "bad\ud800", new string('あ', 81) })
            { p.text = text; Assert.That(LanProtocol.Valid(p, 3, ""), Is.False, text); }
            p.text = "正常"; p.style = 999;
            Assert.That(LanProtocol.Valid(p, 3, ""), Is.False);
        }

        [Test] public void RetryOfSameEventIsDeduplicatedButRepeatedSpeechIsAllowed()
        {
            var recent = new RecentEvents();
            Assert.That(recent.Accept("peer-a", "utterance-1"), Is.True);
            Assert.That(recent.Accept("peer-a", "utterance-1"), Is.False);
            Assert.That(recent.Accept("peer-a", "utterance-2"), Is.True);
            Assert.That(recent.Accept("peer-b", "utterance-1"), Is.True);
        }

        [Test] public void ReplayWindowHasBoundedMemory()
        {
            var recent = new RecentEvents(2);
            recent.Accept("a", "1"); recent.Accept("a", "2"); recent.Accept("a", "3");
            Assert.That(recent.Accept("a", "2"), Is.False);
            Assert.That(recent.Accept("a", "1"), Is.True);
        }

        [Test] public void TruncationDoesNotSplitEmoji()
        {
            string value = new string('あ', 79) + "😀";
            Assert.That(LanProtocol.Clean(value), Is.EqualTo(new string('あ', 79)));
        }
    }
}
