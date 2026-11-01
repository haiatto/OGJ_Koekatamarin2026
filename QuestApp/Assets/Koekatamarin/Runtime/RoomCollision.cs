using System;
using System.Collections;
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
        public string Status { get; private set; } = "部屋: 未読込";
        public void SetMixedReality(bool value)
        { mixedReality = value; mesh.ToggleEffectMeshColliders(value); }
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
            busy = true; Status = "部屋: 読込中（未設定なら空間設定）";
            try
            {
                var result = await scene.LoadSceneFromDevice();
                if (this == null) return;
                if (result != MRUK.LoadDeviceResult.Success)
                { Status = "部屋: " + result + " / 部屋読込で再試行"; return; }
                mesh.DestroyMesh();
                var room = scene.GetCurrentRoom();
                if (room == null) { Status = "部屋: 空間設定がありません"; return; }
                bool global = room.GlobalMeshAnchor != null && room.GlobalMeshAnchor.GlobalMesh != null;
                mesh.Labels = global ? MRUKAnchor.SceneLabels.GLOBAL_MESH :
                    (MRUKAnchor.SceneLabels)(-1) & ~MRUKAnchor.SceneLabels.GLOBAL_MESH;
                mesh.CreateMesh(room);
                mesh.ToggleEffectMeshColliders(mixedReality);
                Status = "部屋: " + (global ? "メッシュ" : "壁・床・家具") + " 当たり判定 " + mesh.EffectMeshObjects.Count;
                Debug.Log("ROOM_COLLISION_READY geometry=" + (global ? "mesh" : "anchors") + " count=" + mesh.EffectMeshObjects.Count);
            }
            catch (Exception e) { if (this != null) { Status = "部屋: 読込失敗 / 再試行できます"; Debug.LogWarning(e.GetType().Name + ": " + e.Message); } }
            finally { busy = false; }
        }
    }
}
