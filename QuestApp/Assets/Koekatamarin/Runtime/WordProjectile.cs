using TMPro;
using UnityEngine;

namespace Koekatamarin
{
    public sealed class WordProjectile : MonoBehaviour
    {
        TextMeshPro label;
        Vector3 velocity;
        float remaining;
        float gravity;
        float floorY;
        bool classic;
        Transform viewer;
        public bool Alive => remaining > 0;
        public void Retire() { remaining = 0; gameObject.SetActive(false); }

        public void Initialize(TMP_FontAsset font, Transform viewer = null)
        {
            this.viewer = viewer;
            label = gameObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 2.2f;
            label.alignment = TextAlignmentOptions.Center;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(8, 1);
            gameObject.SetActive(false);
        }

        public void Launch(string text, Pose pose, Color color, bool classic = false, float floorY = 0)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            label.text = text;
            label.color = color;
            transform.localScale = Vector3.one * (classic ? 1.2f : .25f);
            this.classic = classic;
            this.floorY = floorY;
            // The original shoot.cs used forward 10 m/s, up 5 m/s and Rigidbody gravity.
            velocity = pose.rotation * (classic ? new Vector3(0, 5, 10) : new Vector3(0, .35f, 1.8f));
            gravity = classic ? 9.81f : .08f;
            remaining = 5;
            gameObject.SetActive(true);
        }

        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0) { gameObject.SetActive(false); return; }
            velocity += Vector3.down * (gravity * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;
            if (classic && transform.position.y < floorY + .1f)
            {
                var position = transform.position; position.y = floorY + .1f; transform.position = position;
                velocity = new Vector3(velocity.x * .4f, Mathf.Abs(velocity.y) * .15f, velocity.z * .4f);
            }
            var color = label.color;
            color.a = Mathf.Clamp01(remaining);
            label.color = color;
        }

        void LateUpdate()
        {
            if (viewer == null) return;
            Vector3 direction = transform.position - viewer.position;
            if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
