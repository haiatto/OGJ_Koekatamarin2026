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
            // Desktop previews have no XR runtime. Remove device-only native components from
            // this unsaved preview scene before Play; the saved Android scene stays intact.
            var experience = Object.FindAnyObjectByType<Experience>();
            if (experience.roomCollision != null) Object.DestroyImmediate(experience.roomCollision.gameObject);
            if (experience.hands != null)
            {
                Object.DestroyImmediate(experience.hands.left.gameObject);
                Object.DestroyImmediate(experience.hands.right.gameObject);
                Object.DestroyImmediate(experience.hands);
            }
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

        public static void BeginIP()
        {
            SessionState.SetBool("Koekatamarin.PreviewIP", true);
            Begin();
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 60) { SessionState.SetBool("Koekatamarin.Preview", false); EditorApplication.Exit(1); return; }
            if (!EditorApplication.isPlaying) return;
            var experience = Object.FindAnyObjectByType<Experience>();
            if (experience == null || Time.time < 3) return;
            bool vr = SessionState.GetBool("Koekatamarin.PreviewVR", false);
            bool ip = SessionState.GetBool("Koekatamarin.PreviewIP", false);
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
                if (ip)
                {
                    // A documentation preview must not publish the workstation's real interface addresses.
                    experience.ShowAddressPanel("192.168.1.10（表示例）");
                    var controls = experience.rig.transform.Find("Control panel/IP address controls");
                    foreach (char digit in "192.168.1.20") controls.Find(digit.ToString()).GetComponent<PanelAction>().Invoke();
                    controls.Find("消す").GetComponent<PanelAction>().Invoke();
                    controls.Find("0").GetComponent<PanelAction>().Invoke();
                    if (controls.Find("Entered IPv4").GetComponent<TMPro.TextMeshPro>().text != "192.168.1.20")
                        throw new System.InvalidOperationException("IP keypad entry/backspace failed");
                }
                else experience.SubmitVoiceDelta("声が文字になる！");
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
            File.WriteAllBytes(ip ? "../.work/quest-preview-ip.png" : vr ? "../.work/quest-preview-vr.png" : "../.work/quest-preview.png", image.EncodeToPNG());
            if (vr)
            {
                experience.SetPanelVisible(false);
                camera.Render();
                image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0);
                image.Apply();
                File.WriteAllBytes("../.work/quest-preview-vr-scenery.png", image.EncodeToPNG());
                string initialFont = experience.VoiceFontName;
                for (int index = 0; index < experience.voiceFonts.Length; index++)
                {
                    camera.Render();
                    RenderTexture.active = render;
                    image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0);
                    image.Apply();
                    File.WriteAllBytes("../.work/quest-preview-font-" + index + ".png", image.EncodeToPNG());
                    experience.NextVoiceFont();
                }
                if (experience.VoiceFontName != initialFont)
                    throw new System.InvalidOperationException("Font cycle did not return to original selection");
                Debug.Log("KOEKATAMARIN_FONT_CYCLE_OK count=" + experience.voiceFonts.Length);
                var reflection = experience.environment.GetComponentInChildren<Water4Reflection>();
                if (reflection == null || reflection.RenderCount == 0 || reflection.LeftTexture == null || !reflection.LeftTexture.IsCreated())
                    throw new System.InvalidOperationException("Water4 planar reflection did not render");
                var target = reflection.LeftTexture;
                RenderTexture.active = target;
                var reflectionImage = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                reflectionImage.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                reflectionImage.Apply();
                File.WriteAllBytes("../.work/quest-preview-water-reflection.png", reflectionImage.EncodeToPNG());
                Object.DestroyImmediate(reflectionImage);
                Debug.Log("KOEKATAMARIN_WATER4_REFLECTION_OK mono renders=" + reflection.RenderCount);
            }
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(render);
            if (ip)
            {
                var controls = experience.rig.transform.Find("Control panel/IP address controls");
                controls.Find("戻る").GetComponent<PanelAction>().Invoke();
                if (controls.gameObject.activeSelf || !experience.rig.transform.Find("Control panel/Main controls").gameObject.activeSelf)
                    throw new System.InvalidOperationException("IP keypad did not return to normal controls");
                Debug.Log("KOEKATAMARIN_IP_KEYPAD_OK (Editor input; no device network validation)");
            }
            if (vr) { returningToMr = true; experience.ToggleMode(); return; }
            Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool("Koekatamarin.Preview", false);
            SessionState.SetBool("Koekatamarin.PreviewVR", false);
            SessionState.SetBool("Koekatamarin.PreviewIP", false);
            EditorApplication.Exit(0);
        }
    }
}
