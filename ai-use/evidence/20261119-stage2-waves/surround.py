import math
SK=dict(hp=70,dmg=12,iv=0.8,ar=0)
def ticks(iv): return max(1,round(iv*60))
def fight(x,n,sk=SK):
    """x 1명이 n명에게 둘러싸여 동시에 교전. 모두 t=0에 첫 공격. x는 한 명씩 집중. 반환: x 남은 체력 비율(음수면 패배, -남은 적 수)"""
    hx=x['hp']; hs=[sk['hp']]*n; cx=0; cs=[0]*n; ix=ticks(x['iv']); i_s=ticks(sk['iv'])
    dx=max(0,x['dmg']-sk['ar']); ds=max(0,sk['dmg']-x.get('ar',0))
    for t in range(60*120):
        alive=[i for i in range(n) if hs[i]>0]
        if not alive: return hx/x['hp']
        if hx<=0: return -len(alive)
        px=0; ps=0
        if cx<=0: ps=dx; cx=ix
        for i in alive:
            if cs[i]<=0: px+=ds; cs[i]=i_s
        if ps: hs[alive[0]]-=ps
        hx-=px; cx-=1; cs=[c-1 for c in cs]
    return None
def duel_time(a,b):
    """a가 b를 쓰러뜨리는 시간(초)"""
    h=math.ceil(b['hp']/max(1,a['dmg']-b.get('ar',0))); return (h-1)*a['iv']
def solve(dmg,iv,ar,n,target):
    best=None
    for hp in range(40,600,5):
        r=fight(dict(hp=hp,dmg=dmg,iv=iv,ar=ar),n)
        if r is not None and r>0 and (best is None or abs(r-target)<abs(best[1]-target)): best=(hp,r)
    return best
