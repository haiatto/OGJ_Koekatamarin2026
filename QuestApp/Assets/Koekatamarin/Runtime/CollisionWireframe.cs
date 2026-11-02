using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Koekatamarin
{
    // Draw the physics shape, not the EffectMesh's sometimes different visual geometry.
    // Line topology works on Quest without geometry shaders or added colliders.
    public sealed class CollisionWireframe : MonoBehaviour
    {
        sealed class Entry
        {
            public Collider source;
            public Mesh sourceMesh;
            public Vector3 center, size;
            public GameObject view;
            public Mesh lines;
        }
        readonly Dictionary<Collider, Entry> entries = new Dictionary<Collider, Entry>();
        readonly HashSet<Collider> seen = new HashSet<Collider>();
        readonly List<Collider> removed = new List<Collider>();
        Material material;
        bool visible;
        public int ShapeCount => entries.Count;
        public int EdgeCount { get; private set; }
        public int UnavailableCount { get; private set; }
        public static bool HasTriangles(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount < 3) return false;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles && mesh.GetIndexCount(i) >= 3) return true;
            return false;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            foreach (var entry in entries.Values)
                if (entry.view != null) entry.view.SetActive(value && entry.source != null && entry.source.enabled);
        }

        public void Refresh(IEnumerable<Collider> colliders)
        {
            seen.Clear();
            UnavailableCount = 0;
            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                var id = collider;
                seen.Add(id);
                var sourceMesh = (collider as MeshCollider)?.sharedMesh;
                var box = collider as BoxCollider;
                var center = box != null ? box.center : Vector3.zero;
                var size = box != null ? box.size : Vector3.zero;
                if (entries.TryGetValue(id, out var entry))
                {
                    if (entry.view != null && entry.sourceMesh == sourceMesh && entry.center == center && entry.size == size)
                    { entry.view.SetActive(visible && collider.enabled); continue; }
                    Release(entry);
                    entries.Remove(id);
                }
                var lines = BuildLines(collider);
                if (lines == null) { UnavailableCount++; continue; }
                if (material == null)
                    material = new Material(Resources.Load<Shader>("RoomCollisionWire"));
                var view = new GameObject("Collision wireframe");
                view.layer = 2; // Ignore Raycast; no collider is created.
                view.transform.SetParent(collider.transform, false);
                view.AddComponent<MeshFilter>().sharedMesh = lines;
                var renderer = view.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                view.SetActive(visible && collider.enabled);
                entries.Add(id, new Entry { source = collider, sourceMesh = sourceMesh,
                    center = center, size = size, view = view, lines = lines });
            }
            removed.Clear();
            foreach (var pair in entries) if (!seen.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) { Release(entries[id]); entries.Remove(id); }
            EdgeCount = 0;
            foreach (var entry in entries.Values) EdgeCount += (int)entry.lines.GetIndexCount(0) / 2;
        }

        public void Invalidate(Collider collider)
        {
            if (collider == null || !entries.TryGetValue(collider, out var entry)) return;
            Release(entry);
            entries.Remove(collider);
            EdgeCount = 0;
            foreach (var current in entries.Values) EdgeCount += (int)current.lines.GetIndexCount(0) / 2;
        }

        public static Mesh BuildLines(Collider collider)
        {
            Vector3[] vertices;
            var indices = new List<int>();
            if (collider is BoxCollider box)
            {
                vertices = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    vertices[i] = box.center + Vector3.Scale(box.size * .5f,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                for (int i = 0; i < 8; i++)
                    for (int bit = 1; bit <= 4; bit <<= 1)
                        if ((i & bit) == 0) { indices.Add(i); indices.Add(i | bit); }
            }
            else if (collider is MeshCollider meshCollider && HasTriangles(meshCollider.sharedMesh) && meshCollider.sharedMesh.isReadable)
            {
                var source = meshCollider.sharedMesh;
                vertices = source.vertices;
                int[] triangles = source.triangles;
                // Live buffers reserve capacity; unused vertices must not affect line bounds.
                int usedVertices = 0;
                foreach (int index in triangles) usedVertices = Mathf.Max(usedVertices, index + 1);
                if (usedVertices < vertices.Length) System.Array.Resize(ref vertices, usedVertices);
                var edges = new HashSet<ulong>();
                for (int i = 0; i < triangles.Length; i += 3)
                    for (int side = 0; side < 3; side++)
                    {
                        int a = triangles[i + side], b = triangles[i + (side + 1) % 3];
                        ulong key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                        if (edges.Add(key)) { indices.Add(a); indices.Add(b); }
                    }
            }
            else return null;
            var result = new Mesh { name = "Room collider edges", indexFormat = IndexFormat.UInt32 };
            result.vertices = vertices;
            result.SetIndices(indices, MeshTopology.Lines, 0);
            result.RecalculateBounds();
            return result;
        }

        static void Dispose(Object value)
        { if (value != null) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
        static void Release(Entry entry) { Dispose(entry.view); Dispose(entry.lines); }
        public void Clear()
        { foreach (var entry in entries.Values) Release(entry); entries.Clear(); EdgeCount = 0; UnavailableCount = 0; }
        void OnDestroy() { Clear(); Dispose(material); }
    }
}
