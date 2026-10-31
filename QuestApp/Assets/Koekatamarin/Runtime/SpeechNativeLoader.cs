using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Koekatamarin
{
    internal static class SpeechNativeLoader
    {
#if UNITY_EDITOR_WIN
        static readonly object Gate = new object();
        static bool loaded;
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryExW(string path, IntPtr reserved, uint flags);
#endif
        internal static void PrepareEditor()
        {
#if UNITY_EDITOR_WIN
            lock (Gate)
            {
                if (loaded) return;
                string directory = Path.Combine(UnityEngine.Application.dataPath, "Plugins/x86_64/Speech");
                // Native dependency resolution is scoped to the selected plugin's directory.
                // Keep the modules loaded for the editor process, as Unity does for native plugins.
                foreach (string file in new[] { "onnxruntime.dll", "sherpa-onnx-c-api.dll" })
                    if (LoadLibraryExW(Path.Combine(directory, file), IntPtr.Zero, 8) == IntPtr.Zero)
                        throw new InvalidOperationException("Cannot load " + file + "; Windows error " + Marshal.GetLastWin32Error());
                loaded = true;
            }
#endif
        }
    }
}
