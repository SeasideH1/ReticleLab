"""Read-only import of the user's installed CS2 Noto fonts. Never edits game files."""
from pathlib import Path
import argparse, shutil, struct, zlib, hashlib, json
parser=argparse.ArgumentParser()
parser.add_argument('--cs2-fonts',required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]/'Widget'
fonts=root/'Fonts'; fonts.mkdir(exist_ok=True)
records=[]
notices=[]
for name in ('notosanssc-regular.ttf','notosanssc-bold.ttf','notosans-regular.ttf','notosans-bold.ttf'):
    src=Path(args.cs2_fonts)/name
    if not src.is_file(): raise SystemExit(f'Missing CS2 font: {src}')
    shutil.copyfile(src,fonts/name)
    data=src.read_bytes()
    tables=struct.unpack_from('>H',data,4)[0]
    offset=next(struct.unpack_from('>I',data,12+i*16+8)[0] for i in range(tables) if data[12+i*16:16+i*16]==b'name')
    _,count,start=struct.unpack_from('>HHH',data,offset)
    names={}
    for i in range(count):
        platform,enc,lang,nid,length,pos=struct.unpack_from('>HHHHHH',data,offset+6+i*12)
        if platform==3 and lang==1033 and nid in (0,1,13,14):names[nid]=data[offset+start+pos:offset+start+pos+length].decode('utf-16-be')
    notices.append(name+'\n'+'\n'.join(names.values()))
    records.append({'name':name,'source':'CS2 game/csgo/panorama/fonts/'+name,'sha256':hashlib.sha256(src.read_bytes()).hexdigest()})
(fonts/'provenance.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
(fonts/'FONT-NOTICES.txt').write_text('\n\n'.join(notices),encoding='utf-8')
assets=root/'Assets';assets.mkdir(exist_ok=True)
def png(size):
    rows=[]
    for y in range(size):
        row=bytearray()
        for x in range(size):
            dx,dy=abs(x-(size-1)/2),abs(y-(size-1)/2)
            arm=(dx<size*.035 and size*.12<dy<size*.34) or (dy<size*.035 and size*.12<dx<size*.34)
            row.extend((230,240,195,255) if arm else (19,22,27,255))
        rows.append(b'\0'+row)
    def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows)))+chunk(b'IEND',b'')
for name,size in [('Logo44.png',44),('Logo150.png',150),('StoreLogo.png',50)]: (assets/name).write_bytes(png(size))
print('Prepared CS2 Noto fonts and original application icons.')
