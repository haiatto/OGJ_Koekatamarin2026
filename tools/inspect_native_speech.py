"""Inspect the actual APK's ASR ELF dependencies and C# entry points; never execute native code."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ANDROID = Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer')
LIBRARIES = ('libsherpa-onnx-c-api.so', 'libonnxruntime.so')


def command(*args) -> str:
    return subprocess.check_output([str(arg) for arg in args], text=True, encoding='utf-8', errors='strict', timeout=30)


def required_symbols() -> dict[str, set[str]]:
    result = {library: set() for library in LIBRARIES}
    paths = list((ROOT / 'QuestApp/Assets/ThirdParty/SherpaOnnx').glob('*.cs'))
    paths.append(ROOT / 'QuestApp/Assets/Koekatamarin/Runtime/LocalSpeechRecognizer.cs')
    declaration = re.compile(r'\[DllImport\((.*?)\)\]\s*(?:\w+\s+)*extern\s+\w+\s+(\w+)\s*\(', re.DOTALL)
    for path in paths:
        source = path.read_text(encoding='utf-8-sig')
        matches = list(declaration.finditer(source))
        if len(matches) != source.count('[DllImport('):
            raise ValueError('An import could not be parsed: ' + str(path))
        for match in matches:
            options, method = match.groups()
            if options.startswith('Dll.Filename'):
                library = LIBRARIES[0]
            elif options.startswith('"onnxruntime"'):
                library = LIBRARIES[1]
            else:
                raise ValueError('Unexpected speech import: ' + options)
            entry = re.search(r'EntryPoint\s*=\s*"([^"]+)"', options)
            result[library].add(entry.group(1) if entry else method)
    if any(not symbols for symbols in result.values()):
        raise ValueError('No native speech imports found')
    return result


def inspect(apk: Path, android: Path) -> dict:
    readelf = android / 'NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-readelf.exe'
    build_tools = sorted((android / 'SDK/build-tools').glob('*/aapt2.exe'),
                         key=lambda path: tuple(int(part) for part in path.parent.name.split('.') if part.isdigit()))
    if not readelf.is_file() or not build_tools:
        raise ValueError('Unity Android NDK/readelf and SDK/aapt2 are required')
    badging = command(build_tools[-1], 'dump', 'badging', apk)
    minimum = re.search(r"^(?:minSdkVersion|sdkVersion):'(\d+)'", badging, re.MULTILINE)
    if not minimum:
        raise ValueError('APK minimum Android API could not be determined')
    api = int(minimum.group(1))
    system = android / f'NDK/toolchains/llvm/prebuilt/windows-x86_64/sysroot/usr/lib/aarch64-linux-android/{api}'
    if not system.is_dir():
        raise ValueError('NDK public system stubs not found for API ' + str(api))
    with apk.open('rb') as stream:
        apk_hash = hashlib.file_digest(stream, 'sha256').hexdigest()
    scratch = ROOT / '.work/native-speech-audit' / apk_hash[:12]
    scratch.mkdir(parents=True, exist_ok=True)
    required = required_symbols()
    report = {'apk': apk.name, 'apkSha256': apk_hash, 'minimumAndroidApi': api, 'libraries': [],
              'deviceExecuted': False,
              'scope': 'ELF64/AArch64 headers, DT_NEEDED names against APK and NDK public stubs, and C# entry-point export names. Does not prove native struct layout, runtime loading, model execution, or Quest performance.'}
    with zipfile.ZipFile(apk) as archive:
        packaged = {Path(name).name for name in archive.namelist() if re.fullmatch(r'lib/arm64-v8a/[^/]+\.so', name)}
        for library in LIBRARIES:
            data = archive.read('lib/arm64-v8a/' + library)
            path = scratch / library
            path.write_bytes(data)
            output = command(readelf, '--file-header', '--dynamic', '--program-headers', '--dyn-syms', path)
            if not re.search(r'Class:\s+ELF64', output) or not re.search(r'Machine:\s+AArch64', output):
                raise ValueError('Not an ARM64 ELF library: ' + library)
            needed = re.findall(r'\(NEEDED\)\s+Shared library: \[([^\]]+)\]', output)
            missing = [name for name in needed if name not in packaged and not (system / name).is_file()]
            if missing:
                raise ValueError(f'{library}: missing dependencies {missing}')
            exported = set()
            for line in output.splitlines():
                fields = line.split()
                if len(fields) >= 8 and fields[0].endswith(':') and fields[4] in ('GLOBAL', 'WEAK') and fields[5] in ('DEFAULT', 'PROTECTED') and fields[6] != 'UND':
                    exported.add(fields[7].split('@')[0])
            absent = required[library] - exported
            if absent:
                raise ValueError(f'{library}: missing C# entry points {sorted(absent)}')
            alignments = [int(line.split()[-1], 16) for line in output.splitlines() if line.strip().startswith('LOAD ')]
            if not alignments:
                raise ValueError('No ELF LOAD segments: ' + library)
            report['libraries'].append({'name': library, 'sha256': hashlib.sha256(data).hexdigest(),
                                        'architecture': 'AArch64', 'needed': needed,
                                        'minimumLoadSegmentAlignmentBytes': min(alignments),
                                        'requiredCSharpEntryPointsPresent': sorted(required[library])})
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apk', type=Path, default=ROOT / 'QuestApp/Builds/Koekatamarin2026.apk')
    parser.add_argument('--android-player', type=Path, default=ANDROID)
    args = parser.parse_args()
    report = inspect(args.apk, args.android_player)
    destination = args.apk.with_suffix(args.apk.suffix + '.native-speech.json')
    destination.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'report': str(destination), 'librariesVerified': len(report['libraries']),
                      'entryPointsVerified': sum(len(item['requiredCSharpEntryPointsPresent']) for item in report['libraries']),
                      'deviceExecuted': False}))


if __name__ == '__main__':
    main()
