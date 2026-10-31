using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koekatamarin
{
    public sealed class Experience : MonoBehaviour
    {
        public OVRCameraRig rig;
        public TMP_FontAsset font;
        public Material panelMaterial;
        public Material beamMaterial;
        public LegacyEnvironment environment;
        public bool IsMixedReality => mr;
        public bool IsSwitching => switching;
        readonly string[] samples = { "こんにちは！", "声が文字になる", "コエカタマリン", "おはよう", "ありがとう", "わあああ！" };
        readonly Color[] colors = { new Color(.25f, 1, .85f), new Color(1, .65f, .3f),
            new Color(.6f, .75f, 1), new Color(1, .55f, .75f), Color.white, new Color(1, .9f, .3f) };
        readonly WordProjectile[] pool = new WordProjectile[64];
        readonly Queue<string> voiceShots = new Queue<string>();
        readonly SharedSpace sharedSpace = new SharedSpace();
        LanPeer network;
        SpeechInput speech;
        OVRManager manager;
        OVRPassthroughLayer passthrough;
        OVRDisplay display;
        Transform panel;
        TextMeshPro status;
        TextMeshPro roomLabel;
        TextMeshPro modeLabel;
        TextMeshPro alignmentLabel;
        LineRenderer beam;
        Transform originMarker;
        LineRenderer directionMarker;
        bool mr = true;
        bool switching;
        int room = 1;
        int sampleIndex;
        int projectileIndex;
        float nextShot;
        float nextStatus;
        string feedback = "右トリガーで発射 / Aでパネル表示切替";
        Camera View => rig.centerEyeAnchor.GetComponent<Camera>();

        IEnumerator Start()
        {
            Application.targetFrameRate = 72;
            network = gameObject.AddComponent<LanPeer>();
            speech = gameObject.AddComponent<SpeechInput>();
            speech.FinalTranscript += SubmitFinalTranscript;
            speech.Stopped += ClearVoiceQueue;
            network.Received += Receive;
            manager = rig.GetComponent<OVRManager>();
            display = OVRManager.display;
            if (display != null) display.RecenteredPose += InvalidateAlignment;
            OVRManager.TrackingLost += InvalidateAlignment;
            passthrough = rig.GetComponent<OVRPassthroughLayer>();
            manager.isInsightPassthroughEnabled = true;
            passthrough.enabled = true;
            ConfigureCamera(true);
            if (environment != null) environment.Hide();
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = new GameObject("Word " + i).AddComponent<WordProjectile>();
                pool[i].transform.SetParent(transform);
                pool[i].Initialize(font, rig.centerEyeAnchor);
            }
            BuildPanel();
            beam = new GameObject("Pointer").AddComponent<LineRenderer>();
            beam.transform.SetParent(transform);
            beam.sharedMaterial = beamMaterial;
            beam.positionCount = 2;
            beam.startWidth = .003f;
            beam.endWidth = .001f;
            CreateAlignmentMarkers();
            yield return null;
            PlacePanel();
            // Passthrough initialization is asynchronous; suppression is requested only once it is active.
            float until = Time.realtimeSinceStartup + 10f;
            while (!OVRManager.IsInsightPassthroughInitialized() && Time.realtimeSinceStartup < until) yield return null;
            if (mr && OVRManager.IsInsightPassthroughInitialized()) manager.shouldBoundaryVisibilityBeSuppressed = true;
        }

        void ConfigureCamera(bool isMr)
        {
            foreach (var camera in rig.GetComponentsInChildren<Camera>())
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = isMr ? new Color(0, 0, 0, 0) : new Color(.055f, .08f, .16f, 1);
                camera.nearClipPlane = .05f;
                camera.farClipPlane = isMr ? 50f : 250f;
            }
        }

        TextMeshPro Label(Transform parent, string text, Vector3 position, float size, Vector2 bounds)
        {
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.font = font;
            label.fontSize = size;
            label.color = Color.white;
            label.richText = false;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = bounds;
            label.text = text;
            return label;
        }

        TextMeshPro Button(string text, Vector3 position, System.Action action)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = text;
            go.transform.SetParent(panel, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(.32f, .10f, .025f);
            go.GetComponent<Renderer>().sharedMaterial = panelMaterial;
            go.AddComponent<PanelAction>().Click = action;
            return Label(panel, text, position + new Vector3(0, 0, -.02f), .42f, new Vector2(.3f, .09f));
        }

        void BuildPanel()
        {
            panel = new GameObject("Control panel").transform;
            panel.SetParent(transform);
            Label(panel, "コエカタマリン 2026", new Vector3(0, .33f, 0), .65f, new Vector2(1.2f, .12f));
            Label(panel, "声を文字にして飛ばそう", new Vector3(0, .24f, 0), .32f, new Vector2(1.2f, .1f));
            roomLabel = Button("部屋 1 →", new Vector3(-.18f, .10f, 0), () =>
            {
                network.Disconnect(); InvalidateAlignment(); room = room % 9 + 1; roomLabel.text = "部屋 " + room + " →";
            });
            Button("接続 / 切断", new Vector3(.18f, .10f, 0), () =>
            { if (network.Connected) network.Disconnect(); else network.Connect(room); });
            Button("サンプル発射", new Vector3(-.18f, -.03f, 0), ShootSample);
            Button("音声 ON / OFF", new Vector3(.18f, -.03f, 0), () => speech.Toggle());
            modeLabel = Button("MR / VR", new Vector3(-.18f, -.16f, 0), ToggleMode);
            Button("次のことば", new Vector3(.18f, -.16f, 0), () => { sampleIndex = (sampleIndex + 1) % samples.Length; });
            alignmentLabel = Button("位置合わせ", new Vector3(-.18f, -.29f, 0), BeginAlignment);
            Button("位置を解除", new Vector3(.18f, -.29f, 0), InvalidateAlignment);
            status = Label(panel, "準備中", new Vector3(0, -.56f, -.02f), .28f, new Vector2(1.4f, .4f));
            status.textWrappingMode = TextWrappingModes.Normal;
        }

        void PlacePanel()
        {
            SetPanelVisible(true);
            var head = rig.centerEyeAnchor;
            var yaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
            panel.SetPositionAndRotation(head.position + yaw * new Vector3(0, -.05f, 1.35f), yaw);
        }

        public void SetPanelVisible(bool visible)
        {
            if (panel != null) panel.gameObject.SetActive(visible);
            if (beam != null) beam.enabled = visible;
        }

        public void ToggleMode() { if (!switching) StartCoroutine(SwitchMode()); }

        void CreateAlignmentMarkers()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Shared origin A";
            Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = beamMaterial;
            originMarker = marker.transform;
            originMarker.SetParent(transform);
            originMarker.localScale = Vector3.one * .04f;
            marker.SetActive(false);
            directionMarker = new GameObject("Shared forward A to B").AddComponent<LineRenderer>();
            directionMarker.transform.SetParent(transform);
            directionMarker.sharedMaterial = beamMaterial;
            directionMarker.positionCount = 2;
            directionMarker.startWidth = .01f; directionMarker.endWidth = .004f;
            directionMarker.enabled = false;
        }

        void BeginAlignment()
        {
            if (!mr || switching) { feedback = "位置合わせはMRモードで行います"; return; }
            InvalidateAlignment();
            sharedSpace.Begin();
            feedback = "全員共通の目印Aに右コントローラーを置き右グリップ";
        }

        void CaptureAlignment()
        {
            if (!sharedSpace.Capturing || !mr || switching) return;
            if (!OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch))
            { feedback = "右コントローラーの位置が追跡できません"; return; }
            sharedSpace.Capture(rig.rightHandAnchor.position, out feedback);
            network.SharedSpaceReady = sharedSpace.Ready;
        }

        void InvalidateAlignment()
        {
            sharedSpace.Clear();
            if (network != null) network.SharedSpaceReady = false;
            ClearVoiceQueue();
            foreach (var word in pool) if (word != null) word.Retire();
            if (originMarker != null) originMarker.gameObject.SetActive(false);
            if (directionMarker != null) directionMarker.enabled = false;
            feedback = "位置合わせを解除 / 未設定の相手は手前に表示";
        }

        void UpdateAlignmentMarkers()
        {
            bool visible = mr && (sharedSpace.Current == SharedSpace.Step.Direction || sharedSpace.Ready) &&
                (panel.gameObject.activeSelf || sharedSpace.Capturing);
            originMarker.gameObject.SetActive(visible);
            originMarker.position = sharedSpace.Origin;
            directionMarker.enabled = visible && sharedSpace.Ready;
            if (directionMarker.enabled)
            {
                directionMarker.SetPosition(0, sharedSpace.Origin);
                directionMarker.SetPosition(1, sharedSpace.Origin + sharedSpace.Rotation * Vector3.forward * .5f);
            }
        }

        IEnumerator SwitchMode()
        {
            switching = true;
            if (mr)
            {
                if (environment == null) { feedback = "夕日の風景が見つかりません"; switching = false; yield break; }
                manager.shouldBoundaryVisibilityBeSuppressed = false;
                // Explicitly ask the OS and require a positive state, not a default false field.
                float until = Time.realtimeSinceStartup + 5f;
                bool confirmed = false;
                do
                {
                    yield return null;
                    confirmed = OVRPlugin.GetBoundaryVisibility(out var state) == OVRPlugin.Result.Success &&
                        state == OVRPlugin.BoundaryVisibility.NotSuppressed;
                } while (!confirmed && Time.realtimeSinceStartup < until);
#if UNITY_EDITOR
                confirmed = true; // Desktop visual preview only, not a device safety test.
#endif
                if (confirmed)
                {
                    InvalidateAlignment();
                    mr = false;
                    passthrough.enabled = false;
                    manager.isInsightPassthroughEnabled = false;
                    ConfigureCamera(false);
                    environment.Show(rig.centerEyeAnchor);
                    feedback = "夕日のVR / 声を海へ飛ばそう";
                }
                else feedback = "境界復帰を確認できないためMRを継続";
            }
            else
            {
                manager.isInsightPassthroughEnabled = true;
                passthrough.enabled = true;
                ConfigureCamera(true);
                if (environment != null) environment.Hide();
                mr = true;
                float until = Time.realtimeSinceStartup + 10;
                while (!OVRManager.IsInsightPassthroughInitialized() && Time.realtimeSinceStartup < until) yield return null;
                manager.shouldBoundaryVisibilityBeSuppressed = OVRManager.IsInsightPassthroughInitialized();
                feedback = "MRモード";
            }
            switching = false;
        }

        void Update()
        {
            if (network == null || panel == null || beam == null) return;
            bool pressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            var ray = new Ray(rig.rightHandAnchor.position, rig.rightHandAnchor.forward);
            if (!OVRManager.isHmdPresent)
            {
                if (Mouse.current != null)
                {
                    ray = View.ScreenPointToRay(Mouse.current.position.ReadValue());
                    pressed |= Mouse.current.leftButton.wasPressedThisFrame;
                }
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.spaceKey.wasPressedThisFrame) ShootSample();
                    if (Keyboard.current.nKey.wasPressedThisFrame) { if (network.Connected) network.Disconnect(); else network.Connect(room); }
                    if (Keyboard.current.rKey.wasPressedThisFrame) PlacePanel();
                    if (Keyboard.current.tabKey.wasPressedThisFrame) SetPanelVisible(!panel.gameObject.activeSelf);
                }
            }
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch)) PlacePanel();
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)) SetPanelVisible(!panel.gameObject.activeSelf);
            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch)) CaptureAlignment();
            UpdateAlignmentMarkers();
            bool hit = Physics.Raycast(ray, out var result, 5);
            beam.SetPosition(0, ray.origin);
            beam.SetPosition(1, hit ? result.point : ray.GetPoint(3));
            if (pressed)
            {
                if (hit && result.collider.TryGetComponent<PanelAction>(out var action)) action.Invoke();
                else ShootSample();
            }
            if (voiceShots.Count > 0 && Time.unscaledTime >= nextShot) Emit(voiceShots.Dequeue());
            if (Time.unscaledTime >= nextStatus)
            {
                nextStatus = Time.unscaledTime + .2f;
                status.text = samples[sampleIndex] + "\n" +
                    (network.Connected ? "部屋 " + room + " / 相手 " + network.PeerCount + "台 / 未達 " + network.MissedDeliveries : network.Status) +
                    "\n" + speech.Status + "  " + speech.Level.ToString("F3") +
                    "\n" + (sharedSpace.Ready ? "位置合わせ済 / 相手 " + network.AlignedPeerCount + "台" : "相手の文字は手前に表示") + "\n" + feedback;
                modeLabel.text = switching ? "切替中" : (mr ? "MR → VR" : "VR → MR");
                alignmentLabel.text = sharedSpace.Capturing ? "やり直す" : sharedSpace.Ready ? "位置合わせ済" : "位置合わせ";
            }
        }

        public void SubmitFinalTranscript(string text)
        {
            text = LanProtocol.Clean(text);
            if (text.Length == 0) return;
            if (voiceShots.Count == 8) { voiceShots.Dequeue(); feedback = "処理待ちが多いため古い発話を省略しました"; }
            voiceShots.Enqueue(text);
        }
        void ClearVoiceQueue() { voiceShots.Clear(); }
        void Emit(string text)
        {
            nextShot = Time.unscaledTime + .3f;
            int style = sampleIndex % colors.Length;
            Pose pose = PersonalPose(false);
            Spawn(text, style, pose);
            if (!network.Connected) return;
            bool aligned = mr && sharedSpace.Ready;
            var shared = aligned ? sharedSpace.ToShared(pose) : new Pose(Vector3.zero, Quaternion.identity);
            if (!network.SendShot(text, style, aligned ? 1 : 0, shared.position, Mathf.DeltaAngle(0, shared.rotation.eulerAngles.y)) && network.PeerCount > 0)
                feedback = "文字をLANへ送信できませんでした";
        }
        void ShootSample()
        { if (voiceShots.Count == 0 && Time.unscaledTime >= nextShot) Emit(samples[sampleIndex]); }
        void Receive(LanPacket packet)
        {
            Pose pose = mr && sharedSpace.Ready && packet.space == 1
                ? sharedSpace.ToWorld(packet.position, packet.yaw) : PersonalPose(true);
            Spawn(packet.text, packet.style, pose);
        }
        Pose PersonalPose(bool remote)
        {
            var head = rig.centerEyeAnchor;
            var yaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
            Vector3 offset = new Vector3(remote ? -.45f : .45f, -.1f, .6f);
            return new Pose(head.position + yaw * offset, yaw);
        }
        void Spawn(string text, int style, Pose pose)
        {
            var head = rig.centerEyeAnchor;
            var projectile = pool[projectileIndex++ % pool.Length];
            projectile.Launch(text, pose, colors[style],
                !mr, environment != null ? environment.FloorY : head.position.y - 1.65f);
        }
        void OnApplicationPause(bool paused)
        {
            if (paused) InvalidateAlignment();
            if (!paused && manager != null && mr)
            {
                manager.isInsightPassthroughEnabled = true;
                passthrough.enabled = true;
                // A resume requires a fresh passthrough state check.
                manager.shouldBoundaryVisibilityBeSuppressed = OVRManager.IsInsightPassthroughInitialized();
            }
        }
        void OnDestroy()
        {
            if (speech != null) { speech.FinalTranscript -= SubmitFinalTranscript; speech.Stopped -= ClearVoiceQueue; }
            if (display != null) display.RecenteredPose -= InvalidateAlignment;
            OVRManager.TrackingLost -= InvalidateAlignment;
            if (network != null) network.Received -= Receive;
            if (manager != null) manager.shouldBoundaryVisibilityBeSuppressed = false;
        }
    }
}
