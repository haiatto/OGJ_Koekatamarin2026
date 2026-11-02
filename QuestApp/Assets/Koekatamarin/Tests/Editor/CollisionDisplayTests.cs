using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Koekatamarin.Tests
{
    public sealed class CollisionDisplayTests
    {
        [Test]
        public void LiveMeshChangesInPlaceAndEmptyResultRemoveStaleWire()
        {
            var owner = new GameObject("Live room");
            var obstacle = new GameObject("Live surface");
            var source = new Mesh();
            try
            {
                // The unused reserved vertex must not enlarge the rendered bounds.
                source.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one * 999 };
                source.triangles = new[] { 0, 1, 2 };
                var collider = obstacle.AddComponent<MeshCollider>();
                collider.sharedMesh = source;
                var display = owner.AddComponent<CollisionWireframe>();
                display.SetVisible(true);
                display.Refresh(new[] { collider });
                Assert.That(obstacle.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(3));
                source.vertices = new[] { Vector3.zero, Vector3.right * 2, Vector3.up, Vector3.one * 999 };
                display.Invalidate(collider);
                display.Refresh(new[] { collider });
                Assert.That(obstacle.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.x, Is.EqualTo(2));
                collider.sharedMesh = null;
                display.Invalidate(collider);
                display.Refresh(new[] { collider });
                Assert.That(display.EdgeCount, Is.Zero);
                Assert.That(obstacle.GetComponentInChildren<MeshFilter>(), Is.Null);
                collider.sharedMesh = source;
                display.Refresh(new[] { collider });
                Assert.That(display.EdgeCount, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(obstacle); Object.DestroyImmediate(source); }
        }

        [Test]
        public void MissingOrEmptyRoomMeshIsNotReportedAsDrawableAndCanRecover()
        {
            var go = new GameObject("Room awaiting mesh");
            var owner = new GameObject("Wire display");
            var source = new Mesh();
            try
            {
                Assert.That(CollisionWireframe.HasTriangles(source), Is.False);
                var collider = go.AddComponent<MeshCollider>();
                var display = owner.AddComponent<CollisionWireframe>();
                display.SetVisible(true);
                display.Refresh(new[] { collider });
                Assert.That(display.ShapeCount, Is.Zero);
                Assert.That(display.EdgeCount, Is.Zero);
                Assert.That(display.UnavailableCount, Is.EqualTo(1));
                source.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                source.triangles = new[] { 0, 1, 2 };
                collider.sharedMesh = source;
                Assert.That(CollisionWireframe.HasTriangles(source), Is.True);
                display.Refresh(new[] { collider });
                Assert.That(display.ShapeCount, Is.EqualTo(1));
                Assert.That(display.EdgeCount, Is.EqualTo(3));
                Assert.That(display.UnavailableCount, Is.Zero);
                display.Clear();
                Assert.That(display.ShapeCount, Is.Zero);
                Assert.That(display.EdgeCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(go); Object.DestroyImmediate(source); }
        }

        [Test]
        public void BoxDisplayFollowsActualColliderAndDoesNotChangePhysics()
        {
            var owner = new GameObject("Display owner");
            var obstacle = new GameObject("Furniture");
            try
            {
                obstacle.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(0, 30, 0));
                obstacle.transform.localScale = new Vector3(2, 1, 3);
                var box = obstacle.AddComponent<BoxCollider>();
                box.center = new Vector3(.2f, .3f, -.5f);
                box.size = new Vector3(1, 2, 3);
                var display = owner.AddComponent<CollisionWireframe>();
                display.SetVisible(true);
                display.Refresh(new[] { box });
                var renderer = obstacle.GetComponentInChildren<MeshRenderer>();
                var filter = renderer.GetComponent<MeshFilter>();
                Assert.That(filter.sharedMesh.GetIndexCount(0), Is.EqualTo(24), "12 box edges");
                Assert.That(Vector3.Distance(filter.sharedMesh.bounds.center, box.center), Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(filter.sharedMesh.bounds.size, box.size), Is.LessThan(.00001f));
                Assert.That(renderer.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(renderer.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(renderer.sharedMaterial.shader, Is.SameAs(Resources.Load<Shader>("RoomCollisionWire")));
                Assert.That(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), Is.False);
                Assert.That(obstacle.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                display.SetVisible(false);
                Assert.That(renderer.gameObject.activeSelf, Is.False);
                Assert.That(box.enabled, Is.True, "Hiding the display must not turn off collisions");
                display.SetVisible(true);
                Assert.That(renderer.gameObject.activeSelf, Is.True);
                box.enabled = false;
                display.Refresh(new[] { box });
                Assert.That(renderer.gameObject.activeSelf, Is.False);
                box.enabled = true;
                box.size = Vector3.one;
                display.Refresh(new[] { box });
                Assert.That(obstacle.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size, Is.EqualTo(Vector3.one));
                display.Refresh(System.Array.Empty<Collider>());
                Assert.That(obstacle.GetComponentInChildren<MeshFilter>(), Is.Null);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(obstacle); }
        }

        [Test]
        public void MeshDisplayUsesTriangleEdgesOnceIncludingSharedEdge()
        {
            var go = new GameObject("Scanned room");
            var source = new Mesh();
            Mesh lines = null;
            try
            {
                source.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
                source.triangles = new[] { 0, 1, 2, 2, 1, 3 };
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = source;
                lines = CollisionWireframe.BuildLines(collider);
                CollectionAssert.AreEqual(source.vertices, lines.vertices);
                Assert.That(lines.GetTopology(0), Is.EqualTo(MeshTopology.Lines));
                Assert.That(lines.GetIndexCount(0), Is.EqualTo(10), "5 unique edges, shared edge appears once");
                Assert.That(collider.sharedMesh, Is.SameAs(source));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(source); if (lines != null) Object.DestroyImmediate(lines); }
        }

        [Test]
        public void PooledWordSizeScalesCollisionAndFiveLayersAndCanReturnToOriginal()
        {
            var go = new GameObject("Word sizes");
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Koekatamarin/Art/Legacy/mikachan-PB.ttf");
                var word = go.AddComponent<WordProjectile>();
                word.Initialize(font, font.material);
                foreach (float size in new[] { .25f, 2f, 1f })
                {
                    word.SetSize(size);
                    word.Launch("声", new Pose(new Vector3(500, 2, 0), Quaternion.identity));
                    Physics.SyncTransforms();
                    Assert.That(Vector3.Distance(go.GetComponent<BoxCollider>().bounds.size, new Vector3(size, size, .2f * size)), Is.LessThan(.0001f));
                    var layers = go.GetComponentsInChildren<TextMesh>().OrderBy(t => t.offsetZ).ToArray();
                    Assert.That(layers.Length, Is.EqualTo(5));
                    Assert.That(layers[0].transform.lossyScale, Is.EqualTo(new Vector3(.1f, .1f, .2f) * size));
                    Assert.That(go.GetComponent<Rigidbody>().linearVelocity, Is.EqualTo(new Vector3(0, 5, 10)));
                    word.Retire();
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
