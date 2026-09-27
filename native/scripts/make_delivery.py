"""Bundle the full installer, public source and local commit tools; never commits/pushes."""
from pathlib import Path
import hashlib, zipfile, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2];release=root/'native/artifacts/releases'
identity=ET.parse(root/'native/Widget/Package.appxmanifest').getroot().find('{http://schemas.microsoft.com/appx/manifest/foundation/windows10}Identity')
version='.'.join(identity.get('Version').split('.')[:3]);prefix=f'ReticleLab-{version}'
installer=release/(prefix+'-windows-x64.zip');source=release/(prefix+'-source.zip')
assert installer.is_file() and source.is_file(),'Create and validate the full installer and source first.'
commit=release/(prefix+'-commit-tools.zip');delivery=release/(prefix+'-delivery.zip')
if commit.exists() or delivery.exists():raise SystemExit('Delivery output exists; no prior archive was overwritten.')
files=['Commit-Source.cmd','native/scripts/Commit-Public-Source.cmd','native/scripts/Commit-Public-Source.ps1','native/scripts/public_audit.py']
with zipfile.ZipFile(commit,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for name in files:z.write(root/name,name)
    z.writestr('README-COMMIT.txt','将本包解压到源码根目录，运行 Commit-Source.cmd。需要 Git 和 Python 3。\n源码包已包含相同脚本，无需重复解压。仅创建匿名署名的本地 commit，不推送。\n')
artifacts=[installer,source,commit]
checksums=''.join(f'{hashlib.sha256(f.read_bytes()).hexdigest()}  {f.name}\n' for f in artifacts)
for f in artifacts:
    f.with_suffix('.zip.sha256').write_text(hashlib.sha256(f.read_bytes()).hexdigest()+'  '+f.name+'\n',encoding='ascii')
with zipfile.ZipFile(delivery,'w',zipfile.ZIP_STORED) as z:
    for f in artifacts:z.write(f,f.name)
    z.writestr('SHA256SUMS.txt',checksums)
    z.writestr('START-HERE.txt',f'''Reticle Lab {version} 完整交付包

1. 安装：完整解压 {installer.name}，运行 Install-ReticleLab.cmd。
   含应用运行时、x64 依赖和公开开发证书；首次信任证书可能需要 UAC。
2. 源码：解压 {source.name}，阅读 README.zh-CN.md。
3. 提交：源码目录中运行 Commit-Source.cmd，需要 Git 与 Python 3。
   {commit.name} 是同一套脚本的独立备份，请解压到源码根目录使用。
   署名 Reticle Lab Contributors <contributors@reticle-lab.invalid>。
   只创建本地提交，不配置远程仓库、不推送；本交付包不含 Git 历史。

项目代码 MIT。第三方依赖与字体遵循各自许可证。
未包含私钥、GSI token、个人运行配置和日志；厂商未输出的伤害无法补算。
已做自动化构建/包校验；没有 VAC 认证或零封禁保证。
''')
delivery.with_suffix('.zip.sha256').write_text(hashlib.sha256(delivery.read_bytes()).hexdigest()+'  '+delivery.name+'\n',encoding='ascii')
print(commit);print(delivery)
