# usage: combo.py <stage> <waves.txt> <ncands> ; 진영: 0 고블린 1 언데드 2 야수 3 용족. PICK 순서 = 언데드, 야수
import sys,re,random,subprocess,os,itertools
from concurrent.futures import ThreadPoolExecutor
stage,wf,nc=sys.argv[1],sys.argv[2],int(sys.argv[3])
NU,NB=4,2
def run(lineup,walls,pick):
    e=dict(os.environ,QUIET="1",WALLS=walls,PICK=pick)
    o=subprocess.run(["dotnet","bin/Release/net10.0/Stage.dll",stage,lineup],env=e,capture_output=True).stdout.decode('cp949','replace')
    m=re.findall(r'진영(\d) 출전 (\d+) 코어도달 (\d+) 사망 (\d+)',o)
    return {int(f):(int(n),int(r),int(d)) for f,n,r,d in m} if '전투[' in o else None
mp=subprocess.run(["dotnet","bin/Release/net10.0/Stage.dll",stage,"goblin-grunt*1"],env=dict(os.environ,WALLS=""),capture_output=True).stdout.decode('cp949','replace')
route=sorted({(x,int(l.split()[0])) for l in mp.splitlines() if re.match(r'^\s*\d+ [.#a-zA-Z*X]+$',l.rstrip()) for x,c in enumerate(l.split()[1]) if c in "abcdefgh*X"})
random.seed(21); cands=[""]+[";".join(f"{x},{y}" for x,y in random.sample(route,random.randint(2,8))) for _ in range(nc)]
N="GUBD"
for line in open(wf,encoding='utf-8'):
    name,*sides=line.strip().split('|'); sides+=[""]*(4-len(sides)); lineup="|".join(sides)
    us=range(NU) if sides[1] else [0]; bs=range(NB) if sides[2] else [0]
    print(f"== {name}",flush=True)
    for u,b in itertools.product(us,bs):
        pick=f"{u},{b}"
        with ThreadPoolExecutor(6) as ex: res=list(ex.map(lambda w:(w,run(lineup,w,pick)),cands))
        res=[(w,r) for w,r in res if r]
        def leak(r): return sum(v[1] for v in r.values())
        def fmt(r): return " ".join(f"{N[f]} {v[2]*100//v[0]}%" for f,v in sorted(r.items()))+f" 도달{leak(r)}"
        none=res[0][1]; bw,br=min(res[1:],key=lambda t:(leak(t[1]),t[0]))
        fav=min(br.items(),key=lambda kv:kv[1][2]/kv[1][0])[0]
        print(f"  U{u} B{b} | 벽없음 {fmt(none)} | 최선 {fmt(br)} 우세 {N[fav]} | {bw}",flush=True)
