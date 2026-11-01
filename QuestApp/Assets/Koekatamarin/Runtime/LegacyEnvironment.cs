using UnityEngine;

namespace Koekatamarin
{
    // Optional VR scenery, with the original art and a URP port of the active Water4 shader.
    public sealed class LegacyEnvironment : MonoBehaviour
    {
        public Material ocean;
        Material oceanInstance;
        public bool Visible => gameObject.activeSelf;
        public float FloorY => transform.position.y;

        public void Show(Transform head)
        {
            // Eye-level tracking has no shared floor origin. Place a stationary beach on entering VR.
            transform.SetPositionAndRotation(head.position - Vector3.up * 1.65f,
                Quaternion.Euler(0, head.eulerAngles.y, 0));
            if (oceanInstance == null)
            {
                oceanInstance = new Material(ocean);
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.sharedMaterial == ocean) renderer.sharedMaterial = oceanInstance;
            }
            Vector3 light = transform.rotation * (Vector3)ocean.GetVector("_WorldLightDir");
            oceanInstance.SetVector("_WorldLightDir", new Vector4(light.x, light.y, light.z, 0));
            gameObject.SetActive(true);
        }

        public void Hide() { gameObject.SetActive(false); }
        void OnDestroy()
        {
            if (oceanInstance == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(oceanInstance); return; }
#endif
            Destroy(oceanInstance);
        }
    }
}
