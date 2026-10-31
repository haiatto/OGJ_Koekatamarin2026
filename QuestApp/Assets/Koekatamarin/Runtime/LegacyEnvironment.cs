using UnityEngine;

namespace Koekatamarin
{
    // Optional VR scenery. The original sky mesh/texture are preserved; water uses a new URP pass.
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
            Vector3 sun = transform.rotation * new Vector3(0, .09f, 1).normalized;
            oceanInstance.SetVector("_SunDirection", new Vector4(sun.x, sun.y, sun.z, 0));
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
