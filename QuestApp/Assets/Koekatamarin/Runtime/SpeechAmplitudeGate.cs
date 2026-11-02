using System;
using System.Collections.Generic;

namespace Koekatamarin
{
    // A level gate, not a speech classifier. Keep leading consonants and brief pauses.
    public sealed class SpeechAmplitudeGate
    {
        public const int PreRollBlocks = 6; // 192 ms at 16 kHz / 512 samples.
        public const int ReleaseBlocks = 16; // 512 ms below the selected level.
        public const int DefaultSetting = 3;
        static readonly float[] levels = { 0, .001f, .002f, .004f, .008f, .016f, .032f, .064f };
        public static int SettingCount => levels.Length;
        public static float Threshold(int setting) => levels[Math.Max(0, Math.Min(levels.Length - 1, setting))];
        public static float Decibels(float level) => (float)(20 * Math.Log10(Math.Max(level, .000001f)));
        public static float MeterPosition(float level) => Math.Max(0, Math.Min(1, (Decibels(level) + 72) / 72));
        public static float Rms(float[] samples)
        {
            double sum = 0;
            foreach (float value in samples) sum += value * value;
            return (float)Math.Sqrt(sum / samples.Length);
        }

        readonly Queue<float[]> leading = new Queue<float[]>();
        bool open;
        int quietBlocks;
        public void Reset() { leading.Clear(); open = false; quietBlocks = 0; }

        public void Process(float[] block, float threshold, Action<float[]> accept, Action ended)
        {
            if (block == null || block.Length != 512) throw new ArgumentException("Expected 512 mono samples");
            bool audible = threshold <= 0 || Rms(block) >= threshold;
            if (!open)
            {
                if (!audible)
                {
                    leading.Enqueue(block);
                    if (leading.Count > PreRollBlocks) leading.Dequeue();
                    return;
                }
                open = true;
                while (leading.Count > 0) accept(leading.Dequeue());
            }
            accept(block);
            quietBlocks = audible ? 0 : quietBlocks + 1;
            if (quietBlocks < ReleaseBlocks) return;
            ended();
            Reset();
        }
    }
}
