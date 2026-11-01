using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koekatamarin.Tests
{
    public sealed class LegacyEnvironmentTests
    {
        [Test]
        public void GeneratedSceneStartsWithVrSceneryHiddenAndHasOriginalArt()
        {
            const string path = "Assets/Koekatamarin/Generated/Playground.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var experience = scene.GetRootGameObjects().Select(x => x.GetComponent<Experience>()).First(x => x != null);
                Assert.That(experience.environment, Is.Not.Null);
                Assert.That(experience.environment.Visible, Is.False, "MR must not be occluded by VR geometry at startup");
                var renderers = experience.environment.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(3));
                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null);
                    Assert.That(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), Is.False);
                }
                var sky = renderers.First(x => x.sharedMaterial.name == "LegacySky");
                Assert.That(AssetDatabase.GetAssetPath(sky.sharedMaterial.GetTexture("_BaseMap")), Does.EndWith("Art/Legacy/Yuuyake.tga"));
                Assert.That(sky.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
                var water = renderers.First(x => x.sharedMaterial.name == "SunsetOcean");
                var sourceMesh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Koekatamarin/Art/Legacy/ocean_plane.FBX").GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(water.GetComponent<MeshFilter>().sharedMesh.triangles.Length, Is.EqualTo(sourceMesh.triangles.Length * 6));
                Assert.That(water.GetComponent<MeshFilter>().sharedMesh.bounds.size.y, Is.GreaterThanOrEqualTo(.05f));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ShowingAndHidingVrDoesNotMoveTheHeadAndReusesItsMaterial()
        {
            var go = new GameObject("VR test");
            var head = new GameObject("Head");
            var source = new Material(Shader.Find("Koekatamarin/Water4 Port"));
            Material instance = null;
            try
            {
                var environment = go.AddComponent<LegacyEnvironment>();
                environment.ocean = source;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = source;
                environment.Hide();
                head.transform.SetPositionAndRotation(new Vector3(3, 2, 4), Quaternion.Euler(0, 45, 0));
                environment.Show(head.transform);
                instance = renderer.sharedMaterial;
                Assert.That(instance, Is.Not.SameAs(source));
                Assert.That(environment.FloorY, Is.EqualTo(.35f).Within(.001));
                Assert.That(head.transform.position, Is.EqualTo(new Vector3(3, 2, 4)));
                Assert.That(environment.Visible, Is.True);
                environment.Hide();
                Assert.That(environment.Visible, Is.False);
                environment.Show(head.transform);
                Assert.That(renderer.sharedMaterial, Is.SameAs(instance), "Repeated switches reuse the one material instance");
            }
            finally
            {
                Object.DestroyImmediate(go); Object.DestroyImmediate(head); Object.DestroyImmediate(source);
                // EditMode does not run the normal MonoBehaviour lifetime; clean up the test instance explicitly.
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }
    }
}
