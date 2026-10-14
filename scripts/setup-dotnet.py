#!/usr/bin/env python3
"""Install the pinned SDK in a writable cloud tools directory, verifying SHA-512."""
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import tarfile
import tempfile
from urllib.request import urlopen

project = Path(__file__).resolve().parent.parent
version = json.loads((project / 'global.json').read_text())['sdk']['version']
target = Path(os.environ.get('COOKERPODS_SDK_DIR', '/workspace/.tools/dotnet'))
if platform.system() != 'Linux':
    raise SystemExit('This cloud helper is for Linux. Windows developers can use the official .NET SDK installer; end users need no SDK.')
rid = {'x86_64': 'linux-x64', 'aarch64': 'linux-arm64'}.get(platform.machine())
if not rid:
    raise SystemExit('Unsupported cloud SDK architecture')
cli_env = {**os.environ, 'DOTNET_CLI_HOME': '/workspace/.cache/dotnet', 'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'DOTNET_NOLOGO': '1'}
if (target / 'dotnet').exists():
    installed = subprocess.run([str(target / 'dotnet'), '--list-sdks'], capture_output=True, text=True, env=cli_env, check=True)
    if any(line.split()[0] == version for line in installed.stdout.splitlines()):
        print(f'.NET SDK {version} already installed at {target}')
        raise SystemExit(0)
metadata = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
with urlopen(metadata, timeout=60) as response:
    releases = json.load(response)
artifact = None
for release in releases['releases']:
    for sdk in release.get('sdks', [release.get('sdk', {})]):
        if sdk.get('version') == version:
            artifact = next((f for f in sdk['files'] if f.get('rid') == rid and f['name'].endswith('.tar.gz')), None)
if not artifact or len(artifact.get('hash', '')) != 128:
    raise SystemExit('Official metadata did not supply the pinned SDK and SHA-512')
target.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix='cookerpods-sdk-', dir='/tmp') as temporary:
    archive = Path(temporary) / 'sdk.tar.gz'
    digest = hashlib.sha512()
    with urlopen(artifact['url'], timeout=120) as response, archive.open('wb') as output:
        while chunk := response.read(1024 * 1024):
            digest.update(chunk)
            output.write(chunk)
    if digest.hexdigest().lower() != artifact['hash'].lower():
        raise SystemExit('SDK checksum mismatch; refusing to extract or execute it')
    with tarfile.open(archive) as sdk_archive:
        sdk_archive.extractall(target, filter='data')
print(f'Installed verified .NET SDK {version} at {target}')
