using TMPro;
using UnityEngine;

namespace Koekatamarin
{
    public sealed class WordProjectile : MonoBehaviour
    {
        TextMeshPro label;
        Vector3 velocity;
        float remaining;
        public bool Alive => remaining > 0;

        public void Initialize(TMP_FontAsset font)
        {
            label = gameObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 2.2f;
            label.alignment = TextAlignmentOptions.Center;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(8, 1);
            gameObject.SetActive(false);
        }

        public void Launch(string text, Pose pose, Color color)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            label.text = text;
            label.color = color;
            transform.localScale = Vector3.one * .25f;
            velocity = pose.rotation * new Vector3(0, .35f, 1.8f);
            remaining = 5;
            gameObject.SetActive(true);
        }

        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0) { gameObject.SetActive(false); return; }
            velocity += Vector3.down * (.08f * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;
            var color = label.color;
            color.a = Mathf.Clamp01(remaining);
            label.color = color;
        }
    }
}
