"""Fetch pinned offline ASR dependencies. Python 3.12+, no pip packages required.

Large binaries/models stay out of Git; this script reconstructs the Unity assets.
Source bindings are a minimal dependency closure from sherpa-onnx v2.0.1.
"""
import hashlib
import json
from pathlib import Path
import re
import shutil
import tarfile
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parent.parent
CACHE = ROOT / '.work/asr'
ASSETS = ROOT / 'QuestApp/Assets'
VERSION = '2.0.1'
MODEL = 'sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17'
RELEASE = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/'
DOWNLOADS = {
    'source.tar.gz': ('https://github.com/k2-fsa/sherpa-onnx/archive/refs/tags/v2.0.1.tar.gz', '24ecc0fccf6344c2556e7c229c849e9f61bf9456b915615197d85f35e3f177e7'),
    'android.tar.bz2': (RELEASE + 'v2.0.1/sherpa-onnx-v2.0.1-android.tar.bz2', '39003753ec293425ed56723ea9b972b98841a490af3311670c3060548d654330'),
    'windows.tar.bz2': (RELEASE + 'v2.0.1/sherpa-onnx-v2.0.1-win-x64-shared-MD-Release-no-tts.tar.bz2', '11fd6a50b97eed38b54fb16d8219f49b16aa7f6a8e43a042316469ef47652e82'),
    'model.tar.bz2': (RELEASE + 'asr-models/' + MODEL + '.tar.bz2', '7d1efa2138a65b0b488df37f8b89e3d91a60676e416f515b952358d83dfd347e'),
    'silero_vad.onnx': (RELEASE + 'asr-models/silero_vad.onnx', '9e2449e1087496d8d4caba907f23e0bd3f78d91fa552479bb9c23ac09cbb1fd6'),
}


def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def fetch(name, url, digest):
    path = CACHE / name
    if not path.exists() or sha(path) != digest:
        print('Downloading', name, flush=True)
        temporary = path.with_suffix(path.suffix + '.download')
        urllib.request.urlretrieve(url, temporary)
        if sha(temporary) != digest:
            raise RuntimeError('SHA256 mismatch: ' + name)
        temporary.replace(path)
    return path


def plugin(source, destination, android):
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    # Deterministic metadata; never let an Android library load in the desktop editor.
    guid = uuid.uuid5(uuid.NAMESPACE_URL, str(destination.relative_to(ROOT)).replace('\\', '/')).hex
    editor = 0 if android else 1
    platform = 'Android: Android' if android else 'Standalone: Win64'
    cpu = 'ARM64' if android else 'x86_64'
    destination.with_suffix(destination.suffix + '.meta').write_text(f'''fileFormatVersion: 2
guid: {guid}
PluginImporter:
  serializedVersion: 3
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: {editor}
      settings:
        CPU: x86_64
        OS: Windows
  - first:
      {platform}
    second:
      enabled: 1
      settings:
        CPU: {cpu}
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')


def bindings(source):
    all_files = {p: p.read_text(encoding='utf-8') for p in source.glob('*.cs')}
    types = {}
    for path, content in all_files.items():
        for name in re.findall(r'(?:class|struct)\s+(\w+)', content):
            types[name] = path
    selected = set()
    pending = [types[n] for n in ('OfflineRecognizer', 'VoiceActivityDetector')]
    while pending:
        path = pending.pop()
        if path in selected:
            continue
        selected.add(path)
        pending.extend(types[word] for word in set(re.findall(r'\b\w+\b', all_files[path])) if word in types and types[word] not in selected)
    structs = set()
    for path in selected:
        structs.update(re.findall(r'public struct (\w+)', all_files[path]))
    destination = ASSETS / 'ThirdParty/SherpaOnnx'
    destination.mkdir(parents=True, exist_ok=True)
    for path in sorted(selected):
        content = all_files[path]
        # Unity uses C# 9; preserve upstream struct defaults with an explicit constructor argument.
        for name in structs:
            content = content.replace('public ' + name + '()', 'public ' + name + '(bool defaults)')
            content = content.replace('new ' + name + '()', 'new ' + name + '(true)')
        content = content.replace('[DllImport(Dll.Filename', '[DllImport(Dll.Filename, CallingConvention = CallingConvention.Cdecl')
        if path.name in ('OfflineRecognizer.cs', 'VoiceActivityDetector.cs'):
            content = content.replace('_handle = new HandleRef(this, h);', 'if (h == IntPtr.Zero) throw new InvalidOperationException("Native ASR initialization failed");\n            _handle = new HandleRef(this, h);')
        if path.name == 'OfflineStream.cs':
            content = content.replace('_handle = new HandleRef(this, p);', 'if (p == IntPtr.Zero) throw new InvalidOperationException("Native stream initialization failed");\n            _handle = new HandleRef(this, p);')
        content = '// Vendored v2.0.1; Unity compatibility changes generated by tools/setup_speech.py.\n' + content
        content = '\n'.join(line.rstrip() for line in content.splitlines()).rstrip() + '\n'
        (destination / path.name).write_text(content, encoding='utf-8')
    (destination / 'SherpaOnnx.asmdef').write_text(json.dumps({'name': 'SherpaOnnx', 'allowUnsafeCode': True, 'autoReferenced': True}, indent=2) + '\n')
    shutil.copyfile(source.parent.parent / 'LICENSE', destination / 'LICENSE.txt')
    print('Prepared', len(selected), 'C# binding files', flush=True)


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    unpacked = CACHE / 'unpacked'
    for name, (url, digest) in DOWNLOADS.items():
        path = fetch(name, url, digest)
        stamp = CACHE / (name + '.extracted')
        if '.tar.' in name and (not stamp.exists() or stamp.read_text() != digest):
            with tarfile.open(path) as archive:
                # No symlinks, including unrelated upstream development symlinks.
                archive.extractall(unpacked, members=[m for m in archive.getmembers() if m.isfile() or m.isdir()], filter='data')
            stamp.write_text(digest)
    bindings(unpacked / 'sherpa-onnx-2.0.1/scripts/dotnet')
    for name in ('libonnxruntime.so', 'libsherpa-onnx-c-api.so'):
        plugin(unpacked / 'jniLibs/arm64-v8a' / name, ASSETS / 'Plugins/Android/Speech/arm64-v8a' / name, True)
    for name in ('onnxruntime.dll', 'onnxruntime_providers_shared.dll', 'sherpa-onnx-c-api.dll'):
        plugin(unpacked / 'sherpa-onnx-v2.0.1-win-x64-shared-MD-Release-no-tts/lib' / name, ASSETS / 'Plugins/x86_64/Speech' / name, False)
    models = ASSETS / 'StreamingAssets/Speech'
    models.mkdir(parents=True, exist_ok=True)
    for name in ('model.int8.onnx', 'tokens.txt'):
        shutil.copyfile(unpacked / MODEL / name, models / name)
    shutil.copyfile(CACHE / 'silero_vad.onnx', models / 'silero_vad.onnx')
    print('Offline speech assets ready. No download is needed on the Quest.', flush=True)


if __name__ == '__main__':
    main()
