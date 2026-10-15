#!/usr/bin/env python3
"""Run real WPF controls on Windows using explicit synthetic fixtures."""
import json
import os
from pathlib import Path
import subprocess
import sys

if os.name != 'nt':
    raise SystemExit('The WPF smoke check requires Windows.')
output = Path('artifacts/native-ui')
output.mkdir(parents=True, exist_ok=True)
result = subprocess.run([str(Path('artifacts/native/win-x64/CookerPods.exe').resolve()), '--ui-smoke', str(output.resolve())],
    capture_output=True, text=True, encoding='utf-8', timeout=60)
report_path = output / 'ui-smoke-result.json'
report = json.loads(report_path.read_text()) if report_path.exists() else {'success': False, 'error': 'No UI result file', 'stderr': result.stderr}
summary = json.dumps(report, indent=2)
if os.environ.get('GITHUB_STEP_SUMMARY'):
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as file:
        file.write('## Windows UI smoke (synthetic fixtures)\n\n```json\n' + summary + '\n```\n')
print(summary)
assert result.returncode == 0 and report.get('success'), result.stderr
assert len(report['captures']) == 8
for capture in report['captures']:
    data = (output / capture['filename']).read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n' and len(data) > 5000
print('Actual WPF client captures and selection/pause/resume/change controls verified with synthetic data.')
