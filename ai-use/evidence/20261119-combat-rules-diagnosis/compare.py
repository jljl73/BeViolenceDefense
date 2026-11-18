import os, sys
D=os.path.dirname(os.path.abspath(__file__))
cols="label scale walls lineup finished ticks units fight approachMove approachStand queueWait routeMove routeBlocked hidden seeNoWalk switchAlive dropAlive toQueue reversals fastReversals idle1 idle3 idleTicksLong acquisitions detour15 detour2 sawAttacked sawNeverCore sawNeverDead neverSaw p50 p90 attacks reached queueFull queueNoSlot nearerIgnored ignoreVisible hits hitsUnable hitsQueue hitsRoute demotions promotions demote3 demoteMax".split()
def load(f,scale='1'):
    rows=[dict(zip(cols,l.rstrip('\n').split('\t'))) for l in open(D+'/'+f) if l.strip()]
    rows=[r for r in rows if r['scale']==scale]
    for r in rows:
        for c in cols:
            if c not in('label','walls','lineup','scale'): r[c]=float(r[c])
    return rows
sets=[('기준',load('raw-base.tsv')),('대상당 6명',load('raw-cap6.tsv')),('공격선 몸통 무시',load('raw-over.tsv')),('둘 다',load('raw-both.tsv')),('시야=이동 판정',load('raw-sight.tsv')),('칸 2배(참고)',load('raw-base.tsv','2'))]
def row(name,f): print(f"{name:<34}"+''.join(f"{f(rs):>14}" for _,rs in sets))
print(' '*34+''.join(f"{n:>14}" for n,_ in sets))
T=lambda rs,c: sum(r[c] for r in rs)
eng=lambda rs: T(rs,'fight')+T(rs,'approachMove')+T(rs,'approachStand')+T(rs,'queueWait')
med=lambda rs,c: sorted(r[c] for r in rs if r[c]>=0)[len([r for r in rs if r[c]>=0])//2]
row('미종료 전투',lambda rs: int(sum(1 for r in rs if not r['finished'])))
row('인지 중 때릴 수 없던 비율 %',lambda rs: f"{100*(eng(rs)-T(rs,'fight'))/eng(rs):.1f}")
row('자리 없어 대기 유닛·초',lambda rs: f"{T(rs,'queueWait')/60:.0f}")
row('  그중 3명 미만인데 자리 없음',lambda rs: f"{T(rs,'queueNoSlot')/60:.0f}")
row('접근 이동 유닛·초',lambda rs: f"{T(rs,'approachMove')/60:.0f}")
row('타격 위치 유닛·초',lambda rs: f"{T(rs,'fight')/60:.0f}")
row('경로에서 막혀 섬 유닛·초',lambda rs: f"{T(rs,'routeBlocked')/60:.0f}")
row('보이지만 직선으로 못 감 유닛·초',lambda rs: f"{T(rs,'seeNoWalk')/60:.0f}")
row('1초+ 정지 구간',lambda rs: int(T(rs,'idle1')))
row('3초+ 정지 구간',lambda rs: int(T(rs,'idle3')))
row('자리→대기 강등',lambda rs: int(T(rs,'demotions')))
row('100도+ 방향 전환',lambda rs: int(T(rs,'reversals')))
row('살아있는 타깃 교체',lambda rs: int(T(rs,'switchAlive')))
row('공격 횟수',lambda rs: int(T(rs,'attacks')))
row('코어 도달 유닛',lambda rs: int(T(rs,'reached')))
row('전투 길이 합(초)',lambda rs: f"{T(rs,'ticks')/60:.0f}")
row('첫 인지→첫 공격 중앙값(초)',lambda rs: f"{med(rs,'p50'):.2f}")
row('  90백분위의 중앙값(초)',lambda rs: f"{med(rs,'p90'):.2f}")
print()
rs=sets[0][1]
for l in sorted(set(r['lineup'] for r in rs), key=len):
    g=[r for r in rs if r['lineup']==l]
    print(f"{l[:60]:<62} 못때림 {100*(eng(g)-T(g,'fight'))/max(1,eng(g)):5.1f}%  대기 {100*T(g,'queueWait')/max(1,eng(g)):5.1f}%  경로막힘 {100*T(g,'routeBlocked')/max(1,T(g,'routeBlocked')+T(g,'routeMove')):5.1f}%  3회+강등 {int(T(g,'demote3'))}명")
