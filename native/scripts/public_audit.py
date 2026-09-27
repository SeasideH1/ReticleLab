"""Inspect Git index or nested ZIP/MSIX payloads without printing matched secrets."""
from pathlib import Path
import argparse, io, re, subprocess, zipfile

parser=argparse.ArgumentParser()
parser.add_argument('--staged',action='store_true')
parser.add_argument('--worktree',action='store_true')
parser.add_argument('--archive',type=Path)
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
bad=[];count=0
patterns=[rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
          rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-[A-Za-z0-9]{32,})\b',
          rb'\b7656119[0-9]{10}\b',rb'[A-Za-z]:[\\/]+Users[\\/]+(?!Public\b|Default\b)[^\\/\s<>"\x00]+',
          re.escape(str(root).encode()),re.escape(root.as_posix().encode()),rb'codex-clipboard-[0-9a-f-]+']
# This optional local comparison is never packaged; only the token bytes are compared.
import os
local=Path(os.environ.get('LOCALAPPDATA',''))/'Packages/ReticleLab.GameBar_vb9p3rafwvymj/LocalState/gsi-token.txt'
known=local.read_bytes().strip() if local.is_file() else b''

def inspect(name,data):
 global count
 count+=1
 normalized=name.replace('\\','/');parts=normalized.lower().split('/')
 if any(p in {'.signing','.private','upstream','localstate','install-logs','__pycache__'} for p in parts) or normalized.lower().endswith(('.pfx','.p12','.dpapi','.log','.pdb')):
  bad.append((name,'private file category'))
 if parts[-1] in {'gsi-token.txt','snapshot.json','mouse-state.json','preferences.json','gamestate_integration_reticlelab.cfg','nuget.local.config'}:bad.append((name,'machine state/config'))
 for data_view in (data,data.replace(b'\x00',b'')):
  if any(re.search(pattern,data_view) for pattern in patterns):bad.append((name,'identifier/path/credential pattern'));break
  if len(known)>=32 and known in data_view:bad.append((name,'local runtime secret'));break
 if normalized.lower().endswith(('.zip','.msix','.appx')):
  with zipfile.ZipFile(io.BytesIO(data)) as z:
   for entry in z.infolist():
    if not entry.is_dir():inspect(name+'!/'+entry.filename,z.read(entry))

if args.staged:
 names=subprocess.check_output(['git','ls-files','--cached','-z'],cwd=root).decode().split('\0')
 for name in filter(None,names):inspect(name,subprocess.check_output(['git','show',':'+name],cwd=root))
if args.worktree:
 names=subprocess.check_output(['git','-c','core.excludesFile=','ls-files','--cached','--others','--exclude-standard','-z'],cwd=root).decode().split('\0')
 for name in sorted(set(filter(None,names))):inspect(name,(root/name).read_bytes())
if args.archive:inspect(args.archive.name,args.archive.read_bytes())
if not(args.staged or args.worktree or args.archive):parser.error('Choose --staged, --worktree or --archive')
if count==0:raise SystemExit('FAIL: no entries inspected; an empty index is not a successful audit.')
for name,reason in sorted(set(bad)):print('FAIL:',name,reason)
if bad:raise SystemExit(1)
print(f'PASS: {count} entries checked; no matched private state, local identifiers, known runtime token or key material. Pattern audit is not a proof of absence.')
