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
        public VoiceFontChoice[] voiceFonts;
        Font voiceFont;
        int voiceFontIndex;
        const string VoiceFontPreference = "Koekatamarin.VoiceFont";
        const string VoiceSizePreference = "Koekatamarin.VoiceSize";
        static readonly float[] voiceSizes = { .25f, .5f, .75f, 1f, 1.5f, 2f };
        int voiceSizeIndex = 3;
        public float VoiceSize => voiceSizes[voiceSizeIndex];
        public string VoiceFontName => voiceFonts[voiceFontIndex].title;
        public Material voiceMaterial;
        Material runtimeVoiceMaterial;
        public Material panelMaterial;
        public Material beamMaterial;
        public LegacyEnvironment environment;
        public HandControls hands;
        public RoomCollision roomCollision;
        readonly PlayerMotion motion = new PlayerMotion();
        public bool IsMixedReality => mr;
        public bool IsSwitching => switching;
        readonly string[] samples = { "こんにちは！", "声が文字になる", "コエカタマリン", "おはよう", "ありがとう", "わあああ！" };
        readonly WordProjectile[] pool = new WordProjectile[64];
        readonly Queue<string> voiceShots = new Queue<string>();
        readonly SharedSpace sharedSpace = new SharedSpace();
        readonly MrBoundaryGate boundaryGate = new MrBoundaryGate();
        LanPeer network;
        SpeechInput speech;
        OVRManager manager;
        OVRPassthroughLayer passthrough;
        OVRDisplay display;
        Transform panel;
        Transform mainControls;
        Transform addressControls;
        TextMeshPro addressEntry;
        TextMeshPro addressLocal;
        TextMeshPro addressStatus;
        string enteredAddress = "";
        TextMeshPro status;
        TextMeshPro roomLabel;
        TextMeshPro modeLabel;
        TextMeshPro alignmentLabel;
        TextMeshPro modelLabel;
        TextMeshPro voiceFontLabel;
        TextMeshPro voiceSizeLabel;
        TextMeshPro roomWireLabel;
        LineRenderer beam;
        Transform originMarker;
        LineRenderer directionMarker;
        bool mr = true;
        bool switching;
        bool applicationPaused;
        Coroutine modeTransition;
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
            // Words collide with each other and scanned geometry, never the UI/controller layer.
            Physics.IgnoreLayerCollision(8, 0, true);
            network = gameObject.AddComponent<LanPeer>();
            speech = gameObject.AddComponent<SpeechInput>();
            speech.TranscriptChanged += SubmitVoiceDelta;
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
            runtimeVoiceMaterial = new Material(voiceMaterial);
            Font.textureRebuilt += RefreshVoiceAtlas;
            string savedFont = PlayerPrefs.GetString(VoiceFontPreference, "mikachan-pb");
            int savedIndex = System.Array.FindIndex(voiceFonts, choice => choice.id == savedFont);
            SelectVoiceFont(Mathf.Max(0, savedIndex), false);
            int sizeIndex = System.Array.IndexOf(voiceSizes, PlayerPrefs.GetFloat(VoiceSizePreference, 1f));
            voiceSizeIndex = sizeIndex >= 0 ? sizeIndex : 3;
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = new GameObject("Word " + i).AddComponent<WordProjectile>();
                pool[i].transform.SetParent(transform);
                pool[i].Initialize(voiceFont, runtimeVoiceMaterial);
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
            BeginMrBoundaryRequest();
#if !UNITY_EDITOR
            speech.StartListening();
            if (roomCollision != null) StartCoroutine(roomCollision.LoadAfterMicrophone(speech));
#endif
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

        TextMeshPro Button(string text, Vector3 position, System.Action action, Transform parent = null, float width = .32f)
        {
            if (parent == null) parent = mainControls;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = text;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(width, .10f, .025f);
            go.GetComponent<Renderer>().sharedMaterial = panelMaterial;
            go.AddComponent<PanelAction>().Click = action;
            return Label(parent, text, position + new Vector3(0, 0, -.02f), .42f, new Vector2(width - .02f, .09f));
        }

        void BuildPanel()
        {
            panel = new GameObject("Control panel").transform;
            panel.SetParent(rig.transform);
            mainControls = new GameObject("Main controls").transform;
            mainControls.SetParent(panel, false);
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
            Button("IPで接続", new Vector3(-.18f, -.42f, 0), ShowAddressPanel);
            modelLabel = Button("ReazonSpeech", new Vector3(.18f, -.42f, 0), () => speech.NextModel());
            Button("目印を確定", new Vector3(-.18f, -.55f, 0), CaptureAlignment);
            Button("部屋読込", new Vector3(.18f, -.55f, 0), () => roomCollision?.RequestLoad());
            Button("左へ30度", new Vector3(-.18f, -.68f, 0), () => TurnFromPanel(-1));
            Button("右へ30度", new Vector3(.18f, -.68f, 0), () => TurnFromPanel(1));
            voiceFontLabel = Button("書体：" + VoiceFontName + " →", new Vector3(0, -.81f, 0), NextVoiceFont, width: .68f);
            roomWireLabel = Button("空間線 OFF", new Vector3(-.18f, -.94f, 0), ToggleRoomWire);
            voiceSizeLabel = Button(SizeLabel(), new Vector3(.18f, -.94f, 0), NextVoiceSize);
            status = Label(mainControls, "準備中", new Vector3(0, -1.24f, -.02f), .26f, new Vector2(1.4f, .45f));
            status.textWrappingMode = TextWrappingModes.Normal;
            BuildAddressPanel();
        }

        void BuildAddressPanel()
        {
            addressControls = new GameObject("IP address controls").transform;
            addressControls.SetParent(panel, false);
            addressLocal = Label(addressControls, "", new Vector3(0, .10f, -.02f), .28f, new Vector2(1.4f, .10f));
            addressEntry = Label(addressControls, "相手のIPv4を入力", new Vector3(0, -.01f, -.02f), .48f, new Vector2(1.1f, .10f));
            addressEntry.name = "Entered IPv4";
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", "消す" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                Button(key, new Vector3((i % 3 - 1) * .24f, -.14f - (i / 3) * .12f, 0),
                    () => EditAddress(key), addressControls, .20f);
            }
            Button("接続先に追加", new Vector3(-.18f, -.64f, 0), AddAddress, addressControls);
            Button("戻る", new Vector3(.18f, -.64f, 0), () =>
            { addressControls.gameObject.SetActive(false); mainControls.gameObject.SetActive(true); }, addressControls);
            addressStatus = Label(addressControls, "", new Vector3(0, -.80f, -.02f), .28f, new Vector2(1.5f, .2f));
            addressControls.gameObject.SetActive(false);
        }

        public void ShowAddressPanel()
        { ShowAddressPanel(LanAddress.LocalAddresses()); }

        public void ShowAddressPanel(string localAddresses)
        {
            addressLocal.text = "部屋 " + room + " / 自分のIP: " + localAddresses;
            mainControls.gameObject.SetActive(false);
            addressControls.gameObject.SetActive(true);
        }

        void EditAddress(string key)
        {
            if (key == "消す") { if (enteredAddress.Length > 0) enteredAddress = enteredAddress.Substring(0, enteredAddress.Length - 1); }
            else if (enteredAddress.Length < 15) enteredAddress += key;
            addressEntry.text = enteredAddress.Length > 0 ? enteredAddress : "相手のIPv4を入力";
        }

        void AddAddress()
        {
            if (!network.Connected) network.Connect(room);
            network.AddManualAddress(enteredAddress);
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

        public void ToggleMode() { if (!switching && !applicationPaused) modeTransition = StartCoroutine(SwitchMode()); }

        void BeginMrBoundaryRequest()
        {
            if (manager == null || !mr || switching || applicationPaused) return;
            manager.isInsightPassthroughEnabled = true;
            passthrough.enabled = true;
            manager.shouldBoundaryVisibilityBeSuppressed = false;
            boundaryGate.Begin(Time.realtimeSinceStartup);
        }

        string BoundaryStatus()
        {
            if (boundaryGate.Current == MrBoundaryGate.State.Waiting) return "MR準備中";
            if (boundaryGate.Current == MrBoundaryGate.State.TimedOut) return "MR初期化待ち時間超過";
#if UNITY_EDITOR
            return "境界: 実機で確認";
#else
            if (OVRPlugin.GetBoundaryVisibility(out var state) != OVRPlugin.Result.Success) return "境界: 未確認";
            return state == OVRPlugin.BoundaryVisibility.Suppressed ? "境界: 抑制中" : "境界: 通常";
#endif
        }

        void CreateAlignmentMarkers()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Shared origin A";
            Destroy(marker.GetComponent<SphereCollider>());
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
            feedback = "共通の目印Aに右手を置き「目印を確定」/ 右グリップ";
        }

        void CaptureAlignment()
        {
            if (!sharedSpace.Capturing || !mr || switching) return;
            bool trackedHand = hands != null && hands.right != null && hands.right.IsDataHighConfidence;
            if (!trackedHand && !OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch))
            { feedback = "右コントローラーの位置が追跡できません"; return; }
            sharedSpace.Capture(trackedHand ? hands.right.transform.position : rig.rightHandAnchor.position, out feedback);
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
            boundaryGate.Cancel();
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
                    motion.Enter(rig.transform);
                    roomCollision?.SetMixedReality(false);
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
                motion.Exit(rig.transform);
                InvalidateAlignment();
                roomCollision?.SetMixedReality(true);
                manager.isInsightPassthroughEnabled = true;
                passthrough.enabled = true;
                ConfigureCamera(true);
                if (environment != null) environment.Hide();
                mr = true;
                feedback = "MRモード";
            }
            switching = false;
            modeTransition = null;
            if (mr) BeginMrBoundaryRequest();
            PlacePanel();
        }

        void TurnFromPanel(float direction)
        {
            if (mr || switching || applicationPaused) return;
            rig.transform.RotateAround(rig.centerEyeAnchor.position, Vector3.up, direction * 30);
        }

        void Update()
        {
            if (network == null || panel == null || beam == null) return;
            if (mr && !switching && !applicationPaused && boundaryGate.Tick(Time.realtimeSinceStartup, OVRManager.IsInsightPassthroughInitialized()))
                manager.shouldBoundaryVisibilityBeSuppressed = true;
            bool pressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            var ray = new Ray(rig.rightHandAnchor.position, rig.rightHandAnchor.forward);
            bool allowedMotion = !mr && !switching && !applicationPaused && OVRManager.hasInputFocus;
            if (hands != null)
            {
                hands.Sample(allowedMotion);
                if (hands.HasPointer) { ray = hands.Ray; pressed = hands.Click; }
                if (hands.Recall) PlacePanel();
            }
            Vector2 move = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            if (hands != null && hands.Move.sqrMagnitude > 0) move = hands.Move;
            motion.Step(rig.transform, rig.centerEyeAnchor, move,
                OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x,
                Time.unscaledDeltaTime, allowedMotion);
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
            bool hit = Physics.Raycast(ray, out var result, 5, ~((1 << 8) | (1 << 9)));
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
                    "\n" + speech.ModelName + ": " + speech.Status + "  " + speech.Level.ToString("F3") +
                    "\n" + (sharedSpace.Ready ? "位置合わせ済 / 相手 " + network.AlignedPeerCount + "台" : "相手の文字は手前に表示") +
                    "\n" + BoundaryStatus() + " / " + (roomCollision != null ? roomCollision.Status : "") + "\n" + feedback;
                modelLabel.text = speech.ModelName + " →";
                addressStatus.text = network.Status + "\n相手 " + network.PeerCount + "台 / 戻ると通常の操作画面";
                modeLabel.text = switching ? "切替中" : (mr ? "MR → VR" : "VR → MR");
                alignmentLabel.text = sharedSpace.Capturing ? "やり直す" : sharedSpace.Ready ? "位置合わせ済" : "位置合わせ";
                if (roomCollision != null)
                {
                    roomWireLabel.text = !roomCollision.WireVisible ? "空間線 OFF" :
                        !mr ? "空間線 MRのみ" : roomCollision.WireReady ? "空間線 ON" : "空間線 待ち";
                    if (roomCollision.WireVisible) status.text += "\n" + roomCollision.WireStatus;
                }
            }
        }

        public void SubmitVoiceDelta(string text)
        {
            while (!string.IsNullOrWhiteSpace(text))
            {
                string chunk = LanProtocol.Clean(text);
                if (chunk.Length == 0) break;
                if (voiceShots.Count == 8) { voiceShots.Dequeue(); feedback = "処理待ちが多いため古い発話を省略しました"; }
                voiceShots.Enqueue(chunk);
                text = text.Trim().Substring(chunk.Length);
            }
        }
        void ClearVoiceQueue() { voiceShots.Clear(); }
        void Emit(string text)
        {
            nextShot = Time.unscaledTime + .3f;
            int style = network.Connected ? (Random.Range(0, 3) == 0 ? 2 : 1) : 0;
            Pose pose = PersonalPose(false);
            Spawn(text, style, pose);
            if (!network.Connected) return;
            bool aligned = mr && sharedSpace.Ready;
            var shared = aligned ? sharedSpace.ToShared(pose) : new Pose(Vector3.zero, Quaternion.identity);
            if (!network.SendShot(text, style, aligned ? 1 : 0, shared.position, Mathf.DeltaAngle(0, shared.rotation.eulerAngles.y)) && network.PeerCount > 0)
                feedback = "文字をLANへ送信できませんでした";
        }
        void ShootSample()
        { if (!addressControls.gameObject.activeSelf && voiceShots.Count == 0 && Time.unscaledTime >= nextShot) Emit(samples[sampleIndex]); }
        void Receive(LanPacket packet)
        {
            Pose pose = mr && sharedSpace.Ready && packet.space == 1
                ? sharedSpace.ToWorld(packet.position, packet.yaw) : PersonalPose(true);
            Spawn(packet.text, packet.style, pose);
        }
        Pose PersonalPose(bool remote)
        {
            var head = rig.centerEyeAnchor;
            if (!remote) return PlayerMotion.VoicePose(head.position, head.rotation);
            var yaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
            Vector3 offset = new Vector3(remote ? -.45f : .45f, -.1f, .6f);
            return new Pose(head.position + yaw * offset, yaw);
        }
        void Spawn(string text, int style, Pose pose)
        {
            var head = rig.centerEyeAnchor;
            var projectile = pool[projectileIndex++ % pool.Length];
            projectile.SetSize(VoiceSize);
            projectile.Launch(text, pose, style);
        }
        void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
            {
                InvalidateAlignment();
                boundaryGate.Cancel();
                if (modeTransition != null) { StopCoroutine(modeTransition); modeTransition = null; }
                switching = false;
                if (manager != null) manager.shouldBoundaryVisibilityBeSuppressed = false;
            }
            else BeginMrBoundaryRequest();
        }
        public void NextVoiceFont()
        { SelectVoiceFont((voiceFontIndex + 1) % voiceFonts.Length, true); }

        string SizeLabel() => "文字 " + (VoiceSize * 100).ToString("0") + "% →";
        public void NextVoiceSize()
        {
            voiceSizeIndex = (voiceSizeIndex + 1) % voiceSizes.Length;
            if (voiceSizeLabel != null) voiceSizeLabel.text = SizeLabel();
            PlayerPrefs.SetFloat(VoiceSizePreference, VoiceSize);
            PlayerPrefs.Save();
            feedback = "次に飛ぶ文字から適用（この端末の表示・当たり判定）";
        }
        public void ToggleRoomWire()
        {
            if (roomCollision == null) { feedback = "空間線はQuest実機で部屋読込後に表示"; return; }
            roomCollision.SetWireVisible(!roomCollision.WireVisible);
            roomWireLabel.text = roomCollision.WireVisible ? "空間線 ON" : "空間線 OFF";
            feedback = roomCollision.WireVisible ? "水色の線が部屋の当たり判定（MRのみ・部屋読込が必要）" : "空間線を非表示";
        }

        void SelectVoiceFont(int index, bool save)
        {
            voiceFontIndex = index;
            voiceFont = voiceFonts[index].font;
            foreach (var word in pool)
                if (word != null) word.SetFont(voiceFont, runtimeVoiceMaterial);
            RefreshVoiceAtlas(voiceFont);
            if (voiceFontLabel != null) voiceFontLabel.text = "書体：" + VoiceFontName + " →";
            if (save)
            {
                PlayerPrefs.SetString(VoiceFontPreference, voiceFonts[index].id);
                PlayerPrefs.Save();
            }
        }

        void RefreshVoiceAtlas(Font changed)
        {
            if (changed == voiceFont && runtimeVoiceMaterial != null)
                runtimeVoiceMaterial.mainTexture = voiceFont.material.mainTexture;
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshVoiceAtlas;
            if (runtimeVoiceMaterial != null) Destroy(runtimeVoiceMaterial);
            if (speech != null) { speech.TranscriptChanged -= SubmitVoiceDelta; speech.Stopped -= ClearVoiceQueue; }
            if (display != null) display.RecenteredPose -= InvalidateAlignment;
            OVRManager.TrackingLost -= InvalidateAlignment;
            if (network != null) network.Received -= Receive;
            if (manager != null) manager.shouldBoundaryVisibilityBeSuppressed = false;
        }
    }
}
