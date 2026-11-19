import subprocess, sys, os
from concurrent.futures import ThreadPoolExecutor
D=os.path.dirname(os.path.abspath(__file__))
L=["goblin-grunt-1500*6|skeleton*6","wolf*6|zombie*3","goblin-slinger*5|skeleton-archer*5","orc*2,goblin-slinger*3|skeleton*2,living-armor*1,skeleton-archer*3","goblin-grunt-1500*12|zombie*8","orc*3,goblin-grunt-1500*8,goblin-slinger*4|skeleton*8,necromancer*1,skeleton-archer*4,zombie*3"]
M={
 'Stage1':['','8,7','5,7;11,7','5,7;8,7;11,7'],
 'OldStage2':['','3,12;13,12','0,8;1,8;15,8;16,8','0,4;1,4;15,4;16,4','3,12'],
 'Stage2_Temp':[''],
 'Stage3':[''],
 'Stage4':[''],
 'Stage_Snowfield':['','2,11;14,11','0,7;1,7;15,7;16,7','0,7;1,7;14,11','2,11'],
 'Stage_Graveyard':['','4,12;5,12;11,12;12,12','4,6;5,6;11,6;12,6','4,6;5,6','4,6;5,6;8,6'],
}
jobs=[(m,w,l,s) for s in sys.argv[1:] for m in M for w in M[m] for l in L]
CW='6,5;10,5;7,4;9,4'
jobs+=[('DEFAULT',w,f'crowd:{n}',s) for s in sys.argv[1:] for n in (9,20,40,60) for w in ('',CW)]
def run(j):
    m,w,l,s=j
    r=subprocess.run(['dotnet',os.environ.get('BIN',D+'/bin/Release/net10.0')+'/Diag.dll',('DEFAULT' if m=='DEFAULT' else f'{D}/assets/{m}.asset'),l],env=dict(os.environ,CATALOG=D+'/assets/EnemyCatalog.asset',WALLS=w,SCALE=s,LABEL=m),capture_output=True,text=True)
    if os.environ.get('EXAMPLES')=='1' and r.stderr.strip(): open(D+'/examples.txt','a').write(r.stderr)
    return r.stdout.strip() or ('ERR '+r.stderr[-200:])
with ThreadPoolExecutor(6) as ex: out=list(ex.map(run,jobs))
open(D+'/'+os.environ.get('OUT','raw.tsv'),'w').write('\n'.join(out)+'\n')
print(len(out),'battles', sum(1 for o in out if o.count(chr(9))<40),'errors')
