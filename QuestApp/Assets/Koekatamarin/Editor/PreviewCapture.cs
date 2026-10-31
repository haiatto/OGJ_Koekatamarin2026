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
        static bool requestedVr;
        static float firedAt;
        static bool returningToMr;
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

        public static void BeginVR()
        {
            SessionState.SetBool("Koekatamarin.PreviewVR", true);
            Begin();
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 60) { SessionState.SetBool("Koekatamarin.Preview", false); EditorApplication.Exit(1); return; }
            if (!EditorApplication.isPlaying) return;
            var experience = Object.FindAnyObjectByType<Experience>();
            if (experience == null || Time.time < 3) return;
            bool vr = SessionState.GetBool("Koekatamarin.PreviewVR", false);
            if (returningToMr)
            {
                if (experience.IsSwitching) return;
                if (!experience.IsMixedReality || experience.environment.Visible ||
                    experience.rig.centerEyeAnchor.GetComponent<Camera>().backgroundColor.a != 0)
                    throw new System.InvalidOperationException("VR scenery did not clear on returning to MR");
                Debug.Log("KOEKATAMARIN_VR_ROUNDTRIP_OK (Editor simulation; no device boundary validation)");
                Finish();
                return;
            }
            if (vr && !requestedVr) { experience.ToggleMode(); requestedVr = true; }
            if (vr && (experience.IsMixedReality || experience.IsSwitching)) return;
            if (!fired)
            {
                experience.SubmitFinalTranscript("声が文字になる！");
                fired = true;
                firedAt = Time.time;
            }
            if (Time.time < firedAt + .3f) return;
            var camera = experience.rig.centerEyeAnchor.GetComponent<Camera>();
            var render = new RenderTexture(1440, 1000, 24);
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            var image = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0);
            image.Apply();
            Directory.CreateDirectory("../.work");
            File.WriteAllBytes(vr ? "../.work/quest-preview-vr.png" : "../.work/quest-preview.png", image.EncodeToPNG());
            if (vr)
            {
                experience.SetPanelVisible(false);
                camera.Render();
                image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0);
                image.Apply();
                File.WriteAllBytes("../.work/quest-preview-vr-scenery.png", image.EncodeToPNG());
            }
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(render);
            if (vr) { returningToMr = true; experience.ToggleMode(); return; }
            Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool("Koekatamarin.Preview", false);
            SessionState.SetBool("Koekatamarin.PreviewVR", false);
            EditorApplication.Exit(0);
        }
    }
}
