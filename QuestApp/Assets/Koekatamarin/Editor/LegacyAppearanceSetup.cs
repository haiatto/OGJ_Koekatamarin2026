using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Koekatamarin.Editor
{
    public static class LegacyAppearanceSetup
    {
        public const string Art = "Assets/Koekatamarin/Art/Legacy/";
        const string WaterSource = "../New Unity Project/Assets/Standard Assets/Water (Pro Only)/Water4/Sources/";
        public static void Prepare()
        {
            // User-approved font substitution. Never copy the original proprietary HG font.
            if (!File.Exists(Art + "mikachan-PB.ttf")) throw new FileNotFoundException("Mikachan-PB font is missing");
            Directory.CreateDirectory("Assets/StreamingAssets/Notices");
            foreach (string notice in new[] { "Mikachan-LICENSE.txt", "LightNovelPOP-LICENSE.txt", "NotoSans-LICENSE.txt" })
                File.Copy("Assets/Koekatamarin/ThirdParty/" + notice, "Assets/StreamingAssets/Notices/" + notice, true);
            Copy(WaterSource + "Textures/smallWavesMap.png", Art + "smallWavesMap.png");
            Copy(WaterSource + "Objects/ocean_plane.FBX", Art + "ocean_plane.FBX");
            ImportOriginalMetadata(WaterSource + "Objects/ocean_plane.FBX", Art + "ocean_plane.FBX");
            var plane = (ModelImporter)AssetImporter.GetAtPath(Art + "ocean_plane.FBX");
            plane.isReadable = true;
            plane.importAnimation = false;
            plane.materialImportMode = ModelImporterMaterialImportMode.None;
            plane.SaveAndReimport();
            ImportOriginalMetadata(WaterSource + "Textures/smallWavesMap.png", Art + "smallWavesMap.png");
            ConfigureFont(Art + "mikachan-PB.ttf", "mikachan-PB");
            ConfigureFont(Art + "LightNovelPOP.otf", "07LightNovelPOP");
            // Match the original height-to-normal importer. Preserve the source pixels.
            var bump = (TextureImporter)AssetImporter.GetAtPath(Art + "smallWavesMap.png");
            bump.textureType = TextureImporterType.NormalMap;
            bump.convertToNormalmap = true;
            bump.heightmapScale = .131052643f;
            bump.normalmapFilter = TextureImporterNormalFilter.Sobel;
            bump.maxTextureSize = 256;
            bump.mipmapEnabled = true;
            bump.wrapMode = TextureWrapMode.Repeat;
            bump.textureCompression = TextureImporterCompression.Uncompressed;
            bump.SaveAndReimport();
        }
        static void ConfigureFont(string path, string family)
        {
            var font = (TrueTypeFontImporter)AssetImporter.GetAtPath(path);
            font.fontTextureCase = FontTextureCase.Dynamic;
            font.fontSize = 16;
            font.includeFontData = true;
            font.fontNames = new[] { family, "Noto Sans CJK JP" };
            font.fontReferences = new[] { AssetDatabase.LoadAssetAtPath<Font>("Assets/Koekatamarin/Resources/NotoSansCJKjp-Regular.otf") };
            var fontSettings = new SerializedObject(font);
            var property = fontSettings.GetIterator();
            while (property.Next(true))
            {
                if (property.name.Equals("m_CharacterSpacing", StringComparison.OrdinalIgnoreCase)) property.intValue = 1;
                if (property.name.Equals("m_CharacterPadding", StringComparison.OrdinalIgnoreCase)) property.intValue = 0;
                if (property.name.Equals("m_UseLegacyBoundsCalculation", StringComparison.OrdinalIgnoreCase)) property.boolValue = true;
            }
            fontSettings.ApplyModifiedPropertiesWithoutUndo();
            font.SaveAndReimport();
        }
        static void Copy(string source, string destination)
        {
            var data = File.ReadAllBytes(source);
            if (File.Exists(destination) && Convert.ToBase64String(File.ReadAllBytes(destination)) == Convert.ToBase64String(data)) return;
            File.WriteAllBytes(destination, data);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }
        static void ImportOriginalMetadata(string source, string destination)
        {
            // Let Unity migrate the source mesh/normal-map importer before API adaptations.
            string guid = AssetDatabase.AssetPathToGUID(destination);
            string metadata = File.ReadAllText(source + ".meta");
            metadata = Regex.Replace(metadata, @"(?m)^guid: \w+", "guid: " + guid);
            File.WriteAllText(destination + ".meta", metadata);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        public static Material VoiceMaterial()
        {
            const string path = "Assets/Koekatamarin/Generated/LegacyVoice.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Koekatamarin/Legacy TextMesh")); AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        public static void ApplyWater(Material material)
        {
            // Read numeric settings directly from the original material, not hand-tuned approximations.
            string source = File.ReadAllText(WaterSource + "Materials/Water4ExampleSimple.mat");
            foreach (Match entry in Regex.Matches(source, @"name: (_\w+)\s+second: ([^\r\n]+)"))
            {
                string name = entry.Groups[1].Value, value = entry.Groups[2].Value;
                if (!material.HasProperty(name)) continue;
                if (value.StartsWith("{"))
                {
                    var numbers = Regex.Matches(value, @"[rgba]: ([^,}]+)");
                    if (numbers.Count != 4) throw new InvalidDataException(name);
                    var vector = new Vector4();
                    for (int i = 0; i < 4; i++) vector[i] = float.Parse(numbers[i].Groups[1].Value, CultureInfo.InvariantCulture);
                    material.SetVector(name, vector);
                }
                else material.SetFloat(name, float.Parse(value, CultureInfo.InvariantCulture));
            }
            material.SetFloat("_GerstnerIntensity", 1); // Original shader default; not overridden in material.
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "smallWavesMap.png"));
            EditorUtility.SetDirty(material);
        }
    }
}
