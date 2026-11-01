using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SherpaOnnx;

namespace Koekatamarin
{
    // Native models are owned exclusively by this worker. No Unity API or microphone access here.
    // Audio and results are bounded; Stop/Reset invalidate even a decode already in flight.
    public sealed class LocalSpeechRecognizer : IDisposable
    {
        public readonly struct Result
        {
            public readonly string Text;
            public readonly double DecodeSeconds;
            public readonly double AudioSeconds;
            public Result(string text, double decode, double audio)
            { Text = text; DecodeSeconds = decode; AudioSeconds = audio; }
        }

        readonly object gate = new object();
        readonly Queue<float[]> audio = new Queue<float[]>();
        readonly Queue<Result> results = new Queue<Result>();
        readonly Thread thread;
        readonly string directory;
        readonly SpeechModels.Model model;
        int generation;
        bool stopping;
        public volatile bool Ready;
        public volatile bool Finished;
        public volatile string Error;
        public volatile string State = "音声モデルを読み込み中";
        public int DroppedWindows { get; private set; }

        [DllImport("onnxruntime", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr OrtGetApiBase();

        public LocalSpeechRecognizer(string directory, SpeechModels.Model model = SpeechModels.Model.SenseVoice)
        {
            SpeechNativeLoader.PrepareEditor();
            this.directory = directory;
            this.model = model;
            thread = new Thread(Run) { IsBackground = true, Name = "Koekatamarin Japanese ASR" };
            thread.Start();
        }

        public void Reset()
        {
            lock (gate) { generation++; audio.Clear(); results.Clear(); Monitor.PulseAll(gate); }
        }

        public void Accept(float[] mono16k)
        {
            if (mono16k.Length != 512) throw new ArgumentException("Expected 512 mono samples");
            lock (gate)
            {
                if (stopping || !Ready) return;
                if (audio.Count >= 96)
                {
                    DroppedWindows += audio.Count;
                    generation++; audio.Clear(); results.Clear();
                }
                audio.Enqueue(mono16k);
                Monitor.PulseAll(gate);
            }
        }

        public bool TryRead(out Result result)
        {
            lock (gate)
            {
                if (results.Count != 0) { result = results.Dequeue(); return true; }
                result = default; return false;
            }
        }

        void Run()
        {
            try
            {
                // Load the dependency through Unity's plugin resolver before loading the C API.
                if (OrtGetApiBase() == IntPtr.Zero) throw new InvalidOperationException("ONNX Runtime unavailable");
                var config = new OfflineRecognizerConfig(true);
                if (model == SpeechModels.Model.ReazonSpeech)
                {
                    config.ModelConfig.Transducer.Encoder = Path.Combine(directory, SpeechModels.ReazonFiles[0]);
                    config.ModelConfig.Transducer.Decoder = Path.Combine(directory, SpeechModels.ReazonFiles[1]);
                    config.ModelConfig.Transducer.Joiner = Path.Combine(directory, SpeechModels.ReazonFiles[2]);
                    config.ModelConfig.Tokens = Path.Combine(directory, SpeechModels.ReazonFiles[3]);
                    config.DecodingMethod = "modified_beam_search";
                    config.MaxActivePaths = 4;
                }
                else
                {
                    config.ModelConfig.SenseVoice.Model = Path.Combine(directory, SpeechModels.Files[0]);
                    config.ModelConfig.SenseVoice.Language = "ja";
                    config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
                    config.ModelConfig.Tokens = Path.Combine(directory, SpeechModels.Files[1]);
                }
                config.ModelConfig.NumThreads = 2;
                using var recognizer = new OfflineRecognizer(config);
                var vadConfig = new VadModelConfig(true);
                vadConfig.SileroVad.Model = Path.Combine(directory, SpeechModels.Files[2]);
                vadConfig.SileroVad.MinSilenceDuration = .45f;
                vadConfig.SileroVad.MinSpeechDuration = .20f;
                vadConfig.SileroVad.MaxSpeechDuration = 6f;
                using var vad = new VoiceActivityDetector(vadConfig, 12f);
                int current = -1;
                Ready = true;
                State = "話してください";
                while (true)
                {
                    float[] samples;
                    int incoming;
                    lock (gate)
                    {
                        while (!stopping && audio.Count == 0 && current == generation) Monitor.Wait(gate);
                        if (stopping) break;
                        incoming = generation;
                        samples = audio.Count > 0 ? audio.Dequeue() : null;
                    }
                    if (current != incoming) { vad.Reset(); current = incoming; State = "話してください"; }
                    if (samples == null) continue;
                    vad.AcceptWaveform(samples);
                    State = vad.IsSpeechDetected() ? "聞き取り中…" : "話してください";
                    while (!vad.IsEmpty())
                    {
                        var segment = vad.Front();
                        vad.Pop();
                        if (segment.Samples.Length < 3200) continue;
                        State = "文字に変換中…";
                        using var stream = recognizer.CreateStream();
                        stream.AcceptWaveform(16000, segment.Samples);
                        var timer = Stopwatch.StartNew();
                        recognizer.Decode(stream);
                        string text = LanProtocol.Clean(stream.Result.Text);
                        timer.Stop();
                        lock (gate)
                        {
                            if (stopping || current != generation) break;
                            if (text.Length != 0)
                            {
                                if (results.Count == 8) results.Dequeue();
                                results.Enqueue(new Result(text, timer.Elapsed.TotalSeconds, segment.Samples.Length / 16000.0));
                            }
                        }
                        State = "話してください";
                    }
                }
            }
            catch (Exception e) { Error = e.GetType().Name + ": " + e.Message; State = "音声認識の初期化または処理に失敗"; }
            finally { Ready = false; Finished = true; }
        }

        public void Dispose()
        {
            // Never block the Unity render thread on native inference; the worker disposes its models.
            lock (gate) { stopping = true; generation++; audio.Clear(); results.Clear(); Monitor.PulseAll(gate); }
        }
    }
}
