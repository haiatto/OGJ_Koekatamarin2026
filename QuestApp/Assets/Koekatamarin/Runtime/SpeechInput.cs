using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Koekatamarin
{
    public interface ISpeechRecognizer : IDisposable
    {
        event Action<string, string, bool> Transcript; // utterance ID, text, final
        void AcceptSamples(float[] mono16k);
    }

    // Microphone diagnostic now; ASR adapters plug into the same final-transcript path.
    // No recordings are saved and no audio is sent onto the LAN.
    public sealed class SpeechInput : MonoBehaviour
    {
        AudioClip clip;
        readonly float[] samples = new float[1024];
        public float Level { get; private set; }
        public string Status { get; private set; } = "マイク停止 / 音声認識モデル未導入";
        public bool Running => clip != null;
        public void Toggle()
        {
            if (Running) { Stop(); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => StartCapture();
                callbacks.PermissionDenied += _ => Status = "マイクが許可されていません";
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                return;
            }
#endif
            StartCapture();
        }
        void StartCapture()
        {
            if (!isActiveAndEnabled || Running) return;
            if (Microphone.devices.Length == 0) { Status = "マイクが見つかりません"; return; }
            clip = Microphone.Start(null, true, 1, 16000);
            Status = clip != null ? "マイク確認中 / 音声認識モデル未導入" : "マイク開始失敗";
        }
        void Update()
        {
            if (clip == null) return;
            int position = Microphone.GetPosition(null);
            if (position < samples.Length) return;
            clip.GetData(samples, position - samples.Length);
            float sum = 0;
            foreach (float sample in samples) sum += sample * sample;
            Level = Mathf.Sqrt(sum / samples.Length);
        }
        public void Stop()
        {
            if (clip != null) { Microphone.End(null); Destroy(clip); }
            clip = null;
            Level = 0;
            Status = "マイク停止 / 音声認識モデル未導入";
        }
        void OnApplicationPause(bool pause) { if (pause) Stop(); }
        void OnDisable() { Stop(); }
    }
}
