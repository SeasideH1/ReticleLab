"""Export public candidates, excluding Git metadata and ignored local evidence."""
from pathlib import Path
import hashlib,subprocess,zipfile,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
subprocess.run(['python',str(root/'native/scripts/public_audit.py'),'--worktree'],check=True,cwd=root)
names=subprocess.check_output(['git','-c','core.excludesFile=','ls-files','--cached','--others','--exclude-standard','-z'],cwd=root).decode().split('\0')
names=sorted(set(filter(None,names)))
manifest=ET.parse(root/'native/Widget/Package.appxmanifest').getroot()
version=manifest.find('{http://schemas.microsoft.com/appx/manifest/foundation/windows10}Identity').get('Version')
title='ReticleLab-'+'.'.join(version.split('.')[:3])+'-source'
out=root/'native/artifacts/releases'/(title+'.zip');out.parent.mkdir(parents=True,exist_ok=True)
if out.exists():raise SystemExit('Output exists; no prior source archive was overwritten.')
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for name in names:
  file=(root/name).resolve();file.relative_to(root)
  z.write(file,title+'/'+name)
out.with_suffix('.zip.sha256').write_text(hashlib.sha256(out.read_bytes()).hexdigest()+'  '+out.name+'\n',encoding='ascii')
print(f'{out} ({len(names)} source files; no Git metadata)')
