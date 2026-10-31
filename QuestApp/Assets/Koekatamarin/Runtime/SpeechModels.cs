using System;
using System.IO;
using System.Security.Cryptography;

namespace Koekatamarin
{
    public static class SpeechModels
    {
        public const string CacheVersion = "sensevoice-20240717-sherpa-2.0.1";
        public static readonly string[] Files = { "model.int8.onnx", "tokens.txt", "silero_vad.onnx" };
        public static readonly string[] Hashes = {
            "c71f0ce00bec95b07744e116345e33d8cbbe08cef896382cf907bf4b51a2cd51",
            "f449eb28dc567533d7fa59be34e2abca8784f771850c78a47fb731a31429a1dc",
            "9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6"
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
