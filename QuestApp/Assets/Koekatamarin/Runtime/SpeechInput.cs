using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Koekatamarin
{
    // Audio stays in memory. Only final text is passed to the experience/LAN layer.
    public sealed class SpeechInput : MonoBehaviour
    {
        public event Action<string> FinalTranscript;
        public event Action Stopped;
        AudioClip clip;
        LocalSpeechRecognizer recognizer;
        SpeechResampler resampler;
        UnityWebRequest copying;
        float[] input;
        int readPosition;
        int requestId;
        bool requested;
        float lastRead;
        float started;
        bool microphoneStarted;
        float nextResult;
        string lastText = "";
        public float Level { get; private set; }
        public string Status { get; private set; } = "音声入力 停止";
        public bool Running => requested;

        public void Toggle()
        {
            if (requested) { Stop(); return; }
            requested = true;
            int id = ++requestId;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Status = "マイクの許可を待っています";
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { if (requested && requestId == id && isActiveAndEnabled) StartCoroutine(StartCapture()); };
                callbacks.PermissionDenied += _ => { if (requestId == id) Fail("マイクが許可されていません"); };
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                return;
            }
#endif
            StartCoroutine(StartCapture());
        }

        IEnumerator StartCapture()
        {
            if (recognizer != null && recognizer.Error != null)
            {
                recognizer.Dispose();
                while (!recognizer.Finished) yield return null;
                recognizer = null;
            }
            if (recognizer == null)
            {
                string target = Path.Combine(Application.persistentDataPath, SpeechModels.CacheVersion);
                try { Directory.CreateDirectory(target); }
                catch (Exception e) { Fail("音声モデルの保存先を作れません: " + e.Message); yield break; }
                for (int i = 0; i < SpeechModels.Files.Length; i++)
                {
                    string destination = Path.Combine(target, SpeechModels.Files[i]);
                    string expected = SpeechModels.Hashes[i];
                    Status = "音声モデルを確認中 " + (i + 1) + "/3";
                    Task<bool> check = Task.Run(() => SpeechModels.Valid(destination, expected));
                    while (!check.IsCompleted) yield return null;
                    if (check.IsFaulted) { Fail("音声モデルを確認できません"); yield break; }
                    if (check.Result) continue;
                    Status = "音声モデルを準備中 " + (i + 1) + "/3";
                    string source = Application.streamingAssetsPath + "/Speech/" + SpeechModels.Files[i];
                    if (!source.Contains("://")) source = new Uri(source).AbsoluteUri;
                    string temporary = destination + ".part";
                    copying = UnityWebRequest.Get(source);
                    copying.downloadHandler = new DownloadHandlerFile(temporary) { removeFileOnAbort = true };
                    yield return copying.SendWebRequest();
                    bool success = copying.result == UnityWebRequest.Result.Success;
                    string error = copying.error;
                    copying.Dispose(); copying = null;
                    if (!success) { Fail("音声モデルの準備に失敗: " + error); yield break; }
                    check = Task.Run(() => SpeechModels.Valid(temporary, expected));
                    while (!check.IsCompleted) yield return null;
                    if (check.IsFaulted || !check.Result) { Fail("音声モデルの検証に失敗"); yield break; }
                    // Move within the same directory avoids copying 239 MB on the render thread.
                    try { if (File.Exists(destination)) File.Delete(destination); File.Move(temporary, destination); }
                    catch (Exception e) { Fail("音声モデルの保存に失敗: " + e.Message); yield break; }
                }
                try { recognizer = new LocalSpeechRecognizer(target); }
                catch (Exception e) { Fail("音声認識を開始できません: " + e.Message); yield break; }
            }
            while (!recognizer.Ready && recognizer.Error == null) { Status = recognizer.State; yield return null; }
            if (recognizer.Error != null) { Fail("音声認識エラー: " + recognizer.Error); yield break; }
            if (!requested || !isActiveAndEnabled) yield break;
            if (Microphone.devices.Length == 0) { Fail("マイクが見つかりません"); yield break; }
            try { clip = Microphone.Start(null, true, 2, 16000); }
            catch (Exception e) { Fail("マイク開始失敗: " + e.Message); yield break; }
            if (clip == null) { Fail("マイク開始失敗"); yield break; }
            input = new float[512 * clip.channels];
            readPosition = 0;
            recognizer.Reset();
            resampler = new SpeechResampler(clip.frequency, clip.channels, recognizer.Accept);
            started = lastRead = Time.realtimeSinceStartup;
            microphoneStarted = false;
            Status = "マイク開始中";
        }

        void Update()
        {
            if (clip == null) return;
            if (recognizer.Error != null) { Fail("音声認識エラー: " + recognizer.Error); return; }
            int position = Microphone.GetPosition(null);
            float now = Time.realtimeSinceStartup;
            if (position <= 0 && !microphoneStarted)
            {
                if (now - started > 5) Fail("マイクから音声が届きません");
                return;
            }
            microphoneStarted = true;
            if (!Microphone.IsRecording(null)) { Fail("マイク接続が切れました"); return; }
            if (now - lastRead > 1.8f)
            {
                // A stalled frame may have overwritten the ring: discard the ambiguous interval.
                readPosition = position;
                recognizer.Reset();
                resampler = new SpeechResampler(clip.frequency, clip.channels, recognizer.Accept);
            }
            lastRead = now;
            int available = (position - readPosition + clip.samples) % clip.samples;
            while (available >= 512)
            {
                if (!clip.GetData(input, readPosition)) { Fail("マイクの読み取りに失敗"); return; }
                readPosition = (readPosition + 512) % clip.samples;
                available -= 512;
                float sum = 0;
                foreach (float sample in input) sum += sample * sample;
                Level = Mathf.Sqrt(sum / input.Length);
                resampler.Push(input);
            }
            if (Time.unscaledTime >= nextResult && recognizer.TryRead(out var result))
            {
                nextResult = Time.unscaledTime + .3f;
                lastText = result.Text;
                FinalTranscript?.Invoke(result.Text);
                Debug.Log("ASR completed: audio=" + result.AudioSeconds.ToString("F2") +
                    "s decode=" + result.DecodeSeconds.ToString("F2") + "s"); // Do not log the user's speech.
            }
            string preview = lastText.Length > 18 ? lastText.Substring(0, 18) + "…" : lastText;
            Status = recognizer.State + (preview.Length > 0 ? " / " + preview : "");
            if (recognizer.DroppedWindows > 0) Status += "（処理待ち音声を一部破棄）";
        }

        void Fail(string message) { Stop(); Status = message; Debug.LogWarning(message); }

        public void Stop()
        {
            requested = false; requestId++;
            StopAllCoroutines();
            if (copying != null) { copying.Abort(); copying.Dispose(); copying = null; }
            if (clip != null) { Microphone.End(null); Destroy(clip); }
            clip = null; input = null; resampler = null;
            recognizer?.Reset();
            lastText = ""; Level = 0;
            Status = "音声入力 停止";
            Stopped?.Invoke();
        }

        void OnApplicationPause(bool pause) { if (pause) Stop(); }
        void OnDisable() { Stop(); }
        void OnDestroy() { recognizer?.Dispose(); }
    }
}
