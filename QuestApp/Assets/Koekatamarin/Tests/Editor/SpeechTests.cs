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
        public IEnumerator JapaneseSpeechPassesVadAndNativeRecognizerThenResetDropsPendingResults()
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
            // Feed at faster-than-real-time with pacing so this also exercises the bounded audio queue.
            for (int i = 0; i < samples.Length + 16000; i += 512)
            {
                var block = new float[512];
                if (i < samples.Length) Array.Copy(samples, i, block, 0, Math.Min(512, samples.Length - i));
                engine.Accept(block);
                yield return null;
            }
            LocalSpeechRecognizer.Result result = default;
            bool received = false;
            while (!(received = engine.TryRead(out result)) && engine.Error == null && deadline.Elapsed.TotalSeconds < 90) yield return null;
            Assert.That(engine.Error, Is.Null);
            Assert.That(received, Is.True, "VAD must produce a real Japanese transcript");
            Assert.That(result.Text, Does.Match("[ぁ-んァ-ヶ一-龯]"));
            Assert.That(result.Text, Does.Contain("中学").And.Contain("弁当"), "Known Japanese fixture must be recognized, not just any Japanese output");
            Assert.That(result.AudioSeconds, Is.GreaterThan(.2));
            Assert.That(engine.DroppedWindows, Is.Zero);
            UnityEngine.Debug.Log("ASR_FIXTURE model=" + SpeechModels.Name(model) + " text=" + result.Text + " decode=" + result.DecodeSeconds.ToString("F3") + "s audio=" + result.AudioSeconds.ToString("F3") + "s");
            engine.Reset();
            Assert.That(engine.TryRead(out _), Is.False);
            for (int i = 0; i < 50; i++) { engine.Accept(new float[512]); yield return null; }
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
