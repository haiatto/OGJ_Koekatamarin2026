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
            public readonly string Delta;
            public readonly double DecodeSeconds;
            public readonly double AudioSeconds;
            public Result(string text, string delta, double decode, double audio)
            { Text = text; Delta = delta; DecodeSeconds = decode; AudioSeconds = audio; }
        }

        readonly object gate = new object();
        readonly Queue<float[]> audio = new Queue<float[]>();
        readonly Queue<Result> results = new Queue<Result>();
        readonly Thread thread;
        readonly string directory;
        readonly SpeechModels.Model model;
        int generation;
        bool stopping;
        float minimumLevel = SpeechAmplitudeGate.Threshold(SpeechAmplitudeGate.DefaultSetting);
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

        public void SetMinimumLevel(float level)
        {
            lock (gate)
            {
                minimumLevel = level;
                generation++; audio.Clear(); results.Clear(); Monitor.PulseAll(gate);
            }
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
                var window = new IncrementalSpeechWindow();
                var difference = new TranscriptDelta();
                var levelGate = new SpeechAmplitudeGate();
                int current = -1;
                void Decode(float[] snapshot)
                {
                    if (snapshot == null) return;
                    if (!Array.TrueForAll(snapshot, value => value == 0f))
                    {
                        State = "聞き取り中…";
                        using var stream = recognizer.CreateStream();
                        stream.AcceptWaveform(IncrementalSpeechWindow.SampleRate, snapshot);
                        var timer = Stopwatch.StartNew();
                        recognizer.Decode(stream);
                        string text = (stream.Result.Text ?? "").Trim();
                        timer.Stop();
                        lock (gate)
                        {
                            if (stopping || current != generation) return;
                            string delta = difference.Accept(text);
                            if (delta.Length != 0)
                            {
                                if (results.Count == 8) results.Dequeue();
                                results.Enqueue(new Result(text, delta, timer.Elapsed.TotalSeconds, snapshot.Length / 16000.0));
                            }
                        }
                    }
                    if (window.Complete) { window.Reset(); difference.Reset(); }
                }
                void Feed(float[] block) { Decode(window.Append(block)); }
                void EndUtterance()
                {
                    Decode(window.Flush());
                    window.Reset(); difference.Reset(); State = "音量待ち";
                }
                Ready = true;
                State = "話してください";
                while (true)
                {
                    float[] samples;
                    int incoming;
                    float threshold;
                    lock (gate)
                    {
                        while (!stopping && audio.Count == 0 && current == generation) Monitor.Wait(gate);
                        if (stopping) break;
                        incoming = generation;
                        threshold = minimumLevel;
                        samples = audio.Count > 0 ? audio.Dequeue() : null;
                    }
                    if (current != incoming)
                    { window.Reset(); difference.Reset(); levelGate.Reset(); current = incoming; State = "話してください"; }
                    if (samples == null) continue;
                    levelGate.Process(samples, threshold, Feed, EndUtterance);
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
