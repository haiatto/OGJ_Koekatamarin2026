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
            for (int i = 0; i < SpeechModels.Files.Length; i++)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Speech", SpeechModels.Files[i]);
                if (!SpeechModels.Valid(path, SpeechModels.Hashes[i]))
                    throw new BuildFailedException("Offline speech assets missing or corrupt. Run python tools/setup_speech.py: " + path);
            }
            foreach (string library in new[] { "libonnxruntime.so", "libsherpa-onnx-c-api.so" })
                if (!File.Exists(Path.Combine(Application.dataPath, "Plugins/Android/Speech/arm64-v8a", library)))
                    throw new BuildFailedException("Run python tools/setup_speech.py; missing " + library);
        }
    }
}
