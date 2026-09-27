"""Exercise installer validation/failure paths without elevation or installation."""
from pathlib import Path
import os
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
release = root / 'artifacts/releases/ReticleLab-0.2.10-windows-x64'
script = root / 'scripts/install-dev.ps1'
shell = Path(os.environ['WINDIR']) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
package_name = 'ReticleLab-0.2.10-x64-dev-signed.msix'

def run(folder, expected, phrase):
    result = subprocess.run([str(shell), '-NoProfile', '-ExecutionPolicy', 'Bypass',
                             '-File', str(script), '-PackageDirectory', str(folder), '-ValidateOnly'],
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=45)
    output = result.stdout.decode(errors='replace')
    assert result.returncode == expected, output
    assert phrase in output, output
    assert list((folder / 'install-logs').glob('*.log')), 'Missing diagnostic log'

run(release, 0, 'PASS: package hash')
for answer in ('Q\n', '\nwrong\n\n'):
    result = subprocess.run([str(shell), '-NoProfile', '-ExecutionPolicy', 'Bypass',
                             '-File', str(script), '-PackageDirectory', str(release)],
                            input=answer.encode(), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=45)
    output = result.stdout.decode(errors='replace')
    assert result.returncode == 2 and 'Installation cancelled.' in output, output
    assert 'RuntimeException' not in output and 'Certificate imported' not in output, output
with tempfile.TemporaryDirectory(prefix='installer-smoke-', dir=root / 'artifacts') as work:
    work = Path(work)
    corrupt = work / 'corrupt'; corrupt.mkdir()
    (corrupt / package_name).write_bytes(b'intentionally invalid package')
    run(corrupt, 1, 'File hash mismatch')
    missing = work / 'missing-dependency'; missing.mkdir()
    # Read-only hard link avoids copying a 110 MB package; the installer only reads it.
    os.link(release / package_name, missing / package_name)
    run(missing, 1, 'Microsoft.VCLibs.x64.14.00.appx')
    wrong_cert = work / 'wrong-certificate'; wrong_cert.mkdir()
    os.link(release / package_name, wrong_cert / package_name)
    (wrong_cert / 'Dependencies/x64').mkdir(parents=True)
    os.link(release / 'Dependencies/x64/Microsoft.VCLibs.x64.14.00.appx', wrong_cert / 'Dependencies/x64/Microsoft.VCLibs.x64.14.00.appx')
    (wrong_cert / 'ReticleLab.Local.cer').write_bytes(b'intentionally invalid certificate')
    run(wrong_cert, 1, 'Exception')
print('PASS: valid release; corrupt package rejected; missing dependency rejected; invalid certificate rejected; logs preserved per run. No installation or trust mutation.')
