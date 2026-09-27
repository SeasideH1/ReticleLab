"""Package an existing signed MSIX as an update, without source or framework installers."""
from pathlib import Path
import argparse, hashlib, json, shutil, zipfile, xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('--signed-dir',type=Path,required=True);args=p.parse_args()
root=Path(__file__).resolve().parents[2];native=root/'native'
packages=list(args.signed_dir.glob('ReticleLab-*-x64-dev-signed.msix'))
assert len(packages)==1,'Expected one signed MSIX'
package=packages[0]
with zipfile.ZipFile(package) as z:
    manifest=ET.fromstring(z.read('AppxManifest.xml'))
version=manifest.find('{http://schemas.microsoft.com/appx/manifest/foundation/windows10}Identity').get('Version')
short='.'.join(version.split('.')[:3]);name=f'ReticleLab-{short}-update-x64'
out=native/'artifacts/releases'/name
if out.exists():raise SystemExit('Output exists; no existing update was overwritten.')
out.mkdir(parents=True)
for file in [package.name,'ReticleLab.Local.cer','signing-info.json']:shutil.copy2(args.signed_dir/file,out/file)
for file in ['Update-ReticleLab.cmd','install-dev.ps1']:shutil.copy2(native/'scripts'/file,out/file)
shutil.copy2(root/'docs'/f'RELEASE-{short}.md',out/'CHANGES.md')
shutil.copy2(root/'LICENSE',out/'LICENSE')
(out/'START-HERE.txt').write_text(f'''Reticle Lab {short} update / Windows x64

完整解压后运行 Update-ReticleLab.cmd。仅适用于当前账户已经安装 0.2.7 或更新版本的电脑。
Windows 会结束本应用的已有控件/接收器后更新；保留设置、布局与 GSI token。
更新后 Win+G 重新打开控件。伤害仍缺失时，请在设置 > GSI > 检查伤害数据查看原因。
本包不安装证书、不附带依赖安装包，也不包含源码压缩包。
这是签名 MSIX 更新包，不是二进制差分补丁；MSIX 内仍含应用运行时。

Extract everything, then run Update-ReticleLab.cmd. Requires an existing working
Reticle Lab 0.2.7+ installation and its trusted certificate/framework. No downgrade.
Settings are retained. Read CHANGES.md for scope and unverified in-game behavior.
''',encoding='utf-8-sig')
(out/'SHA256SUMS.txt').write_text(''.join(f'{hashlib.sha256(f.read_bytes()).hexdigest()}  {f.name}\n' for f in sorted(out.iterdir()) if f.is_file()),encoding='ascii')
archive=out.with_name(out.name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for f in sorted(out.iterdir()):z.write(f,name+'/'+f.name)
archive.with_suffix('.zip.sha256').write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+'  '+archive.name+'\n',encoding='ascii')
print(archive)
