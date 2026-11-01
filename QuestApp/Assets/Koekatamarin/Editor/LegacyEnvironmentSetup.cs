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
            var water = Material("SunsetOcean", "Koekatamarin/Water4 Port");
            water.shader = Shader.Find("Koekatamarin/Water4 Port");
            LegacyAppearanceSetup.ApplyWater(water);

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
            // The shore depth gradient is part of Water4's edge blending, so retain the
            // original ground geometry rather than the prototype's flat substitute.
            ground.transform.localPosition = new Vector3(0, -.062579751f, -6.61979866f);
            ground.transform.localRotation = new Quaternion(.0730102956f, 0, 0, .997331262f);
            ground.transform.localScale = new Vector3(140, 1, 70);
            ground.layer = 9;
            ground.GetComponent<MeshRenderer>().sharedMaterial = sand;
            ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var oceanObject = new GameObject("Ocean", typeof(MeshFilter), typeof(MeshRenderer));
            oceanObject.transform.SetParent(environment.transform, false);
            oceanObject.transform.localPosition = Vector3.zero;
            var mesh = MakeOcean();
            string meshPath = Generated + "Ocean.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            oceanObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            oceanObject.layer = 9;
            oceanObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            oceanObject.GetComponent<MeshRenderer>().sharedMaterial = water;
            oceanObject.AddComponent<Water4Reflection>().water = oceanObject.GetComponent<MeshRenderer>();
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
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "ocean_plane.FBX");
            var original = model.GetComponentInChildren<MeshFilter>().sharedMesh;
            var tiles = new CombineInstance[6];
            int cursor = 0;
            foreach (float x in new[] { -50f, 0f, 50f })
                foreach (float z in new[] { 15f, 65f })
                    tiles[cursor++] = new CombineInstance { mesh = original,
                        transform = Matrix4x4.TRS(new Vector3(x,0,z),Quaternion.identity,Vector3.one * .5f) };
            var mesh = new Mesh { name = "Original Water4 six tiles", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(tiles, true, true);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(4f); mesh.bounds = bounds;
            return mesh;
        }
    }
}
