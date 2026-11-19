import sys,random,re,subprocess,os
from concurrent.futures import ThreadPoolExecutor
stage,lineup,steps,sample=sys.argv[1],sys.argv[2],int(sys.argv[3]),int(sys.argv[4])
env=dict(os.environ)
def run(walls,quiet=True):
    e=dict(env,WALLS=";".join(f"{x},{y}" for x,y in walls)); 
    if quiet: e["QUIET"]="1"
    out=subprocess.run(["dotnet","bin/Release/net10.0/Stage.dll",stage,lineup],env=e,capture_output=True).stdout.decode('cp949','replace')
    return out
def score(walls):
    o=run(walls); m=re.findall(r'코어도달 (\d+)',o)
    if not m or '전투[' not in o: return None
    att=sum(int(a) for a in re.findall(r'공격 (\d+)회',o))
    return sum(map(int,m))-att*0.001
walls=[]; random.seed(1)
for s in range(steps):
    o=run(walls,quiet=False)
    route=list({(x,int(l.split()[0])) for l in o.splitlines() if re.match(r'^\s*\d+ [.#a-zA-Z*]+$',l.rstrip()) for x,c in enumerate(l.split()[1]) if c in "abcdef*"})
    cand=random.sample(route,min(sample,len(route)))
    with ThreadPoolExecutor(6) as ex: res=list(ex.map(lambda c:(score(walls+[c]),c),cand))
    res=[r for r in res if r[0] is not None]; best=min(res)
    walls.append(best[1]); print(s+1,round(best[0],2),";".join(f"{x},{y}" for x,y in walls),flush=True)
