using System;
using System.IO;
using System.Security.Cryptography;

namespace Koekatamarin
{
    public static class SpeechModels
    {
        public enum Model { SenseVoice, ReazonSpeech }
        public static string Name(Model model) => model == Model.ReazonSpeech ? "ReazonSpeech" : "SenseVoice";
        public static string Cache(Model model) => model == Model.ReazonSpeech ? "reazon-k2-291488c-sherpa-2.0.1" : CacheVersion;
        public static string[] ModelFiles(Model model) => model == Model.ReazonSpeech ? ReazonFiles : Files;
        public static string[] ModelHashes(Model model) => model == Model.ReazonSpeech ? ReazonHashes : Hashes;
        public static readonly string[] ReazonFiles = {
            "Reazon/encoder-epoch-99-avg-1.int8.onnx", "Reazon/decoder-epoch-99-avg-1.onnx",
            "Reazon/joiner-epoch-99-avg-1.int8.onnx", "Reazon/tokens.txt"
        };
        public static readonly string[] ReazonHashes = {
            "2c7bd08a8a99f9ddd0d9e458456577b1f6279214e51426f114f9eced44c54e1d",
            "58b18211ae06265466bfa17172dab574df94f76c8bcb61a3640c28ba860e4124",
            "49cc7ea1d3d35a40a27442db5e89996da64bf0e683a903dce76e99e57a12e4de",
            "2c3ac659818a48a0c04010e0593bbc4d7c8a24a054340b01131499c05fd52def"
        };
        public const string CacheVersion = "sensevoice-20240717-sherpa-2.0.1";
        public static readonly string[] Files = { "model.int8.onnx", "tokens.txt" };
        public static readonly string[] Hashes = {
            "c71f0ce00bec95b07744e116345e33d8cbbe08cef896382cf907bf4b51a2cd51",
            "f449eb28dc567533d7fa59be34e2abca8784f771850c78a47fb731a31429a1dc"
        };

        public static bool Valid(string path, string expected)
        {
            if (!File.Exists(path)) return false;
            using var input = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant() == expected;
        }
    }
}
