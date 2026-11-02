using UnityEngine;

namespace Koekatamarin
{
    // Port of Game/Prefabs/text.prefab and TextManage, including original TextMesh metrics.
    public sealed class WordProjectile : MonoBehaviour
    {
        readonly TextMesh[] layers = new TextMesh[5];
        Rigidbody body;
        float remaining;
        public bool Alive => remaining > 0;
        public void Retire()
        {
            remaining = 0;
            if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            gameObject.SetActive(false);
        }
        public void Initialize(Font font, Material material)
        {
            gameObject.layer = 8;
            transform.localScale = new Vector3(1, 1, .2f);
            body = gameObject.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.mass = 1;
            body.linearDamping = 0;
            body.angularDamping = .05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            gameObject.AddComponent<BoxCollider>().size = Vector3.one;
            for (int i = 0; i < layers.Length; i++)
            {
                var label = new GameObject("Text" + (i * 25).ToString("D3")).AddComponent<TextMesh>();
                label.gameObject.layer = 8;
                label.transform.SetParent(transform, false);
                label.transform.localScale = new Vector3(.1f, .1f, 1);
                label.offsetZ = (i - 2) * .25f;
                label.font = font;
                label.fontSize = 100;
                label.characterSize = 1;
                label.lineSpacing = 1;
                label.tabSize = 4;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Left;
                label.fontStyle = FontStyle.Normal;
                label.richText = true;
                label.GetComponent<MeshRenderer>().sharedMaterial = material;
                layers[i] = label;
            }
            gameObject.SetActive(false);
        }
        public void SetFont(Font font, Material material)
        {
            foreach (var label in layers)
            {
                label.font = font;
                label.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
        // Keep the original proportions and fixed collision box at 100%.
        // Apply when launching, so already flying words never grow into nearby geometry.
        public void SetSize(float factor)
        { transform.localScale = new Vector3(1, 1, .2f) * Mathf.Clamp(factor, .25f, 2f); }
        // 0: single-player callback; 1: network prefab; 2: network random red.
        public void Launch(string text, Pose pose, int style = 0)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i].text = text;
                layers[i].color = style == 0 ? (i == 2 ? Color.white : Color.black) :
                    style == 2 ? Color.red : (i == 2 ? Color.red : Color.white);
            }
            remaining = 5;
            gameObject.SetActive(true);
            body.position = pose.position;
            body.rotation = pose.rotation;
            body.angularVelocity = Vector3.zero;
            body.linearVelocity = pose.rotation * new Vector3(0, 5, 10);
        }
        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0) Retire();
        }
    }
}
