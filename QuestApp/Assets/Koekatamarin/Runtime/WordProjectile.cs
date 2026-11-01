using TMPro;
using UnityEngine;

namespace Koekatamarin
{
    // Original text.prefab: five text planes, Rigidbody gravity and a box collider.
    public sealed class WordProjectile : MonoBehaviour
    {
        readonly TextMeshPro[] layers = new TextMeshPro[5];
        Rigidbody body;
        BoxCollider box;
        float remaining;
        public bool Alive => remaining > 0;
        public void Retire()
        {
            remaining = 0;
            if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            gameObject.SetActive(false);
        }
        public void Initialize(TMP_FontAsset font, Transform viewer = null)
        {
            gameObject.layer = 8;
            body = gameObject.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            box = gameObject.AddComponent<BoxCollider>();
            for (int i = 0; i < layers.Length; i++)
            {
                var label = new GameObject("Voice layer " + i).AddComponent<TextMeshPro>();
                label.gameObject.layer = 8;
                label.transform.SetParent(transform, false);
                // Original offsets -0.5,-0.25,0,0.25,0.5 multiplied by root Z scale 0.2.
                label.transform.localPosition = new Vector3(0, 0, (i - 2) * .05f);
                label.font = font;
                label.fontSize = 2.2f;
                label.alignment = TextAlignmentOptions.Center;
                label.richText = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.sizeDelta = new Vector2(8, 1);
                layers[i] = label;
            }
            gameObject.SetActive(false);
        }
        public void Launch(string text, Pose pose, Color color, bool classic = false, float floorY = 0)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            transform.localScale = Vector3.one * 1.2f;
            foreach (var label in layers) label.text = text;
            for (int i = 0; i < layers.Length; i++) layers[i].color = i == 2 ? Color.white : Color.black;
            remaining = 5;
            gameObject.SetActive(true);
            layers[2].ForceMeshUpdate();
            var bounds = layers[2].textBounds;
            box.center = new Vector3(bounds.center.x, bounds.center.y, 0);
            box.size = new Vector3(Mathf.Max(.1f, bounds.size.x), Mathf.Max(.1f, bounds.size.y), .22f);
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
