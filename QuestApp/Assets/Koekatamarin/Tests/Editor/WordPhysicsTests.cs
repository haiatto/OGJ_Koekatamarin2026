using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koekatamarin.Tests
{
    public sealed class WordPhysicsTests
    {
        [Test]
        public void FiveLayerVoiceUsesGravityAndHitsARealMeshCollider()
        {
            var voice = new GameObject("Voice");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                wall.layer = 9;
                wall.transform.position = new Vector3(1000, 2, 2);
                wall.transform.localScale = new Vector3(20, 20, .1f);
                Object.DestroyImmediate(wall.GetComponent<BoxCollider>());
                wall.AddComponent<MeshCollider>().sharedMesh = wall.GetComponent<MeshFilter>().sharedMesh;
                var word = voice.AddComponent<WordProjectile>();
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Koekatamarin/Art/Legacy/mikachan-PB.ttf");
                word.Initialize(font, font.material);
                word.Launch("縺薙ｓ縺ｫ縺｡縺ｯ", new Pose(new Vector3(1000, 1, 0), Quaternion.identity));
                Assert.That(voice.GetComponentsInChildren<TextMesh>().Length, Is.EqualTo(5));
                var body = voice.GetComponent<Rigidbody>();
                Assert.That(body.useGravity, Is.True);
                Assert.That(body.linearVelocity, Is.EqualTo(new Vector3(0, 5, 10)));
                Physics.SyncTransforms();
                var physics = Physics.defaultPhysicsScene;
                for (int i = 0; i < 30; i++) physics.Simulate(.02f);
                Assert.That(body.position.z, Is.LessThan(2), "Voice must collide, not fly through room geometry");
                Assert.That(body.linearVelocity.z, Is.LessThan(1));
                word.Retire();
                Assert.That(word.Alive, Is.False);
                Assert.That(voice.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(voice); Object.DestroyImmediate(wall); Physics.simulationMode = previousMode; }
        }
    }
}
