using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Koekatamarin.Editor
{
    public static class LegacyEnvironmentSetup
    {
        const string Art = "Assets/Koekatamarin/Art/Legacy/";
        const string Generated = "Assets/Koekatamarin/Generated/";

        public static LegacyEnvironment Create()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Art + "Yuuhi.FBX");
            if (importer == null) throw new FileNotFoundException("Original sky mesh is missing");
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.globalScale = .01f;
            importer.SaveAndReimport();
            foreach (string file in new[] { "Yuuyake.tga", "Sand_Pattern.jpg" })
            {
                var texture = (TextureImporter)AssetImporter.GetAtPath(Art + file);
                texture.maxTextureSize = 1024;
                texture.mipmapEnabled = true;
                texture.wrapMode = file == "Yuuyake.tga" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                var platform = texture.GetPlatformTextureSettings("Android");
                platform.overridden = true;
                platform.format = TextureImporterFormat.ASTC_6x6;
                platform.maxTextureSize = 1024;
                texture.SetPlatformTextureSettings(platform);
                texture.SaveAndReimport();
            }
            var skyMaterial = Material("LegacySky", "Universal Render Pipeline/Unlit");
            skyMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Yuuyake.tga"));
            skyMaterial.SetColor("_BaseColor", Color.white);
            skyMaterial.SetFloat("_Cull", (float)CullMode.Off);
            var sand = Material("LegacySand", "Universal Render Pipeline/Unlit");
            sand.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Sand_Pattern.jpg"));
            sand.SetTextureScale("_BaseMap", new Vector2(30, 18));
            sand.SetColor("_BaseColor", new Color(.65f, .35f, .17f));
            var water = Material("SunsetOcean", "Koekatamarin/Sunset Ocean");

            var environment = new GameObject("Legacy sunset beach (VR only)").AddComponent<LegacyEnvironment>();
            environment.ocean = water;
            var skySource = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Yuuhi.FBX");
            var sky = UnityEngine.Object.Instantiate(skySource, environment.transform);
            sky.name = "Original Yuuhi sky";
            sky.transform.localRotation = Quaternion.Euler(0, 3.339f, 0);
            var renderers = sky.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Sky mesh import failed");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterial = skyMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; }
            // Preserve source proportions, scale the dome to a radius near 150 m.
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            sky.transform.localScale *= 150 / radius;
            sky.transform.localPosition = new Vector3(0, 50, 0);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Sand beach";
            ground.transform.SetParent(environment.transform, false);
            ground.transform.localPosition = new Vector3(0, -.13f, -25);
            ground.transform.localScale = new Vector3(220, .2f, 64); // front edge z=7
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<MeshRenderer>().sharedMaterial = sand;
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var oceanObject = new GameObject("Ocean", typeof(MeshFilter), typeof(MeshRenderer));
            oceanObject.transform.SetParent(environment.transform, false);
            oceanObject.transform.localPosition = new Vector3(0, -.08f, 0);
            var mesh = MakeOcean();
            string meshPath = Generated + "Ocean.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            oceanObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            oceanObject.GetComponent<MeshRenderer>().sharedMaterial = water;
            oceanObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(skyMaterial); EditorUtility.SetDirty(sand); EditorUtility.SetDirty(water);
            environment.Hide();
            return environment;
        }

        static Material Material(string name, string shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Generated + name + ".mat");
            if (material == null)
            {
                var loaded = Shader.Find(shader);
                if (loaded == null) throw new InvalidOperationException("Missing shader: " + shader);
                material = new Material(loaded) { name = name };
                AssetDatabase.CreateAsset(material, Generated + name + ".mat");
            }
            return material;
        }

        static Mesh MakeOcean()
        {
            const int width = 80, depth = 60;
            var vertices = new Vector3[(width + 1) * (depth + 1)];
            var triangles = new int[width * depth * 6];
            for (int z = 0; z <= depth; z++)
                for (int x = 0; x <= width; x++)
                    vertices[z * (width + 1) + x] = new Vector3(Mathf.Lerp(-110, 110, x / (float)width), 0, Mathf.Lerp(7, 170, z / (float)depth));
            int cursor = 0;
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    int a = z * (width + 1) + x, b = a + width + 1;
                    triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = a + 1;
                    triangles[cursor++] = a + 1; triangles[cursor++] = b; triangles[cursor++] = b + 1;
                }
            var mesh = new Mesh { name = "Sunset ocean grid", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(.1f); mesh.bounds = bounds;
            return mesh;
        }
    }
}
