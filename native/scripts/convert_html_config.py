"""Convert the existing HTML schema 5 export to native schema 1 (no simulated game data)."""
import argparse,json
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('source');parser.add_argument('destination');args=parser.parse_args()
raw=Path(args.source).read_text(encoding='utf-8-sig')
if len(raw)>128*1024:raise SystemExit('Config too large')
source=json.loads(raw)
if source.get('schemaVersion')!=5:raise SystemExit('Expected HTML schemaVersion 5')
s=source['controls'];p={'Schema':1}
for group,mapping in {
 'crosshair':{'x':'OffsetX','y':'OffsetY','size':'CrosshairSize','gap':'CrosshairGap','thickness':'CrosshairThickness','dot':'Dot','color':'CrosshairColor','killColor':'KillColor','headKillColor':'HeadKillColor','hitColor':'HitColor','headColor':'HeadColor','effectSize':'EffectSize','effectWidth':'EffectWidth','duration':'EffectDuration'},
 'kill':{'width':'FeedWidth','height':'FeedHeight','radius':'FeedRadius','color':'FeedColor','enter':'Enter','hold':'Hold','exit':'Exit'},
 'input':{'color':'InputColor','trailLife':'TrailLife','idleCenter':'IdleCenter','sensitivity':'Sensitivity','snap':'Snap','snapGrid':'Grid'},
 'stats':{'color':'Accent'},
}.items():
 for key,dest in mapping.items():
  if key in s.get(group,{}):p[dest]=s[group][key]
p['InputOpacity']=s.get('input',{}).get('opacity',32)/100
p['StatsOpacity']=s.get('stats',{}).get('opacity',30)/100
p['FeedOpacity']=s.get('kill',{}).get('opacity',75)/100
p['StatsRoundOnly']=s.get('stats',{}).get('displayMode')=='round-start'
p['DimOpacity']=s.get('stats',{}).get('buyOpacity',20)/100
p['FontFamily']='Noto Sans SC'
def tile(t,mode='inherit',height=48):
 return {'Id':t['id'],'Label':t.get('label',t['id']),'X':t.get('x',0),'Y':t.get('y',0),'Width':t.get('w',96),'Height':height,'Mode':'hidden' if t.get('visible') is False else t.get('displayMode',mode),'Style':s.get('input',{}).get('style','rounded')}
p['Stats']=[tile(t) for t in s.get('stats',{}).get('items',[])]
codes={'ShiftLeft':'LeftShift','ShiftRight':'RightShift','ControlLeft':'LeftControl','ControlRight':'RightControl','AltLeft':'LeftMenu','AltRight':'RightMenu','Space':'Space','ArrowLeft':'Left','ArrowRight':'Right','ArrowUp':'Up','ArrowDown':'Down','Enter':'Enter','Escape':'Escape','Backspace':'Back','Tab':'Tab'}
def native_key(code):
 if code in codes:return codes[code]
 if code.startswith('Key') and len(code)==4:return code[3:]
 if code.startswith('Digit') and len(code)==6:return 'Number'+code[5:]
 if code.startswith('F') and code[1:].isdigit():return code
 raise ValueError('Unsupported key code: '+code)
p['Inputs']=[tile(dict(t,id=native_key(t['code'])),'background',40) for t in s.get('input',{}).get('keys',[])]
for name,w,h in [('mouse',58,94),('trace',90,96)]:
 t=s.get('input',{}).get(name,{})
 p['Inputs'].append(tile(dict(t,id=name,label=name,w=w),'always' if name=='trace' else 'background',h))
Path(args.destination).write_text(json.dumps(p,ensure_ascii=False,indent=2),encoding='utf-8')
print('Converted visual settings only. CSS coordinates become DIP; crosshair offsets become physical pixels. Widget screen positions are managed by Game Bar.')
