# 사용: cmp2.py 이름=파일 ...   (칸 크기 열로 거르지 않고 파일 전체를 본다)
import os, sys
D=os.path.dirname(os.path.abspath(__file__))
cols="label scale walls lineup finished ticks units fight approachMove approachStand queueWait routeMove routeBlocked hidden seeNoWalk switchAlive dropAlive toQueue reversals fastReversals idle1 idle3 idleTicksLong acquisitions detour15 detour2 sawAttacked sawNeverCore sawNeverDead neverSaw p50 p90 attacks reached queueFull queueNoSlot nearerIgnored ignoreVisible hits hitsUnable hitsQueue hitsRoute demotions promotions demote3 demoteMax overlapRest overlapRestDeep overlapTransit enemyOverlap crossedWall core headingFlips headingFlipsFast episodes long3 long6 extraSum extraMax approachWalk waitDistSum waitGapSum waitFar waitFarFree awayPath awaySteer awayOther movePath moveSteer moveOther returnPath offRouteMove ex05 ex1 ex2 ex1Path ex1Steer ex1Hidden ex1Plain marchMove back1 back2 backEpisodes loopA loopQ loopR loopO loopF".split()
def load(f):
    rows=[dict(zip(cols,l.rstrip('\n').split('\t'))) for l in open(D+'/'+f) if l.strip()]
    for r in rows:
        for c in cols:
            if c not in('label','walls','lineup','scale'): r[c]=float(r.get(c,0))
    return rows
def pick_scale(a):
    f=a.split('=')[1]
    if '@' in f:
        f,s=f.split('@'); return [r for r in load(f) if r['scale']==s]
    return load(f)
sets=[(a.split('=')[0],pick_scale(a)) for a in sys.argv[1:]]
T=lambda rs,c: sum(r[c] for r in rs)
eng=lambda rs: T(rs,'fight')+T(rs,'approachMove')+T(rs,'approachStand')+T(rs,'queueWait')
def med(rs,c):
    v=sorted(r[c] for r in rs if r[c]>=0); return v[len(v)//2] if v else -1
def table(title, pick):
    print(f"\n## {title}")
    def row(name,f): print(f"{name:<32}"+''.join(f"{f([r for r in rs if pick(r)]):>13}" for _,rs in sets))
    print(' '*32+''.join(f"{n:>13}" for n,_ in sets))
    row('전투 수',lambda rs: len(rs))
    row('맴돈 3초 구간: 적 접근 중',lambda rs: int(T(rs,'loopA')))
    row('  자리 없이 대기 중',lambda rs: int(T(rs,'loopQ')))
    row('  경로 위 이동 중',lambda rs: int(T(rs,'loopR')))
    row('  경로 밖 복귀 중',lambda rs: int(T(rs,'loopO')))

    row('타깃 없이 이동 유닛·초',lambda rs: f"{T(rs,'marchMove')/60:.0f}")
    row('  코어에서 1칸+ 뒤로 간 채 이동',lambda rs: f"{T(rs,'back1')/60:.0f}")
    row('  2칸+ 뒤로',lambda rs: f"{T(rs,'back2')/60:.0f}")
    row('  뒤로 간 횟수',lambda rs: int(T(rs,'backEpisodes')))

    row('접근 이동 유닛·초: 경로탐색',lambda rs: f"{T(rs,'movePath')/60:.0f}")
    row('  옆걸음',lambda rs: f"{T(rs,'moveSteer')/60:.0f}")
    row('  직행·둘레 우회',lambda rs: f"{T(rs,'moveOther')/60:.0f}")
    row('적에게서 멀어지는 접근: 경로탐색',lambda rs: f"{T(rs,'awayPath')/60:.0f}")
    row('  옆걸음',lambda rs: f"{T(rs,'awaySteer')/60:.0f}")
    row('  직행·둘레 우회',lambda rs: f"{T(rs,'awayOther')/60:.0f}")
    row('접근 한 번에 0.5칸 넘게 더 걸음',lambda rs: int(T(rs,'ex05')))
    row('  1칸 넘게',lambda rs: int(T(rs,'ex1')))
    row('  2칸 넘게',lambda rs: int(T(rs,'ex2')))
    row('1칸 넘게의 방식: 경로탐색',lambda rs: int(T(rs,'ex1Path')))
    row('  옆걸음',lambda rs: int(T(rs,'ex1Steer')))
    row('  가려진 적 추격',lambda rs: int(T(rs,'ex1Hidden')))
    row('  그 외(쫓아감·둘레 우회)',lambda rs: int(T(rs,'ex1Plain')))
    row('접근 횟수',lambda rs: int(T(rs,'episodes')))
    row('경로 밖 복귀 이동 유닛·초',lambda rs: f"{T(rs,'offRouteMove')/60:.0f}")
    row('  그중 경로탐색 복귀',lambda rs: f"{T(rs,'returnPath')/60:.0f}")

    row('대기 중 적까지 평균 거리',lambda rs: f"{T(rs,'waitDistSum')/max(1,T(rs,'queueWait')):.2f}")
    row('  대기선보다 평균 초과 거리',lambda rs: f"{T(rs,'waitGapSum')/max(1,T(rs,'queueWait')):.2f}")
    row('멀리서 대기 유닛·초',lambda rs: f"{T(rs,'waitFar')/60:.0f}")
    row('  그중 앞이 비어 있음',lambda rs: f"{T(rs,'waitFarFree')/60:.0f}")

    row('접근 중 걸은 거리 합(칸 아님, 월드)',lambda rs: f"{T(rs,'approachWalk'):.0f}")
    row('직선보다 3칸 넘게 돌아간 접근',lambda rs: int(T(rs,'long3')))
    row('  6칸 넘게',lambda rs: int(T(rs,'long6')))
    row('  가장 크게 돌아간 접근(칸)',lambda rs: f"{max([r['extraMax'] for r in rs] or [0]):.1f}")
    row('가려진 적 추격 유닛·초',lambda rs: f"{T(rs,'hidden')/60:.0f}")

    row('미종료 전투',lambda rs: int(sum(1 for r in rs if not r['finished'])))
    row('인지 중 때릴 수 없던 비율 %',lambda rs: f"{100*(eng(rs)-T(rs,'fight'))/max(1,eng(rs)):.1f}")
    row('타격 위치 유닛·초',lambda rs: f"{T(rs,'fight')/60:.0f}")
    row('접근 이동 유닛·초',lambda rs: f"{T(rs,'approachMove')/60:.0f}")
    row('자리 없어 대기 유닛·초',lambda rs: f"{T(rs,'queueWait')/60:.0f}")
    row('  그중 3명 제한이 참',lambda rs: f"{T(rs,'queueFull')/60:.0f}")
    row('경로에서 막혀 섬 유닛·초',lambda rs: f"{T(rs,'routeBlocked')/60:.0f}")
    row('1초+ 정지 구간',lambda rs: int(T(rs,'idle1')))
    row('3초+ 정지 구간',lambda rs: int(T(rs,'idle3')))
    row('자리→대기 강등',lambda rs: int(T(rs,'demotions')))
    row('위치 이동 방향 전환(밀림 포함)',lambda rs: int(T(rs,'reversals')))
    row('  0.5초 안 재반전',lambda rs: int(T(rs,'fastReversals')))
    row('바라보는 방향 뒤집힘',lambda rs: int(T(rs,'headingFlips')))
    row('  0.5초 안 재반전',lambda rs: int(T(rs,'headingFlipsFast')))
    row('살아있는 타깃 교체',lambda rs: int(T(rs,'switchAlive')))
    row('첫 인지→첫 공격 중앙값(초)',lambda rs: f"{med(rs,'p50'):.2f}")
    row('  90백분위의 중앙값(초)',lambda rs: f"{med(rs,'p90'):.2f}")
    row('공격 횟수',lambda rs: int(T(rs,'attacks')))
    row('코어 도달 유닛',lambda rs: int(T(rs,'reached')))
    row('전투 길이 합(초)',lambda rs: f"{T(rs,'ticks')/60:.0f}")
    row('아군 겹침: 서 있는 채 유닛쌍·초',lambda rs: f"{T(rs,'overlapRest')/60:.0f}")
    row('  그중 절반 넘게 겹침',lambda rs: f"{T(rs,'overlapRestDeep')/60:.0f}")
    row('아군 겹침: 지나가는 중 쌍·초',lambda rs: f"{T(rs,'overlapTransit')/60:.0f}")
    row('적과 겹침 쌍·틱',lambda rs: int(T(rs,'enemyOverlap')))
    row('벽 통과 전투 수',lambda rs: int(T(rs,'crossedWall')))
table('스테이지 4종 114개 전투', lambda r: r['label']!='DEFAULT')
table('밀집 전투 8개 (기본 지형, 진영당 9·20·40·60명, 벽 없음/4개)', lambda r: r['label']=='DEFAULT')
for n,rs in sets:
    for r in rs:
        if not r['finished']: print('미종료', n, r['label'], r['walls'], r['lineup'][:50])
