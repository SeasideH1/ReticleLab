"""Check the actual staged Game Bar contracts, including failure regressions."""
from pathlib import Path
import os
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
package = Path(sys.argv[1]).resolve()
sdk = root / '.packages/microsoft.gaming.xboxgamebar/7.3.2607010'
smoke = root / 'Smoke/bin/Debug/net10.0/Smoke.dll'

def check(folder, expected):
    result = subprocess.run(['dotnet', str(smoke), '--audit-gamebar', str(folder), str(sdk)],
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=30)
    assert (result.returncode == 0) == expected, result.stdout.decode(errors='replace')

check(package, True)
with tempfile.TemporaryDirectory(prefix='gamebar-regression-', dir=root / 'artifacts') as work:
    work = Path(work)
    source_manifest = (package / 'AppxManifest.xml').read_text(encoding='utf-8-sig')
    (work / 'AppxManifest.xml').write_text(source_manifest, encoding='utf-8')
    check(work, False)  # Old packaging omitted both WinMDs.
    for name in ('Microsoft.Gaming.XboxGameBar.winmd', 'Microsoft.Gaming.XboxGameBar.Private.winmd'):
        os.link(package / name, work / name)
    check(work, True)
    invalid_manifest = source_manifest.replace('FD037AC2-5D3A-438D-BD53-88F0B6B0F399', '00000000-0000-0000-0000-000000000001')
    assert invalid_manifest != source_manifest
    (work / 'AppxManifest.xml').write_text(invalid_manifest, encoding='utf-8')
    check(work, False)
print('PASS: real SDK registrations accepted; missing WinMDs and wrong activation IID rejected. No app activation performed.')
