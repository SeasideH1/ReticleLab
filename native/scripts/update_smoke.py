"""Validate a relocated update bundle and reject corruption without deploying anything."""
from pathlib import Path
import os,subprocess,tempfile,zipfile
root=Path(__file__).resolve().parents[1]
archive=root/'artifacts/releases/ReticleLab-0.2.10-update-x64.zip'
work=Path(tempfile.mkdtemp(prefix='update validation with spaces ',dir=root/'artifacts'))
with zipfile.ZipFile(archive) as z:
    assert not any('Dependencies/' in n or n.endswith('.pfx') or n.endswith('-source.zip') for n in z.namelist())
    z.extractall(work)
bundle=work/'ReticleLab-0.2.10-update-x64'
shell=Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/powershell.exe'
def run(folder,expected,phrase):
    result=subprocess.run([str(shell),'-NoProfile','-ExecutionPolicy','Bypass','-File',str(bundle/'install-dev.ps1'),'-PackageDirectory',str(folder),'-UpdateOnly','-ValidateOnly'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=45)
    output=result.stdout.decode(errors='replace')
    assert result.returncode==expected and phrase in output,output
run(bundle,0,'PASS: package hash')
bad=work/'corrupt';bad.mkdir();(bad/'ReticleLab-0.2.10-x64-dev-signed.msix').write_bytes(b'invalid fixture')
run(bad,1,'File hash mismatch')
missing=work/'missing certificate';missing.mkdir()
os.link(bundle/'ReticleLab-0.2.10-x64-dev-signed.msix',missing/'ReticleLab-0.2.10-x64-dev-signed.msix')
run(missing,1,'Exception')
print('PASS: relocated update validates without bundled dependencies; corrupt package and missing certificate rejected. No deployment/trust mutation.')
