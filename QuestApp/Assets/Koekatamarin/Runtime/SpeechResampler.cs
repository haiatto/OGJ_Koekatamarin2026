using System;

namespace Koekatamarin
{
    // Continuous linear resampling, preserving fractional position between microphone reads.
    // Produces exactly 512 mono samples per VAD window, without rereading the ring buffer.
    public sealed class SpeechResampler
    {
        readonly double step;
        readonly int channels;
        readonly Action<float[]> output;
        float[] window = new float[512];
        int used;
        long sourceIndex;
        double nextOutput;
        float previous;

        public SpeechResampler(int sampleRate, int channels, Action<float[]> output)
        {
            if (sampleRate <= 0 || channels <= 0) throw new ArgumentOutOfRangeException();
            step = sampleRate / 16000.0;
            this.channels = channels;
            this.output = output;
        }

        public void Push(float[] interleaved)
        {
            if (interleaved.Length % channels != 0) throw new ArgumentException("Incomplete audio frame");
            for (int offset = 0; offset < interleaved.Length; offset += channels, sourceIndex++)
            {
                float current = 0;
                for (int c = 0; c < channels; c++) current += interleaved[offset + c];
                current /= channels;
                while (nextOutput <= sourceIndex)
                {
                    float fraction = (float)(nextOutput - (sourceIndex - 1));
                    window[used++] = sourceIndex == 0 ? current : previous + (current - previous) * fraction;
                    nextOutput += step;
                    if (used != window.Length) continue;
                    output(window);
                    window = new float[512];
                    used = 0;
                }
                previous = current;
            }
        }
    }
}
