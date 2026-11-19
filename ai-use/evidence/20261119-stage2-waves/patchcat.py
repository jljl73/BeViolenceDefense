import sys,re
src,dst=sys.argv[1],sys.argv[2]
ch={k:dict(kv.split('=') for kv in v.split(',')) for k,v in (a.split(':') for a in sys.argv[3:])}
t=open(src,encoding='utf-8',newline='').read()
blocks=re.split(r'(\n  - factionGroup)',t)
for i,b in enumerate(blocks):
    m=re.search(r'\n    id: (\S+)',b)
    if m and m.group(1) in ch:
        for f,v in ch[m.group(1)].items():
            b,n=re.subn(r'(\n    %s: )[^\r\n]*'%f, r'\g<1>'+v, b); assert n==1,(m.group(1),f)
        blocks[i]=b
open(dst,'w',encoding='utf-8',newline='').write(''.join(blocks))
