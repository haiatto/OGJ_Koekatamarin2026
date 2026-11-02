using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Koekatamarin.Tests
{
    public sealed class SpeechAmplitudeGateTests
    {
        static float[] Block(float level) => Enumerable.Repeat(level, 512).ToArray();

        [Test]
        public void QuietBackgroundDoesNotProduceRecognitionInput()
        {
            var gate = new SpeechAmplitudeGate();
            int accepted = 0, ended = 0;
            for (int i = 0; i < 1000; i++) gate.Process(Block(.001f), .004f, _ => accepted++, () => ended++);
            Assert.That(accepted, Is.Zero);
            Assert.That(ended, Is.Zero);
        }

        [Test]
        public void ShortSpeechKeepsLeadingAudioAndFlushesTailOnceThenStops()
        {
            var gate = new SpeechAmplitudeGate();
            var window = new IncrementalSpeechWindow();
            var snapshots = new List<float[]>();
            int ended = 0;
            void Feed(float[] block) { var value = window.Append(block); if (value != null) snapshots.Add(value); }
            void End() { var tail = window.Flush(); if (tail != null) snapshots.Add(tail); ended++; window.Reset(); }
            for (int i = 0; i < 20; i++) gate.Process(Block(.001f), .004f, Feed, End);
            gate.Process(Block(.02f), .004f, Feed, End);
            for (int i = 0; i < 100; i++) gate.Process(Block(.001f), .004f, Feed, End);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(snapshots.Count, Is.EqualTo(2));
            Assert.That(snapshots[0][6 * 512], Is.EqualTo(.02f));
            Assert.That(snapshots[1].Length, Is.EqualTo((6 + 1 + 16) * 512));
        }

        [Test]
        public void BriefPauseKeepsOneUtteranceAndOffPassesQuietAudio()
        {
            var gate = new SpeechAmplitudeGate();
            int accepted = 0, ended = 0;
            gate.Process(Block(.01f), .004f, _ => accepted++, () => ended++);
            for (int i = 0; i < 15; i++) gate.Process(Block(.001f), .004f, _ => accepted++, () => ended++);
            gate.Process(Block(.01f), .004f, _ => accepted++, () => ended++);
            Assert.That(ended, Is.Zero);
            gate.Reset(); accepted = 0;
            for (int i = 0; i < 100; i++) gate.Process(Block(.0001f), 0, _ => accepted++, () => ended++);
            Assert.That(accepted, Is.EqualTo(100));
            Assert.That(ended, Is.Zero);
        }

        [Test]
        public void ResetDiscardsPreviousLeadingAudioAndMeterUsesSameScale()
        {
            var gate = new SpeechAmplitudeGate();
            int accepted = 0;
            gate.Process(Block(.001f), .004f, _ => accepted++, () => {});
            gate.Reset();
            gate.Process(Block(.01f), .004f, _ => accepted++, () => {});
            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(SpeechAmplitudeGate.Decibels(.001f), Is.EqualTo(-60).Within(.01));
            Assert.That(SpeechAmplitudeGate.MeterPosition(0), Is.Zero);
            Assert.That(SpeechAmplitudeGate.MeterPosition(1), Is.EqualTo(1));
        }
    }
}
