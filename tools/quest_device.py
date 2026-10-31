"""Install and inspect the local Quest prototype. Status is the read-only default."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import re
import statistics
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'com.haiatto.koekatamarin2026'
DEFAULT_ANDROID = Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\PlaybackEngines\AndroidPlayer')


class DeviceError(RuntimeError):
    pass


def run(arguments: list[str], timeout: int = 30) -> str:
    try:
        completed = subprocess.run(arguments, capture_output=True, text=True,
                                   encoding='utf-8', errors='replace', timeout=timeout)
    except (OSError, subprocess.TimeoutExpired) as error:
        raise DeviceError(str(error)) from error
    if completed.returncode:
        raise DeviceError(f"Command failed ({completed.returncode}): " +
                          (completed.stderr or completed.stdout).strip())
    return completed.stdout


def devices(output: str) -> list[dict]:
    found = []
    for line in output.splitlines():
        if not line.strip() or line.startswith(('List of devices', '*')):
            continue
        parts = line.split()
        if len(parts) < 2 or parts[1] not in ('device', 'offline', 'unauthorized', 'no'):
            continue
        values = dict(part.split(':', 1) for part in parts[2:] if ':' in part)
        found.append({'serial': parts[0], 'state': parts[1], 'model': values.get('model', '')})
    return found


def select_device(found: list[dict], serial: str | None) -> dict:
    # Do not silently choose the only authorized device while another is plugged in.
    matches = [item for item in found if item['serial'] == serial] if serial else found
    if not matches:
        raise DeviceError('No matching device. Connect Quest by USB and allow USB debugging in the headset.')
    if len(matches) != 1:
        raise DeviceError('Multiple devices are connected. Specify --serial from the status output.')
    selected = matches[0]
    if selected['state'] != 'device':
        raise DeviceError(f"Device is {selected['state']}. Allow USB debugging and reconnect before retrying.")
    return selected


class Quest:
    def __init__(self, adb: Path, serial: str):
        self.prefix = [str(adb), '-s', serial]

    def call(self, *arguments: str, timeout: int = 30) -> str:
        return run(self.prefix + list(arguments), timeout)

    def verify_target(self):
        model = self.call('shell', 'getprop', 'ro.product.model').strip()
        if model not in ('Quest 3', 'Meta Quest 3'):
            raise DeviceError(f'Target reports model {model!r}, not Quest 3. No install or launch performed.')

    def install(self, apk: Path, android_player: Path):
        if not apk.is_file():
            raise DeviceError(f'APK not found: {apk}')
        # Verify package, signature, ABI, speech model hashes and notices before changing a device.
        run(['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
             str(ROOT / 'tools/inspect_apk.ps1'), '-Apk', str(apk),
             '-AndroidPlayer', str(android_player)], timeout=120)
        output = self.call('install', '-r', str(apk), timeout=180)
        if not re.search(r'^Success\s*$', output, re.MULTILINE):
            raise DeviceError('Installation did not report Success: ' + output.strip())
        print(output.strip())

    def launch(self):
        resolved = self.call('shell', 'cmd', 'package', 'resolve-activity', '--brief',
                             '-a', 'android.intent.action.MAIN',
                             '-c', 'android.intent.category.LAUNCHER', PACKAGE)
        components = re.findall(r'^' + re.escape(PACKAGE) + r'/[A-Za-z0-9_.]+\s*$', resolved, re.MULTILINE)
        if len(components) != 1:
            raise DeviceError('Installed launch activity could not be resolved. Install the APK first.')
        output = self.call('shell', 'am', 'start', '-W', '-n', components[0].strip())
        if re.search(r'\b(Error|Exception)\b', output) or not re.search(r'^Status:\s*ok\s*$', output, re.MULTILINE):
            raise DeviceError('Launch was not confirmed: ' + output.strip())
        print(output.strip())

    def collect(self, apk: Path) -> Path:
        # pidof returns exit 1 when the app is not running. Never launch or restart implicitly.
        try:
            process_ids = self.call('shell', 'pidof', PACKAGE).strip().split()
        except DeviceError as error:
            raise DeviceError('The app is not running or the device disconnected. Launch it before collect.') from error
        if len(process_ids) != 1 or not process_ids[0].isdigit():
            raise DeviceError('Expected one main app process; collection stopped without choosing a process.')
        app_process = process_ids[0]
        stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S.%fZ')
        directory = ROOT / '.work/quest-device' / stamp
        directory.mkdir(parents=True, exist_ok=False)
        report = {'capturedAtUtc': stamp, 'package': PACKAGE, 'processId': int(app_process),
                  'deviceTestsPassed': None, 'errors': {}, 'properties': {}}
        for prop in ('ro.product.model', 'ro.build.version.release', 'ro.build.version.sdk', 'ro.build.display.id'):
            try:
                report['properties'][prop] = self.call('shell', 'getprop', prop).strip()
            except DeviceError as error:
                report['errors'][prop] = str(error)
        probes = {
            'app-log.txt': ('logcat', '-d', '-v', 'threadtime', '--pid=' + app_process, '-t', '5000'),
            'app-memory.txt': ('shell', 'dumpsys', 'meminfo', PACKAGE),
            'battery.txt': ('shell', 'dumpsys', 'battery'),
            'installed-package.txt': ('shell', 'dumpsys', 'package', PACKAGE),
        }
        for name, arguments in probes.items():
            try:
                output = self.call(*arguments)
                (directory / name).write_text(output, encoding='utf-8')
                if name == 'app-log.txt':
                    report['speechDecode'] = speech_metrics(output)
            except DeviceError as error:
                report['errors'][name] = str(error)
        try:
            report['sameProcessStillRunning'] = self.call('shell', 'pidof', PACKAGE).strip() == app_process
        except DeviceError:
            report['sameProcessStillRunning'] = False
        if apk.is_file():
            with apk.open('rb') as stream:
                report['localApkSha256'] = hashlib.file_digest(stream, 'sha256').hexdigest()
            report['localApkNote'] = 'Local file only; not proof this APK is the installed build. Check installed-package.txt.'
        (directory / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        return directory


def speech_metrics(log: str) -> dict:
    pairs = re.findall(r'ASR completed: audio=(\d+(?:[.,]\d+)?)s decode=(\d+(?:[.,]\d+)?)s', log)
    samples = [(float(a.replace(',', '.')), float(d.replace(',', '.'))) for a, d in pairs]
    samples = [(a, d) for a, d in samples if a > 0 and math.isfinite(a) and math.isfinite(d)]
    times = sorted(d for _, d in samples)
    return {
        'sampleCount': len(samples),
        'decodeSecondsMedian': statistics.median(times) if times else None,
        'decodeSecondsP95': times[math.ceil(len(times) * .95) - 1] if times else None,
        'decodeToAudioRatioMedian': statistics.median(d / a for a, d in samples) if samples else None,
        'note': 'Available app log samples only, rounded to 0.01s. Excludes VAD endpoint wait, queueing and display latency. Not end-to-end latency or a pass/fail result.',
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', nargs='?', choices=('status', 'install', 'launch', 'collect'), default='status')
    parser.add_argument('--serial', help='Required when multiple adb devices are connected')
    parser.add_argument('--android-player', type=Path, default=DEFAULT_ANDROID)
    parser.add_argument('--apk', type=Path, default=ROOT / 'QuestApp/Builds/Koekatamarin2026.apk')
    args = parser.parse_args(argv)
    adb = args.android_player / 'SDK/platform-tools/adb.exe'
    try:
        found = devices(run([str(adb), 'devices', '-l']))
        if args.action == 'status':
            print(json.dumps({'devices': found, 'note': 'No device changes. Allow USB debugging in the headset if unauthorized.'}, ensure_ascii=False, indent=2))
            return 0
        selected = select_device(found, args.serial)
        quest = Quest(adb, selected['serial'])
        quest.verify_target()
        if args.action == 'install':
            quest.install(args.apk.resolve(), args.android_player)
        elif args.action == 'launch':
            quest.launch()
        else:
            print(quest.collect(args.apk))
        return 0
    except DeviceError as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
