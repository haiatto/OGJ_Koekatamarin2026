using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Koekatamarin.Tests
{
    public sealed class LegacyAppearanceTests
    {
        const string Art = "Assets/Koekatamarin/Art/Legacy/";
        [Test]
        public void ChangingFreeFontsKeepsTextLayersAndFlightState()
        {
            var go = new GameObject("Font switching");
            try
            {
                var fonts = new[] {
                    AssetDatabase.LoadAssetAtPath<Font>(Art + "mikachan-PB.ttf"),
                    AssetDatabase.LoadAssetAtPath<Font>(Art + "LightNovelPOP.otf"),
                    AssetDatabase.LoadAssetAtPath<Font>("Assets/Koekatamarin/Resources/NotoSansCJKjp-Regular.otf")
                };
                var word = go.AddComponent<WordProjectile>();
                word.Initialize(fonts[0], fonts[0].material);
                word.Launch("声が文字になる！", new Pose(new Vector3(1,2,3), Quaternion.identity));
                var body = go.GetComponent<Rigidbody>();
                foreach (var font in fonts)
                {
                    Assert.That(font, Is.Not.Null);
                    font.RequestCharactersInTexture("声が文字になる！", 100);
                    Assert.That(font.GetCharacterInfo('声', out _, 100), Is.True, font.name);
                    word.SetFont(font, font.material);
                    Assert.That(word.Alive, Is.True);
                    Assert.That(body.position, Is.EqualTo(new Vector3(1,2,3)));
                    Assert.That(body.linearVelocity, Is.EqualTo(new Vector3(0,5,10)));
                    var labels = go.GetComponentsInChildren<TextMesh>();
                    Assert.That(labels.Length, Is.EqualTo(5));
                    Assert.That(labels.All(t => t.font == font && t.text == "声が文字になる！"), Is.True);
                    CollectionAssert.AreEqual(new[] { -.5f,-.25f,0,.25f,.5f }, labels.Select(t => t.offsetZ));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void TextMeshPortUsesApprovedMikachanFontAndOriginalPrefabDimensions()
        {
            Assert.That(File.Exists(Art + "HGRPP1001.TTF"), Is.False, "Do not redistribute the original proprietary font in QuestApp");
            string original = File.ReadAllText("../New Unity Project/Assets/Game/Prefabs/text.prefab");
            float[] offsets = Regex.Matches(original, @"m_OffsetZ: ([^\r\n]+)").Cast<Match>()
                .Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).OrderBy(x => x).ToArray();
            var go = new GameObject("Original text comparison");
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(Art + "mikachan-PB.ttf");
                font.RequestCharactersInTexture("こんにちは声", 100);
                Assert.That(font.GetCharacterInfo('声', out var glyph, 100), Is.True, "Mikachan must generate Japanese glyphs in this Unity");
                var word = go.AddComponent<WordProjectile>();
                word.Initialize(font, font.material);
                word.Launch("こんにちは", new Pose(Vector3.zero, Quaternion.identity));
                var texts = go.GetComponentsInChildren<TextMesh>().OrderBy(t => t.offsetZ).ToArray();
                CollectionAssert.AreEqual(offsets, texts.Select(t => t.offsetZ).ToArray());
                Assert.That(go.transform.localScale, Is.EqualTo(new Vector3(1, 1, .2f)));
                foreach (var text in texts)
                {
                    Assert.That(text.font, Is.SameAs(font));
                    Assert.That(text.fontSize, Is.EqualTo(100));
                    Assert.That(text.characterSize, Is.EqualTo(1));
                    Assert.That(text.anchor, Is.EqualTo((TextAnchor)4));
                    Assert.That(text.transform.localScale, Is.EqualTo(new Vector3(.1f,.1f,1)));
                }
                Assert.That(texts[2].color, Is.EqualTo(Color.white));
                Assert.That(texts[0].color, Is.EqualTo(Color.black));
                word.Launch("通信", new Pose(Vector3.zero, Quaternion.identity), 1);
                Assert.That(texts[2].color, Is.EqualTo(Color.red));
                Assert.That(texts[0].color, Is.EqualTo(Color.white));
                word.Launch("赤", new Pose(Vector3.zero, Quaternion.identity), 2);
                Assert.That(texts.All(t => t.color == Color.red), Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void WaterPortRetainsOriginalWavePixelsAndMaterialValues()
        {
            const string source = "../New Unity Project/Assets/Standard Assets/Water (Pro Only)/Water4/Sources/";
            CollectionAssert.AreEqual(File.ReadAllBytes(source + "Textures/smallWavesMap.png"), File.ReadAllBytes(Art + "smallWavesMap.png"));
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Koekatamarin/Generated/SunsetOcean.mat");
            Assert.That(material.shader.name, Is.EqualTo("Koekatamarin/Water4 Port"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
            string original = File.ReadAllText(source + "Materials/Water4ExampleSimple.mat");
            foreach (string property in new[] { "_GAmplitude", "_GFrequency", "_GSteepness", "_GSpeed", "_GDirectionAB", "_GDirectionCD", "_DistortParams", "_BaseColor", "_ReflectionColor" })
            {
                var value = Regex.Match(original, "name: " + property + @"\s+second: \{([^}]+)\}").Groups[1].Value;
                var numbers = Regex.Matches(value, @"[rgba]: ([^,]+)");
                Assert.That(numbers.Count, Is.EqualTo(4), property);
                var actual = material.GetVector(property);
                for (int i=0;i<4;i++) Assert.That(actual[i], Is.EqualTo(float.Parse(numbers[i].Groups[1].Value,CultureInfo.InvariantCulture)).Within(.000001f), property);
            }
        }
        [Test]
        public void PlanarReflectionMirrorsBothEyesAcrossWaterWithoutCollapsingTheirSeparation()
        {
            var matrix = Water4Reflection.ReflectionMatrix(new Vector3(0,2,0),Vector3.up,0);
            Vector3 left = new Vector3(-.032f,3,1), right = new Vector3(.032f,3,1);
            Assert.That(matrix.MultiplyPoint(left),Is.EqualTo(new Vector3(-.032f,1,1)));
            Assert.That(Vector3.Distance(matrix.MultiplyPoint(left),matrix.MultiplyPoint(right)),Is.EqualTo(.064f).Within(.00001));
            Assert.That(Vector3.Distance(matrix.MultiplyPoint(matrix.MultiplyPoint(left)),left),Is.LessThan(.00001));
        }
    }
}
