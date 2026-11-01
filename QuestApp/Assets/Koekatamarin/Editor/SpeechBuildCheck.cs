using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Koekatamarin.Editor
{
    public sealed class SpeechBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (var model in new[] { SpeechModels.Model.SenseVoice, SpeechModels.Model.ReazonSpeech })
            {
                var files = SpeechModels.ModelFiles(model);
                var hashes = SpeechModels.ModelHashes(model);
                for (int i = 0; i < files.Length; i++)
                {
                    string path = Path.Combine(Application.streamingAssetsPath, "Speech", files[i]);
                    if (!SpeechModels.Valid(path, hashes[i]))
                        throw new BuildFailedException("Offline speech assets missing or corrupt. Run python tools/setup_speech.py: " + path);
                }
            }
            if (File.Exists(Path.Combine(Application.streamingAssetsPath, "Speech/silero_vad.onnx")))
                throw new BuildFailedException("Remove the obsolete Silero VAD model before building.");
            foreach (string library in new[] { "libonnxruntime.so", "libsherpa-onnx-c-api.so" })
                if (!File.Exists(Path.Combine(Application.dataPath, "Plugins/Android/Speech/arm64-v8a", library)))
                    throw new BuildFailedException("Run python tools/setup_speech.py; missing " + library);
        }
    }
}
