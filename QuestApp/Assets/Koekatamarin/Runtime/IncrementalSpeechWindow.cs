using System;

namespace Koekatamarin
{
    // A bounded growing recording for non-streaming models. No VAD or amplitude gate:
    // decode every 0.512 s, including silence. Start a new context at 12.288 s.
    public sealed class IncrementalSpeechWindow
    {
        public const int SampleRate = 16000;
        public const int StepSamples = 8192;
        public const int MaxSamples = StepSamples * 24;
        readonly float[] samples = new float[MaxSamples];
        int count;
        public bool Complete => count == MaxSamples;

        public float[] Append(float[] block)
        {
            if (block == null || block.Length != 512) throw new ArgumentException("Expected 512 mono samples");
            if (Complete) throw new InvalidOperationException("Reset the completed context first");
            Array.Copy(block, 0, samples, count, block.Length);
            count += block.Length;
            if (count % StepSamples != 0) return null;
            var snapshot = new float[count];
            Array.Copy(samples, snapshot, count);
            return snapshot;
        }

        public void Reset() { count = 0; }
    }
}
