"""Exercise the real PS5.1 installer on fixtures; no installed game or token is modified."""
from pathlib import Path
import os, subprocess, tempfile
root=Path(__file__).resolve().parents[1]
shell=Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/powershell.exe'
with tempfile.TemporaryDirectory(prefix='gsi-setup-',dir=root/'artifacts') as tmp:
 work=Path(tmp);game=work/'CS2 with spaces';cfg=game/'game/csgo/cfg';cfg.mkdir(parents=True)
 source=work/'source.cfg'
 text='"Reticle Lab"\n{\n "uri" "http://127.0.0.1:29841/gsi"\n "auth" { "token" "'+('A'*64)+'" }\n "data" {\n "provider" "1"\n "player_state" "1"\n }\n}\n'
 source.write_text(text)
 def run(code):
  result=subprocess.run([str(shell),'-NoProfile','-ExecutionPolicy','Bypass','-File',str(root/'scripts/Setup-GSI.ps1'),'-Cs2Directory',str(game),'-SourceConfig',str(source)],capture_output=True,timeout=15)
  assert result.returncode==code,result.stdout.decode(errors='replace')+result.stderr.decode(errors='replace')
 run(0);target=cfg/'gamestate_integration_reticlelab.cfg';assert target.read_bytes()==source.read_bytes()
 run(0);assert not list(cfg.glob('*.bak'))
 target.write_text('old local config');run(0);assert list(cfg.glob('*.bak'))[0].read_text()=='old local config'
 saved=target.read_bytes();source.write_text(text+'bind "mouse1" "attack"\n');run(1);assert target.read_bytes()==saved
 source.write_text(text.replace('127.0.0.1','example.invalid'));run(1);assert target.read_bytes()==saved
 print('PASS: fresh machine cfg install, spaces in path, idempotence, backup, command/remote URI rejection. No real game files changed.')
