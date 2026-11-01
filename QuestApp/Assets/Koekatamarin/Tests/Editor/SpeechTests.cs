using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Koekatamarin.Tests
{
    public sealed class SpeechTests
    {
        [Test]
        public void OriginalPrefixDifferenceIncludesCorrectionsButNotDuplicateFinals()
        {
            var delta = new TranscriptDelta();
            Assert.That(delta.Accept("今日"), Is.EqualTo("今日"));
            Assert.That(delta.Accept("今日は晴れ"), Is.EqualTo("は晴れ"));
            Assert.That(delta.Accept("今日は雨"), Is.EqualTo("雨"));
            Assert.That(delta.Accept("今日は雨"), Is.Empty);
            Assert.That(delta.Accept("今日"), Is.Empty);
            Assert.That(delta.Accept("今日は"), Is.EqualTo("は"));
            delta.Reset();
            Assert.That(delta.Accept("今日は"), Is.EqualTo("今日は"));
        }

        [Test]
        public void ChangedSupplementaryCharacterRemainsValidUnicode()
        {
            var delta = new TranscriptDelta();
            delta.Accept("声😀");
            Assert.That(delta.Accept("声😁"), Is.EqualTo("😁"));
            string longText = new string('あ', 100);
            delta.Reset();
            delta.Accept(longText);
            Assert.That(delta.Accept(longText + "続き"), Is.EqualTo("続き"), "Diff must not be capped at LAN text length");
        }

        [Test]
        public void GrowingWindowsPreserveEverySampleAcrossContextBoundaryAndReset()
        {
            var window = new IncrementalSpeechWindow();
            for (int offset = 0; offset < IncrementalSpeechWindow.MaxSamples; offset += 512)
            {
                var block = new float[512];
                for (int i = 0; i < block.Length; i++) block[i] = offset + i;
                var snapshot = window.Append(block);
                if ((offset + 512) % IncrementalSpeechWindow.StepSamples != 0)
                { Assert.That(snapshot, Is.Null); continue; }
                Assert.That(snapshot.Length, Is.EqualTo(offset + 512));
                for (int i = 0; i < snapshot.Length; i++) Assert.That(snapshot[i], Is.EqualTo((float)i));
            }
            Assert.That(window.Complete, Is.True);
            window.Reset();
            float[] silence = null;
            for (int i = 0; i < 16; i++) silence = window.Append(new float[512]);
            Assert.That(silence.Length, Is.EqualTo(IncrementalSpeechWindow.StepSamples));
            Assert.That(silence, Is.All.Zero, "No previous audio may survive reset; silence is still decoded");
        }

        [TestCase(16000)]
        [TestCase(44100)]
        [TestCase(48000)]
        public void ResamplingPreservesTimeAndChunkContinuity(int rate)
        {
            var source = new float[rate * 2];
            for (int i = 0; i < rate; i++) source[2 * i] = source[2 * i + 1] = (float)Math.Sin(2 * Math.PI * 440 * i / rate);
            var whole = new List<float>();
            var split = new List<float>();
            new SpeechResampler(rate, 2, chunk => whole.AddRange(chunk)).Push(source);
            var resampler = new SpeechResampler(rate, 2, chunk => split.AddRange(chunk));
            for (int i = 0; i < source.Length; i += 274)
            {
                var chunk = new float[Math.Min(274, source.Length - i)];
                Array.Copy(source, i, chunk, 0, chunk.Length);
                resampler.Push(chunk);
            }
            Assert.That(split.Count, Is.EqualTo(31 * 512));
            Assert.That(split, Is.EqualTo(whole));
            for (int i = 0; i < split.Count; i++)
                Assert.That(split[i], Is.EqualTo(Math.Sin(2 * Math.PI * 440 * i / 16000)).Within(.002));
        }

        [UnityTest]
        public IEnumerator SenseVoiceProducesInterimJapaneseWithoutVadAndResets()
        { return RecognizeFixture(SpeechModels.Model.SenseVoice); }

        [UnityTest]
        public IEnumerator ReazonSpeechRecognizesJapaneseFixtureAndResets()
        { return RecognizeFixture(SpeechModels.Model.ReazonSpeech); }

        static IEnumerator RecognizeFixture(SpeechModels.Model model)
        {
            string models = Path.Combine(Application.streamingAssetsPath, "Speech");
            var files = SpeechModels.ModelFiles(model);
            var hashes = SpeechModels.ModelHashes(model);
            for (int i = 0; i < files.Length; i++)
                Assert.That(SpeechModels.Valid(Path.Combine(models, files[i]), hashes[i]), Is.True,
                    "Run python tools/setup_speech.py first");
            string fixture = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../.work/asr/unpacked/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/test_wavs/ja.wav"));
            float[] samples = ReadPcm16Wave(fixture);
            using var engine = new LocalSpeechRecognizer(models, model);
            var deadline = Stopwatch.StartNew();
            while (!engine.Ready && engine.Error == null && deadline.Elapsed.TotalSeconds < 60) yield return null;
            Assert.That(engine.Error, Is.Null);
            Assert.That(engine.Ready, Is.True, "Native models must actually initialize");
            int updates = 0;
            int fedSamples = 0;
            int firstReceivedAtSample = int.MaxValue;
            double firstAudioSeconds = double.PositiveInfinity;
            string combinedHypotheses = "";
            double maxDecode = 0;
            void Drain()
            {
                while (engine.TryRead(out var result))
                {
                    updates++;
                    firstReceivedAtSample = Math.Min(firstReceivedAtSample, fedSamples);
                    firstAudioSeconds = Math.Min(firstAudioSeconds, result.AudioSeconds);
                    combinedHypotheses += result.Text;
                    maxDecode = Math.Max(maxDecode, result.DecodeSeconds);
                    Assert.That(result.Delta, Is.Not.Empty);
                }
            }
            // Real microphone cadence: the test must receive text before speech ends,
            // not merely run a complete prerecorded utterance through an offline decoder.
            for (int i = 0; i < samples.Length + 16000; i += 512)
            {
                var block = new float[512];
                if (i < samples.Length) Array.Copy(samples, i, block, 0, Math.Min(512, samples.Length - i));
                engine.Accept(block);
                fedSamples = i + 512;
                Drain();
                yield return new WaitForSecondsRealtime(.032f);
            }
            float until = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < until) { Drain(); yield return null; }
            Assert.That(engine.Error, Is.Null);
            Assert.That(updates, Is.GreaterThan(1), "Must produce multiple interim updates");
            Assert.That(firstReceivedAtSample, Is.LessThan(samples.Length), "Result must arrive while the fixture is still speaking");
            Assert.That(firstAudioSeconds, Is.LessThan(samples.Length / 16000.0 - .5),
                "First transcript must precede end of the spoken fixture");
            Assert.That(combinedHypotheses, Does.Contain("中学").And.Contain("弁当"));
            Assert.That(engine.DroppedWindows, Is.Zero);
            UnityEngine.Debug.Log("ASR_INTERIM_FIXTURE model=" + SpeechModels.Name(model) +
                " updates=" + updates + " firstAudio=" + firstAudioSeconds.ToString("F3") +
                "s maxDecode=" + maxDecode.ToString("F3") + "s");
            engine.Reset();
            Assert.That(engine.TryRead(out _), Is.False);
            for (int i = 0; i < 80; i++)
            { engine.Accept(new float[512]); yield return new WaitForSecondsRealtime(.032f); }
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(engine.TryRead(out _), Is.False, "Silence and reset must not emit stale speech");
            engine.Dispose();
            while (!engine.Finished && deadline.Elapsed.TotalSeconds < 95) yield return null;
            Assert.That(engine.Finished, Is.True);
        }

        static float[] ReadPcm16Wave(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"));
            reader.ReadInt32(); reader.ReadBytes(4);
            int channels = 0, rate = 0, bits = 0;
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string chunk = new string(reader.ReadChars(4));
                int length = reader.ReadInt32();
                long end = reader.BaseStream.Position + length + (length % 2);
                if (chunk == "fmt ")
                {
                    Assert.That(reader.ReadInt16(), Is.EqualTo(1));
                    channels = reader.ReadInt16(); rate = reader.ReadInt32();
                    reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16();
                }
                else if (chunk == "data")
                {
                    Assert.That((channels, rate, bits), Is.EqualTo((1, 16000, 16)));
                    var data = new float[length / 2];
                    for (int i = 0; i < data.Length; i++) data[i] = reader.ReadInt16() / 32768f;
                    return data;
                }
                reader.BaseStream.Position = end;
            }
            throw new InvalidDataException("Missing WAV data");
        }
    }
}
