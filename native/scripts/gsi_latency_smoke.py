"""Run the cfg updater against disposable fixtures; never touches the installed game."""
from pathlib import Path
import os, subprocess, tempfile
root=Path(__file__).resolve().parents[1]
state=Path(tempfile.mkdtemp(prefix='cfg-latency-',dir=root/'artifacts'))
cfg=state/'gamestate_integration_reticlelab.cfg'
original='"Reticle Lab"\r\n{\r\n  "uri" "http://127.0.0.1:29841/gsi"\r\n  "buffer" "0.1"\r\n  "throttle" "0.1"\r\n  "auth" { "token" "fixture-private-value" }\r\n}\r\n'
cfg.write_bytes(original.encode())
shell=Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/powershell.exe'
def run(expected):
    p=subprocess.run([str(shell),'-NoProfile','-ExecutionPolicy','Bypass','-File',str(root/'scripts/Update-GSI-Latency.ps1'),'-ConfigPath',str(cfg)],capture_output=True,timeout=15)
    assert p.returncode==expected,p.stderr.decode(errors='replace')
    assert b'fixture-private-value' not in p.stdout+p.stderr,'token exposed'
run(0)
updated=cfg.read_bytes()
assert updated==original.replace('"buffer" "0.1"','"buffer" "0.0"').replace('"throttle" "0.1"','"throttle" "0.03"').encode()
backups=list(state.glob('*.bak'));assert len(backups)==1 and backups[0].read_bytes()==original.encode()
run(0);assert cfg.read_bytes()==updated and len(list(state.glob('*.bak')))==1
cfg.write_bytes(updated.replace(b'29841/gsi',b'29842/gsi'));bad=cfg.read_bytes();run(1);assert cfg.read_bytes()==bad
print('PASS: only timing values change; token/URI preserved; backup exact; idempotent; unexpected endpoint rejected. No game files changed.')
