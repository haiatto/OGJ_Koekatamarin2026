using System.Collections;
using UnityEngine;
using Uralstech.UXR.QuestMeshing;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Koekatamarin
{
    // Owns the live depth pipeline. No saved Scene Model or Space Setup is loaded.
    public sealed class RoomCollision : MonoBehaviour
    {
        public GameObject liveRoot;
        public DepthPreprocessor depth;
        public DepthMesher mesher;
        public MeshCollider surface;
        bool requested, permissionPending, paused, mixedReality = true;
        CollisionWireframe wireframe;
        Collider[] surfaces;
        int updates;
        float lastMeshTime, nextDiagnostic;
        string permissionStatus = "";
        public bool WireVisible { get; private set; }
        public bool WireReady => wireframe != null && wireframe.EdgeCount > 0;
        public string WireStatus => !WireVisible ? "空間線: OFF" : !mixedReality ? "空間線: MRのみ" :
            WireReady ? "空間線: ライブ / 線" + wireframe.EdgeCount : "空間線: 周囲をゆっくり見回してください";
        public string Status
        {
            get
            {
                if (!mixedReality) return "空間: VR中は停止";
                if (paused) return "空間: 一時停止";
                if (permissionStatus.Length > 0) return permissionStatus;
                if (!requested) return "空間: ライブ準備中";
                if (liveRoot == null || !liveRoot.activeSelf) return "空間: 再取得で開始";
                if (!depth.IsSupported) return "空間: 深度を利用できません";
                if (!depth.IsDataAvailable) return "空間: 深度待ち / 周囲を見回してください";
                if (surface.sharedMesh == null) return "空間: 形状取得中 / 周囲を見回してください";
                return "空間: ライブ " + (surface.sharedMesh.GetIndexCount(0) / 3) + "面 / 更新" + updates +
                    (Time.unscaledTime - lastMeshTime > 4 ? "（更新待ち）" : "");
            }
        }
        void Awake()
        {
            surfaces = new Collider[] { surface };
            mesher.OnMeshRefreshed += MeshRefreshed;
            mesher.OnCleared += Cleared;
            OVRManager.TrackingLost += TrackingLost;
        }
        public IEnumerator LoadAfterMicrophone(SpeechInput speech)
        {
            yield return null;
            while (!OVRManager.hasInputFocus || (speech.Running && !speech.IsCapturing)) yield return null;
            RequestLoad();
        }
        // Kept as the panel entry point; now clears and reacquires live geometry.
        public void RequestLoad()
        {
            if (!mixedReality || permissionPending) return;
            requested = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                permissionPending = true;
                permissionStatus = "空間: 空間データの許可待ち";
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ =>
                {
                    if (this == null) return;
                    permissionPending = false; permissionStatus = "";
                    UpdateActivity();
                };
                callbacks.PermissionDenied += _ =>
                {
                    if (this == null) return;
                    permissionPending = false;
                    permissionStatus = "空間: 未許可 / 空間再取得で再試行";
                };
                Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
                return;
            }
            permissionStatus = "";
            if (mesher.IsInitialized) mesher.Clear();
            UpdateActivity();
#else
            permissionStatus = "空間: Quest実機でライブ取得";
#endif
        }
        public void SetMixedReality(bool value)
        {
            if (mixedReality == value) return;
            mixedReality = value;
            UpdateActivity();
        }
        void OnApplicationPause(bool value) { paused = value; UpdateActivity(); }
        void UpdateActivity()
        {
            if (liveRoot == null) return;
            bool active = requested && mixedReality && !paused && !permissionPending && permissionStatus.Length == 0;
            if (!active && liveRoot.activeSelf && mesher.IsInitialized) mesher.Clear();
            surface.enabled = active;
            liveRoot.SetActive(active);
            RefreshWireframe();
        }
        void TrackingLost() { if (mesher != null && mesher.IsInitialized) mesher.Clear(); }
        void Cleared()
        {
            updates = 0;
            if (wireframe != null) wireframe.Clear();
            Debug.Log("LIVE_ROOM_CLEARED");
        }
        void MeshRefreshed()
        {
            if (!mixedReality || paused) return;
            updates++;
            lastMeshTime = Time.unscaledTime;
            // UXR reuses its Mesh; reference equality cannot detect its changes.
            if (wireframe != null) wireframe.Invalidate(surface);
            RefreshWireframe();
            if (Time.unscaledTime >= nextDiagnostic)
            {
                nextDiagnostic = Time.unscaledTime + 10;
                Debug.Log("LIVE_ROOM_UPDATE " + Status);
            }
        }
        public void SetWireVisible(bool value)
        {
            WireVisible = value;
            RefreshWireframe();
            if (value && !requested) RequestLoad();
        }
        void RefreshWireframe()
        {
            bool show = WireVisible && mixedReality && !paused;
            if (show)
            {
                if (wireframe == null) wireframe = gameObject.AddComponent<CollisionWireframe>();
                wireframe.Refresh(surfaces);
            }
            if (wireframe != null) wireframe.SetVisible(show);
        }
        void OnDestroy()
        {
            OVRManager.TrackingLost -= TrackingLost;
            if (mesher == null) return;
            mesher.OnMeshRefreshed -= MeshRefreshed;
            mesher.OnCleared -= Cleared;
        }
    }
}
