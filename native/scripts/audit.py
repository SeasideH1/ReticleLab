"""Project-specific structural checks; not an anti-cheat certification."""
from pathlib import Path
import argparse, json, re, xml.etree.ElementTree as ET, zipfile, hashlib
parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path);args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
for folder in ('Widget','Core','Bridge'):
 for path in (root/folder).glob('*.cs'):
  source=path.read_text(encoding='utf-8')
  forbidden=r'\b(OpenProcess|ReadProcessMemory|WriteProcessMemory|SetWindowsHookEx|GetAsyncKeyState|SendInput|CreateRemoteThread|VirtualAllocEx)\s*\('
  assert not re.search(forbidden,source),f'Forbidden API in {path}'
  assert 'MurbongCrosshair' not in source
  if 'RegisterRawInputDevices(' in source:
   assert path==root/'Bridge/RawMouse.cs','Raw Input confined to the user-approved mouse receiver'
   assert 'Page=1,Usage=2,Flags=0x100' in source,'Mouse-only INPUTSINK registration'
   assert 'Page=1,Usage=2,Flags=1,Target=IntPtr.Zero' in source,'Explicit registration cleanup'
manifest=ET.parse(root/'Widget/Package.appxmanifest')
ns={'f':'http://schemas.microsoft.com/appx/manifest/foundation/windows10','u3':'http://schemas.microsoft.com/appx/manifest/uap/windows10/3'}
ids=[e.attrib['Id'] for e in manifest.findall('.//u3:AppExtension',ns)]
assert sorted(ids)==sorted(['Crosshair','Feed','Stats','Input','Settings'])
identity=manifest.find('f:Identity',ns).attrib
assert identity['Name']=='ReticleLab.GameBar'
assert 'runFullTrust' in (root/'Widget/Package.appxmanifest').read_text(encoding='utf-8')
for file in (root/'Widget/Fonts').glob('*.ttf'):
 assert file.read_bytes()[:4] in (b'\0\1\0\0',b'OTTO'),file
if args.package:
 with zipfile.ZipFile(args.package) as z:
  names={n.replace('\\','/') for n in z.namelist()}
  for name in ['AppxManifest.xml','ReticleWidget.exe','ReticleWidget.dll','Bridge/ReticleBridge.exe','Microsoft.Gaming.XboxGameBar.dll','Microsoft.Gaming.XboxGameBar.winmd','Microsoft.Gaming.XboxGameBar.Private.winmd','Microsoft.Windows.UI.Xaml.dll','resources.pri','Fonts/notosanssc-regular.ttf','Fonts/OFL.txt','GAMEBAR-LICENSE.txt']:
   assert name in names,'Missing payload '+name
  packed_manifest=ET.fromstring(z.read('AppxManifest.xml'))
  interfaces=packed_manifest.findall('./f:Extensions/f:Extension/f:ProxyStub/f:Interface',ns)
  registrations={e.attrib['Name']:e.attrib['InterfaceId'].upper() for e in interfaces}
  assert len(interfaces)==len(registrations)==62,'Incomplete Game Bar 7.3.2607010 interface registration'
  assert len(set(registrations.values()))==62,'Duplicate interface GUID'
  assert registrations.get('Microsoft.Gaming.XboxGameBar.IXboxGameBarWidgetActivatedEventArgs')=='FD037AC2-5D3A-438D-BD53-88F0B6B0F399'
  assert registrations.get('Microsoft.Gaming.XboxGameBar.Private.IXboxGameBarWidgetNotificationHost')=='6F68D392-E4A9-46F7-A024-5275BC2FE7BA'
  assert not any('murbong' in n.lower() or n.lower().endswith('.uifont') for n in names)
 print('Package sha256:',hashlib.sha256(args.package.read_bytes()).hexdigest())
print('Source API audit, independent identity, 4 widgets + settings, and font/payload checks passed.')
