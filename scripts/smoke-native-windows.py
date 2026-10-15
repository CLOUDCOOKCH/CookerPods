#!/usr/bin/env python3
"""Exercise the packaged diagnostic on Windows; no Bluetooth hardware is assumed."""
from datetime import datetime
import json
import os
from pathlib import Path
import subprocess

if os.name != 'nt':
    raise SystemExit('This smoke check requires Windows.')

executable = Path('artifacts/native/win-x64/CookerPods-Diagnostic.exe').resolve()
reports = Path(os.environ['LOCALAPPDATA']) / 'CookerPods' / 'reports'
before = set(reports.glob('native-report-*.json'))
result = subprocess.run([str(executable), '--seconds', '10', '--no-wait'],
    capture_output=True, text=True, encoding='utf-8', timeout=60)
if result.returncode != 0:
    raise SystemExit(f'Diagnostic exited {result.returncode}\n{result.stdout}\n{result.stderr}')
created = set(reports.glob('native-report-*.json')) - before
assert len(created) == 1, 'Expected exactly one new report from this invocation'
report = json.loads(created.pop().read_text(encoding='utf-8'))
assert report['schemaVersion'] == 2
assert report['probeVersion'] == '0.4.2-native-preview'
assert report['requestedScanSeconds'] == 10 and report['finishedAtUtc']
assert report['metadataCompletion'] in ('Completed', 'DeadlineElapsed')
assert report['metadataSeconds'] is not None and report['metadataSeconds'] < 25
if report['scanStarted']:
    delay = (datetime.fromisoformat(report['scanStartedAtUtc'])
             - datetime.fromisoformat(report['startedAtUtc'])).total_seconds()
    assert 0 <= delay < 10, 'Broadcast capture must start before slow metadata enumeration'
assert isinstance(report['issues'], list)
print('Packaged Windows diagnostic completed and wrote a current, bounded report.')
print('Reported issue stages:', [issue['stage'] for issue in report['issues']])
print('No Bluetooth hardware, device identity, or telemetry correctness is established by this smoke check.')
