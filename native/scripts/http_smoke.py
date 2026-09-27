"""Starts the real bridge on a loopback test port, tests HTTP, and stops only its child."""
from pathlib import Path
import json, os, subprocess, tempfile, time, urllib.request, urllib.error
root=Path(__file__).resolve().parents[1]
run=root/'artifacts'/'http-smoke';run.mkdir(parents=True,exist_ok=True)
state=Path(tempfile.mkdtemp(prefix='state-',dir=run))
import sys
bridge=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'Bridge/bin/Debug/net10.0/ReticleBridge.dll'
env=os.environ.copy();env['DOTNET_CLI_HOME']=str(root/'.dotnet')
with (state/'process.log').open('w',encoding='utf-8') as log:
 process=subprocess.Popen(['dotnet',str(bridge),'--state-dir',str(state),'--port','29843'],stdout=log,stderr=log,env=env,creationflags=0x08000000)
 try:
  for _ in range(100):
   if process.poll() is not None: raise RuntimeError('Bridge exited: '+(state/'process.log').read_text())
   try:
    urllib.request.urlopen('http://127.0.0.1:29843/health',timeout=.5).read();break
   except (OSError,urllib.error.URLError):time.sleep(.1)
  else:raise RuntimeError('Bridge did not start')
  token=(state/'gsi-token.txt').read_text().strip()
  duplicate=subprocess.run(['dotnet',str(bridge),'--state-dir',str(state),'--port','29843'],stdout=log,stderr=log,env=env,creationflags=0x08000000,timeout=10)
  assert duplicate.returncode==0,'duplicate helper must exit gracefully'
  assert process.poll() is None,'original helper must remain running'
  assert (state/'gsi-token.txt').read_text().strip()==token,'duplicate must not rotate token'
  for _ in range(30):
   if (state/'bridge-status.json').exists():break
   time.sleep(.1)
  assert json.loads((state/'bridge-status.json').read_text())['Status']=='running','receiver heartbeat'
  conflict_state=Path(tempfile.mkdtemp(prefix='port-conflict-',dir=run))
  conflict=subprocess.run(['dotnet',str(bridge),'--state-dir',str(conflict_state),'--port','29843'],stdout=log,stderr=log,env=env,creationflags=0x08000000,timeout=10)
  assert conflict.returncode==1 and (conflict_state/'bridge-error.txt').exists(),'bind failure must produce a diagnostic and controlled exit'
  def post(body):
   raw=body if isinstance(body,bytes) else json.dumps(body).encode()
   request=urllib.request.Request('http://127.0.0.1:29843/gsi',data=raw,headers={'Content-Type':'application/json'})
   try:return urllib.request.urlopen(request,timeout=5).status
   except urllib.error.HTTPError as e:return e.code
  def payload(kills=0):return {'auth':{'token':token},'provider':{'appid':730,'steamid':'local-test','timestamp':int(time.time())},'map':{'name':'de_test','round':1},'round':{'phase':'live'},'player':{'steamid':'local-test','activity':'playing','state':{'round_kills':kills,'round_killhs':0,'money':3000},'match_stats':{'kills':kills,'deaths':0,'assists':0},'weapons':{'weapon_0':{'name':'weapon_m4a1','state':'active'}}}}
  assert post(b'{')==400,'malformed JSON'
  bad=payload();bad['auth']['token']='wrong';assert post(bad)==403,'authentication'
  assert post(payload())==204,'baseline'
  assert post(payload(1))==204,'kill'
  snapshot=json.loads((state/'snapshot.json').read_text())
  assert snapshot['IsSelf'] and len(snapshot['Events'])==1
  assert snapshot['Events'][0]['WeaponSource']=='当前手持' and snapshot['Events'][0]['Damage'] is None
  assert post(payload(1))==204
  assert len(json.loads((state/'snapshot.json').read_text())['Events'])==1,'dedup'
  community=payload(1);community['map']['name']='5e_1v1_inferno';community['player']['state']['round_kills']=0
  assert post(community)==204
  community['player']['match_stats']['kills']=2
  assert post(community)==204
  snap=json.loads((state/'snapshot.json').read_text())
  assert snap['RoundKills']==0 and snap['Kills']==2 and len(snap['Events'])==1,'community match delta'
  community['map']['round']=2;community['round']['phase']='freezetime'
  assert post(community)==204
  assert len(json.loads((state/'snapshot.json').read_text())['Events'])==1,'round transition retains last kill'
  # Exercise the real receiver registration and visible-widget lease without generating input.
  (state/'preferences.json').write_text(json.dumps({'BackgroundMouse':True}))
  (state/'mouse-request.txt').write_text('visible')
  def wait_mouse(enabled):
   for _ in range(50):
    try:
     current=json.loads((state/'mouse-state.json').read_text())
     if current['Enabled']==enabled:return current
    except (OSError,json.JSONDecodeError):pass
    time.sleep(.1)
   raise AssertionError('mouse state did not become '+str(enabled))
  assert not wait_mouse(True)['Error'],'real Raw Input registration'
  # Windows replacement sharing failure must not permanently terminate the mouse publisher.
  import ctypes
  from ctypes import wintypes
  kernel=ctypes.WinDLL('kernel32',use_last_error=True)
  kernel.CreateFileW.argtypes=[wintypes.LPCWSTR,wintypes.DWORD,wintypes.DWORD,ctypes.c_void_p,wintypes.DWORD,wintypes.DWORD,wintypes.HANDLE]
  kernel.CreateFileW.restype=wintypes.HANDLE
  kernel.CloseHandle.argtypes=[wintypes.HANDLE]
  locked=kernel.CreateFileW(str(state/'mouse-state.json'),0x80000000,1,None,3,0,None)
  assert locked not in (None,ctypes.c_void_p(-1).value),'open restrictive fixture reader'
  try:time.sleep(.4)
  finally:kernel.CloseHandle(locked)
  (state/'mouse-request.txt').write_text('visible')
  released_at=int(time.time()*1000)
  for _ in range(40):
   resumed=wait_mouse(True)
   if resumed['At']>=released_at:break
   time.sleep(.05)
  else:raise AssertionError('mouse publisher permanently stopped after a sharing violation')
  (state/'preferences.json').write_text(json.dumps({'BackgroundMouse':False}))
  wait_mouse(False)
  (state/'preferences.json').write_text(json.dumps({'BackgroundMouse':True}))
  (state/'mouse-request.txt').write_text('visible')
  wait_mouse(True)
  os.utime(state/'mouse-request.txt',(time.time()-10,time.time()-10))
  wait_mouse(False)
  observer=payload(20);observer['player']['steamid']='other';assert post(observer)==204
  assert not json.loads((state/'snapshot.json').read_text())['IsSelf'],'observer quarantine'
  assert post(b' '*140000)==413,'body limit'
  damage_baseline=payload(0);damage_baseline['player']['state']['round_totaldmg']=20
  assert post(damage_baseline)==204
  damage_kill=payload(2);damage_kill['player']['state']['round_totaldmg']=140
  assert post(damage_kill)==204
  damage_snapshot=json.loads((state/'snapshot.json').read_text())
  assert damage_snapshot['Events'][-1]['Damage']==120 and damage_snapshot['Events'][-1]['DamageLabel']=='更新增量','labeled damage delta reaches persisted kill event'
  missing_damage=payload(3);assert post(missing_damage)==204
  assert json.loads((state/'snapshot.json').read_text())['Events'][-1]['Damage'] is None,'missing damage remains unknown on wire'
  assert json.loads((state/'snapshot.json').read_text())['DamageStatus']=='not_sent','raw absent field is distinguished from parse failure'
  recovered=payload(4);recovered['player']['state']['round_totaldmg']=230.0
  recovered['previously']={'player':{'state':{'round_totaldmg':140}}}
  assert post(recovered)==204
  snap=json.loads((state/'snapshot.json').read_text())
  assert snap['Events'][-1]['Damage']==90,'acknowledged previous value recovers adjacent delta on wire'
  assert snap['DamageStatus']=='available','whole-valued JSON float is accepted by the real receiver'
  freeze=payload(4);freeze['map']['round']=2;freeze['round']['phase']='freezetime'
  freeze['player']['state'].update(round_kills=0,round_killhs=0,round_totaldmg=0,money=5500)
  assert post(freeze)==204
  snap=json.loads((state/'snapshot.json').read_text())
  assert snap['LastRound']['Kills']==4 and snap['LastRound']['Damage']==230,'last round persists in the actual receiver snapshot'
  freeze['player']['state']['money']=2000
  assert post(freeze)==204
  snap=json.loads((state/'snapshot.json').read_text())
  assert snap['LastRound']['Damage']==230 and snap['Money']==2000,'freeze shopping preserves archived combat values'
  assert 'allplayers' not in (state/'gamestate_integration_reticlelab.cfg').read_text()
  cfg=(state/'gamestate_integration_reticlelab.cfg').read_text()
  assert '"buffer" "0.0"' in cfg and '"throttle" "0.03"' in cfg,'low-latency generated config'
  import statistics
  elapsed=[]
  for i in range(30):
   began=time.perf_counter();assert post(payload(i))==204
   elapsed.append((time.perf_counter()-began)*1000)
   assert json.loads((state/'snapshot.json').read_text())['Kills']==i,'snapshot published before HTTP acknowledgement'
   time.sleep(.04)
  report={'samples':len(elapsed),'http_to_persisted_snapshot_median_ms':round(statistics.median(elapsed),3),'p95_ms':round(sorted(elapsed)[int(.95*(len(elapsed)-1))],3),'scope':'local synthetic POST through response with snapshot present; excludes CS2 generation, cfg timing, UI and monitor'}
  (run/'latest-latency.json').write_text(json.dumps(report,indent=2))
  print(json.dumps(report))
  print('HTTP smoke passed: startup/duplicate/port conflict; authenticated GSI; community respawn/round transition/dedup; real Raw Input registration, opt-out, re-enable and expired lease; observer isolation, size limit and cfg whitelist. No synthetic input.')
 finally:
  process.terminate()
  try:process.wait(timeout=5)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
