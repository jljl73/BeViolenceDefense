import itertools, subprocess, sys, os
from concurrent.futures import ThreadPoolExecutor
D=os.path.dirname(os.path.abspath(__file__))
CAT='/Users/mignon/BeViolenceDefense/ProjectBeViolenceDefense/Assets/04_Data/03_Balance/EnemyCatalog.asset'
maps={
 'snow':('snow.asset',{'topL':'2,11','topR':'14,11','midL':'0,7;1,7','midR':'15,7;16,7','exit':'8,11','sideL':'3,9','sideR':'13,9','ridgeL':'4,7;5,7','ridgeR':'11,7;12,7'}),
 'grave':('grave.asset',{'lowL':'4,6;5,6','lowR':'11,6;12,6','lowC':'8,6','topL':'4,12;5,12','topR':'11,12;12,12','topC':'8,12'}),
}
L={'A':"goblin-grunt-1500*6|skeleton*6",'B':"wolf*6|zombie*3",'C':"goblin-slinger*5|skeleton-archer*5",'D':"orc*2,goblin-slinger*3|skeleton*2,living-armor*1,skeleton-archer*3",'E':"goblin-grunt-1500*12|zombie*8",'F':"orc*3,goblin-grunt-1500*8,goblin-slinger*4|skeleton*8,necromancer*1,skeleton-archer*4,zombie*3"}
name=sys.argv[1]; lineups=sys.argv[2]
asset,gates=maps[name]
jobs=[]
for r in range(len(gates)+1):
    for combo in itertools.combinations(gates,r):
        w=';'.join(gates[g] for g in combo)
        if w.count(',')>8: continue
        for k in lineups: jobs.append((combo,w,k))
def run(j):
    combo,w,k=j
    out=subprocess.run(['dotnet',D+'/bin/Release/net10.0/Stage.dll',D+'/'+asset,L[k]],env=dict(os.environ,CATALOG=CAT,WALLS=w,QUIET='1'),capture_output=True,text=True).stdout
    last=[l for l in out.split('\n') if l.strip()]
    if '배치 실패' in out: return ('rejected',combo,k,'')
    res=[l for l in last if '전투[' in l]
    return ('unfinished' if '미종료' in out else 'ok',combo,k,(res[0] if res else out[-300:]).strip()[:260])
with ThreadPoolExecutor(6) as ex: results=list(ex.map(run,jobs))
from collections import Counter
c=Counter(r[0] for r in results)
print(name,'조합',len(jobs),dict(c))
for r in results:
    if r[0]=='unfinished': print('미종료',r[1],r[2],r[3])
with open(D+f'/{name}-stress.txt','w') as f:
    f.write(f"{name}: 문 조합 x 편성 {lineups} = {len(jobs)}건, {dict(c)}\n")
    for r in results: f.write(f"{r[0]}\t{'+'.join(r[1]) or '-'}\t{r[2]}\t{r[3]}\n")
