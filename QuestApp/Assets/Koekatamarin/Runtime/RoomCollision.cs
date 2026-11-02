using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Koekatamarin
{
    public sealed class RoomCollision : MonoBehaviour
    {
        public MRUK scene;
        public EffectMesh mesh;
        bool busy;
        bool mixedReality = true;
        CollisionWireframe wireframe;
        readonly List<Collider> colliders = new List<Collider>();
        float nextWireRefresh;
        float loadStarted;
        string lastWireDiagnostic;
        public bool WireVisible { get; private set; }
        public bool WireReady => wireframe != null && wireframe.EdgeCount > 0;
        public string WireStatus => !WireVisible ? "空間線: OFF" : !mixedReality ? "空間線: MRのみ" :
            busy ? "空間線: 部屋読込待ち" : wireframe == null || wireframe.ShapeCount == 0 ? "空間線: 形状なし / 部屋読込・空間設定を確認" :
            "空間線: 形状" + wireframe.ShapeCount + " / 線" + wireframe.EdgeCount + " / 未取得" + wireframe.UnavailableCount;
        public string Status { get; private set; } = "部屋: 未読込";
        public void SetMixedReality(bool value)
        {
            mixedReality = value;
            mesh.ToggleEffectMeshColliders(value);
            RefreshWireframe();
        }
        public void SetWireVisible(bool value)
        {
            WireVisible = value;
            RefreshWireframe();
            if (value && mesh.EffectMeshObjects.Count == 0 && !busy) RequestLoad();
        }
        void RefreshWireframe()
        {
            if (WireVisible && mixedReality)
            {
                if (wireframe == null) wireframe = gameObject.AddComponent<CollisionWireframe>();
                colliders.Clear();
                foreach (var item in mesh.EffectMeshObjects.Values) colliders.Add(item.collider);
                wireframe.Refresh(colliders);
            }
            if (wireframe != null) wireframe.SetVisible(WireVisible && mixedReality);
            string diagnostic = WireStatus;
            if (WireVisible && diagnostic != lastWireDiagnostic)
            {
                lastWireDiagnostic = diagnostic;
                Debug.Log("ROOM_WIRE " + diagnostic);
            }
        }
        void Update()
        {
            if (busy && loadStarted > 0 && Time.unscaledTime - loadStarted > 15)
                Status = "部屋: 読込継続中 / スキャンした場所・空間設定を確認";
            // MRUK can replace anchor meshes after a room update.
            if (!WireVisible || !mixedReality || Time.unscaledTime < nextWireRefresh) return;
            nextWireRefresh = Time.unscaledTime + .5f;
            RefreshWireframe();
        }
        public IEnumerator LoadAfterMicrophone(SpeechInput speech)
        {
            yield return null;
            while (!OVRManager.hasInputFocus || (speech.Running && !speech.IsCapturing)) yield return null;
            RequestLoad();
        }
        public void RequestLoad()
        {
            if (busy) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                busy = true; Status = "部屋: 空間データの許可待ち";
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { busy = false; if (this != null) Load(); };
                callbacks.PermissionDenied += _ => { busy = false; if (this != null) Status = "部屋: 空間データ未許可 / 部屋読込で再試行"; };
                Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
                return;
            }
            Load();
#else
            Status = "部屋: Quest実機で読込";
#endif
        }
        async void Load()
        {
            if (busy || this == null) return;
            busy = true; loadStarted = Time.unscaledTime;
            Status = "部屋: 読込中（未設定なら空間設定）";
            Debug.Log("ROOM_LOAD_BEGIN");
            try
            {
                var result = await scene.LoadSceneFromDevice();
                if (this == null) return;
                Debug.Log("ROOM_LOAD_RESULT " + result);
                if (result != MRUK.LoadDeviceResult.Success)
                { Status = "部屋: " + result + " / 部屋読込で再試行"; return; }
                if (wireframe != null) wireframe.Clear();
                mesh.DestroyMesh();
                var room = scene.GetCurrentRoom();
                if (room == null) { Status = "部屋: 空間設定がありません"; return; }
                // MRUK may return a non-null but empty Mesh when its triangle data is unavailable.
                bool global = room.GlobalMeshAnchor != null && CollisionWireframe.HasTriangles(room.GlobalMeshAnchor.GlobalMesh);
                mesh.Labels = global ? MRUKAnchor.SceneLabels.GLOBAL_MESH :
                    (MRUKAnchor.SceneLabels)(-1) & ~MRUKAnchor.SceneLabels.GLOBAL_MESH;
                mesh.CreateMesh(room);
                mesh.ToggleEffectMeshColliders(mixedReality);
                RefreshWireframe();
                Status = "部屋: " + (global ? "メッシュ" : "壁・床・家具") + " 当たり判定 " + mesh.EffectMeshObjects.Count;
                Debug.Log("ROOM_COLLISION_READY geometry=" + (global ? "mesh" : "anchors") + " count=" + mesh.EffectMeshObjects.Count);
            }
            catch (Exception e) { if (this != null) { Status = "部屋: 読込失敗 / 再試行できます"; Debug.LogWarning(e.GetType().Name + ": " + e.Message); } }
            finally { busy = false; loadStarted = 0; }
        }
    }
}
