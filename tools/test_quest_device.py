"""Offline checks for device selection, failed installs and honest ASR measurements."""
from pathlib import Path
import json
import tempfile
import unittest
from unittest.mock import patch

import quest_device as device


class DeviceChecks(unittest.TestCase):
    def test_no_device_and_unauthorized_do_not_select_a_target(self):
        with self.assertRaises(device.DeviceError):
            device.select_device(device.devices('List of devices attached\n\n'), None)
        found = device.devices('List of devices attached\nquest unauthorized usb:1\n')
        with self.assertRaises(device.DeviceError):
            device.select_device(found, 'quest')

    def test_multiple_devices_require_serial_even_if_one_is_offline(self):
        found = device.devices('List of devices attached\nquest device model:Quest_3\nphone offline\n')
        with self.assertRaises(device.DeviceError):
            device.select_device(found, None)
        self.assertEqual(device.select_device(found, 'quest')['model'], 'Quest_3')

    def test_other_android_model_is_not_accepted(self):
        quest = device.Quest(Path('adb.exe'), 'phone')
        with patch.object(quest, 'call', return_value='Pixel\n') as command:
            with self.assertRaises(device.DeviceError):
                quest.verify_target()
            command.assert_called_once_with('shell', 'getprop', 'ro.product.model')

    def test_failed_apk_verification_never_installs(self):
        quest = device.Quest(Path('adb.exe'), 'quest')
        (device.ROOT / '.work').mkdir(exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=device.ROOT / '.work', suffix='.apk') as temporary:
            apk = Path(temporary.name)
            with patch.object(device, 'run', side_effect=device.DeviceError('Invalid APK')), patch.object(quest, 'call') as adb:
                with self.assertRaises(device.DeviceError):
                    quest.install(apk, Path('AndroidPlayer'))
                adb.assert_not_called()

    def test_install_failure_is_not_reported_as_success(self):
        quest = device.Quest(Path('adb.exe'), 'quest')
        (device.ROOT / '.work').mkdir(exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=device.ROOT / '.work', suffix='.apk') as temporary:
            apk = Path(temporary.name)
            with patch.object(device, 'run', return_value='verified'), patch.object(quest, 'call', return_value='Failure [INSTALL_FAILED_UPDATE_INCOMPATIBLE]') as adb:
                with self.assertRaises(device.DeviceError):
                    quest.install(apk, Path('AndroidPlayer'))
                adb.assert_called_once_with('install', '-r', str(apk), timeout=180)

    def test_launch_cannot_use_activity_from_another_package(self):
        quest = device.Quest(Path('adb.exe'), 'quest')
        with patch.object(quest, 'call', return_value='com.other/.Activity\n') as adb:
            with self.assertRaises(device.DeviceError):
                quest.launch()
            self.assertEqual(adb.call_count, 1)

    def test_log_metrics_do_not_count_missing_data_as_zero_latency(self):
        empty = device.speech_metrics('No ASR results\n')
        self.assertEqual(empty['sampleCount'], 0)
        self.assertIsNone(empty['decodeSecondsP95'])
        log = '\n'.join('ASR completed: audio=2.00s decode=' + value + 's' for value in ('0.20', '0,40', '0.60'))
        result = device.speech_metrics(log)
        self.assertEqual(result['sampleCount'], 3)
        self.assertAlmostEqual(result['decodeSecondsMedian'], .4)
        self.assertAlmostEqual(result['decodeSecondsP95'], .6)
        self.assertAlmostEqual(result['decodeToAudioRatioMedian'], .2)
        self.assertIn('Not end-to-end', result['note'])

    def test_collect_filters_logs_to_app_and_records_process_restart(self):
        quest = device.Quest(Path('adb.exe'), 'quest')
        writes = {}
        pid_calls = 0

        def response(*args, **kwargs):
            nonlocal pid_calls
            if args == ('shell', 'pidof', device.PACKAGE):
                pid_calls += 1
                return '123\n' if pid_calls == 1 else '456\n'
            if args[0] == 'logcat':
                self.assertIn('--pid=123', args)
                self.assertIn('-d', args)
                self.assertNotIn('-c', args)
                return 'ASR completed: audio=2.00s decode=0.40s\n'
            if args == ('shell', 'dumpsys', 'meminfo', device.PACKAGE):
                raise device.DeviceError('probe unavailable')
            return 'test data\n'

        def save(path, content, **kwargs):
            writes[path.name] = content
            return len(content)

        with patch.object(quest, 'call', side_effect=response), patch.object(Path, 'mkdir'), \
                patch.object(Path, 'write_text', autospec=True, side_effect=save):
            quest.collect(Path('missing-test-file.apk'))
        report = json.loads(writes['report.json'])
        self.assertFalse(report['sameProcessStillRunning'])
        self.assertIsNone(report['deviceTestsPassed'])
        self.assertIn('app-memory.txt', report['errors'])
        self.assertEqual(report['speechDecode']['sampleCount'], 1)

    def test_interim_logs_are_counted_without_treating_context_as_new_audio(self):
        result = device.speech_metrics('ASR interim: audio=0.51s decode=0.03s model=ReazonSpeech\n'
                                       'ASR interim: audio=1.02s decode=0.05s model=ReazonSpeech\n')
        self.assertEqual(result['sampleCount'], 2)
        self.assertAlmostEqual(result['decodeSecondsMedian'], .04)
        self.assertIn('not new audio per decode', result['note'])

    def test_launch_uses_only_resolved_app_component(self):
        quest = device.Quest(Path('adb.exe'), 'quest')
        component = device.PACKAGE + '/com.unity3d.player.UnityPlayerGameActivity'
        with patch.object(quest, 'call', side_effect=[component + '\n', 'Status: ok\n']) as adb, patch('builtins.print'):
            quest.launch()
            self.assertEqual(adb.call_args.args, ('shell', 'am', 'start', '-W', '-n', component))


if __name__ == '__main__':
    unittest.main()
