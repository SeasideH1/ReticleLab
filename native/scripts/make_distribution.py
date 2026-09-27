"""Allowlisted relocatable installer bundle. Does not sign, install or publish."""
from pathlib import Path
import argparse, hashlib, json, shutil, zipfile, xml.etree.ElementTree as ET

p=argparse.ArgumentParser();p.add_argument('--signed-dir',type=Path,required=True);p.add_argument('--dependency',type=Path,required=True);args=p.parse_args()
root=Path(__file__).resolve().parents[2];native=root/'native'
packages=list(args.signed_dir.glob('ReticleLab-*-x64-dev-signed.msix'))
if len(packages)!=1:raise SystemExit('Expected exactly one signed Reticle MSIX.')
package=packages[0]
with zipfile.ZipFile(package) as z:
 manifest=ET.fromstring(z.read('AppxManifest.xml'));version=manifest.find('{http://schemas.microsoft.com/appx/manifest/foundation/windows10}Identity').get('Version')
 short='.'.join(version.split('.')[:3]);title=f'ReticleLab-{short}-windows-x64'
 out=native/'artifacts/releases'/title
 if out.exists():raise SystemExit('Output exists; use a new release version or review the existing bundle. No files overwritten.')
 out.mkdir(parents=True)
 (out/'LICENSES').mkdir()
 for entry in z.namelist():
  filename=Path(entry.replace('\\','/')).name
  if filename.lower() in ('gamebar-license.txt','license.txt','thirdpartynotices.txt'):
   (out/'LICENSES'/entry.replace('\\','-').replace('/','-')).write_bytes(z.read(entry))
for name in [package.name,'ReticleLab.Local.cer','signing-info.json']:shutil.copy2(args.signed_dir/name,out/name)
dep=out/'Dependencies/x64/Microsoft.VCLibs.x64.14.00.appx';dep.parent.mkdir(parents=True);shutil.copy2(args.dependency,dep)
scripts=['Install-ReticleLab.cmd','install-dev.ps1','Diagnose-ReticleLab.cmd','Setup-GSI.cmd','Setup-GSI.ps1','SteamPaths.ps1','Update-GSI-Latency.cmd','Update-GSI-Latency.ps1']
for name in scripts:shutil.copy2(native/'scripts'/name,out/name)
assert package.name in (out/'install-dev.ps1').read_text(encoding='utf-8-sig'),'Update the installer pins before bundling'
for source,dest in [('LICENSE','LICENSE'),('docs/INSTALL.md','INSTALL.md'),('docs/ANTI-CHEAT-REVIEW.md','ANTI-CHEAT-REVIEW.md'),('SECURITY.md','SECURITY.md'),('native/THIRD-PARTY.md','THIRD-PARTY.md'),('native/Widget/Fonts/OFL.txt','LICENSES/OFL.txt'),('native/Widget/Fonts/FONT-NOTICES.txt','LICENSES/FONT-NOTICES.txt')]:shutil.copy2(root/source,out/dest)
(out/'START-HERE.txt').write_text('Reticle Lab '+short+''' / Windows x64

1. Extract the WHOLE ZIP into a writable folder.
2. Run Install-ReticleLab.cmd. Review component licenses in LICENSES/.
3. Install/enable Xbox Game Bar, press Win+G and open/pin the widgets.
4. For CS2: start receiver in Reticle settings > GSI on THIS computer.
5. Run Setup-GSI.cmd, then restart CS2. No developer token is distributed.

The app/runtime/dependency/public certificate are included. Private keys, local
settings, game cfg, logs and personal data are excluded. Game Bar is required
separately. Windows may request administrator approval for certificate trust.
No VAC approval or ban-free guarantee. Read ANTI-CHEAT-REVIEW.md and INSTALL.md.

中文：完整解压后双击 Install-ReticleLab.cmd。首次信任证书可能需要管理员确认。
Win+G 打开并固定控件。需要 CS2 数据时先在本机 GSI 页启动接收，再运行
Setup-GSI.cmd 并重启 CS2。不会复制开发者 token。没有 VAC 零风险保证。
''',encoding='utf-8-sig')
manifest={'version':version,'target':'Windows x64 build 19041+','package':package.name,'contains_private_key':False,'contains_runtime_state':False,'second_pc_install_tested':False,'files':{}}
for file in sorted(out.rglob('*')):
 if file.is_file():manifest['files'][file.relative_to(out).as_posix()]=hashlib.sha256(file.read_bytes()).hexdigest()
(out/'distribution.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(out/'SHA256SUMS.txt').write_text(''.join(f'{hashlib.sha256(f.read_bytes()).hexdigest()}  {f.relative_to(out).as_posix()}\n' for f in sorted(out.rglob('*')) if f.is_file() and f.name!='SHA256SUMS.txt'),encoding='utf-8')
archive=out.parent/(out.name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for file in sorted(out.rglob('*')):
  if file.is_file():z.write(file,title+'/'+file.relative_to(out).as_posix())
archive.with_suffix('.zip.sha256').write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+'  '+archive.name+'\n',encoding='ascii')
print(archive)
