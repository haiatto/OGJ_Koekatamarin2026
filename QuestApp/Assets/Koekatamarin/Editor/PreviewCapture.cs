using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koekatamarin.Editor
{
    [InitializeOnLoad]
    public static class PreviewCapture
    {
        static double started;
        static bool fired;
        static PreviewCapture()
        {
            if (!SessionState.GetBool("Koekatamarin.Preview", false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        public static void Begin()
        {
            EditorSceneManager.OpenScene("Assets/Koekatamarin/Generated/Playground.unity");
            SessionState.SetBool("Koekatamarin.Preview", true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 60) { SessionState.SetBool("Koekatamarin.Preview", false); EditorApplication.Exit(1); return; }
            if (!EditorApplication.isPlaying) return;
            var experience = Object.FindAnyObjectByType<Experience>();
            if (experience == null || Time.time < 3) return;
            if (!fired)
            {
                experience.SubmitFinalTranscript("声が文字になる！");
                fired = true;
            }
            if (Time.time < 3.3f) return;
            var camera = experience.rig.centerEyeAnchor.GetComponent<Camera>();
            var render = new RenderTexture(1440, 1000, 24);
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            var image = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0);
            image.Apply();
            Directory.CreateDirectory("../.work");
            File.WriteAllBytes("../.work/quest-preview.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(render);
            EditorApplication.update -= Tick;
            SessionState.SetBool("Koekatamarin.Preview", false);
            EditorApplication.Exit(0);
        }
    }
}
