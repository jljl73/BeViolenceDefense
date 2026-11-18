import sys, collections
cols="label scale walls lineup finished ticks units fight approachMove approachStand queueWait routeMove routeBlocked hidden seeNoWalk switchAlive dropAlive toQueue reversals fastReversals idle1 idle3 idleTicksLong acquisitions detour15 detour2 sawAttacked sawNeverCore sawNeverDead neverSaw p50 p90 attacks reached queueFull queueNoSlot nearerIgnored ignoreVisible hits hitsUnable hitsQueue hitsRoute demotions promotions demote3 demoteMax".split()
rows=[dict(zip(cols,l.rstrip('\n').split('\t'))) for l in open(sys.argv[1]) if l.strip()]
num=[c for c in cols if c not in('label','walls','lineup','scale','p50','p90')]
for r in rows:
    for c in num: r[c]=int(r[c])
    r['p50']=float(r['p50']); r['p90']=float(r['p90'])
def summarize(name, rs):
    if not rs: return
    t=lambda c: sum(r[c] for r in rs)
    ut=sum(t(c) for c in ['fight','approachMove','approachStand','queueWait','routeMove','routeBlocked'])
    eng=t('fight')+t('approachMove')+t('approachStand')+t('queueWait')
    units=t('units'); sec=lambda x: x/60
    print(f"== {name}: 전투 {len(rs)} (미종료 {sum(1 for r in rs if not r['finished'])}), 유닛 {units}, 유닛·초 {sec(ut):.0f}")
    for c,k in [('fight','타격 위치(쿨다운 포함)'),('approachMove','접근 이동'),('approachStand','타깃 있고 서 있음'),('queueWait','자리 없어 대기'),('routeMove','경로 이동'),('routeBlocked','경로에서 막혀 섬')]:
        print(f"   {k:<20} {sec(t(c)):8.0f} 유닛·초  전체의 {100*t(c)/ut:5.1f}%   적 인지 시간의 {100*t(c)/eng if c in('fight','approachMove','approachStand','queueWait') else 0:5.1f}%")
    print(f"   적을 인지한 시간 중 때릴 수 없던 비율 {100*(eng-t('fight'))/eng:.1f}%  (가려진 적 추격 {sec(t('hidden')):.0f}, 보이지만 직선으로 못 감 {sec(t('seeNoWalk')):.0f} 유닛·초)")
    print(f"   유닛당: 살아있는 타깃 교체 {t('switchAlive')/units:.2f}회, 타깃 놓음 {t('dropAlive')/units:.2f}회, 자리 잃고 대기로 {t('toQueue')/units:.2f}회, 타깃 획득 {t('acquisitions')/units:.2f}회")
    print(f"   유닛당: 100도 넘는 방향 전환 {t('reversals')/units:.2f}회 (0.5초 안 재반전 {t('fastReversals')/units:.2f}회)")
    print(f"   적을 두고 1초 이상 서 있던 구간 {t('idle1')}개 (유닛당 {t('idle1')/units:.2f}), 3초 이상 {t('idle3')}개, 그 구간 합 {sec(t('idleTicksLong')):.0f} 유닛·초")
    print(f"   타깃 획득 때 걸어갈 거리가 직선의 1.5배 이상 {t('detour15')}건 ({100*t('detour15')/max(1,t('acquisitions')):.1f}%), 2배 이상 {t('detour2')}건")
    saw=t('sawAttacked')+t('sawNeverCore')+t('sawNeverDead')
    print(f"   적을 본 유닛 {saw}: 공격함 {t('sawAttacked')} / 한 번도 못 때리고 죽음 {t('sawNeverDead')} / 한 번도 못 때리고 코어 도달 {t('sawNeverCore')}  (적을 못 본 유닛 {t('neverSaw')})")
    print(f"   자리 없어 대기의 원인: 대상당 3명 제한이 참 {sec(t('queueFull')):.0f} 유닛·초 ({100*t('queueFull')/max(1,t('queueFull')+t('queueNoSlot')):.0f}%), 3명 미만인데 자리·길 없음 {sec(t('queueNoSlot')):.0f} 유닛·초")
    print(f"   맞은 횟수 {t('hits')}: 그 순간 아무도 때릴 수 없는 상태 {t('hitsUnable')} ({100*t('hitsUnable')/max(1,t('hits')):.1f}%), 그중 자리 없어 대기 중 {t('hitsQueue')}, 타깃 없이 경로 이동 중 {t('hitsRoute')}")
    print(f"   더 가까운 보이는 적을 두고 먼 타깃에 접근 {sec(t('nearerIgnored')):.0f} 유닛·초 (접근 시간의 {100*t('nearerIgnored')/max(1,t('approachMove')+t('approachStand')):.1f}%), 인지 거리 안 보이는 적이 있는데 타깃 없이 경로 이동 {sec(t('ignoreVisible')):.0f} 유닛·초")
    print(f"   자리→대기 강등 {t('demotions')}회, 대기→자리 승격 {t('promotions')}회, 3번 이상 강등된 유닛 {t('demote3')}명, 한 유닛 최다 {max(r['demoteMax'] for r in rs)}회")
    d50=sorted(r['p50'] for r in rs if r['p50']>=0); d90=sorted(r['p90'] for r in rs if r['p90']>=0)
    if d50: print(f"   첫 인지→첫 공격: 전투별 중앙값의 중앙값 {d50[len(d50)//2]:.2f}초, 전투별 90백분위의 중앙값 {d90[len(d90)//2]:.2f}초, 최악 {max(d90):.1f}초")
for s in ['1','2']:
    rs=[r for r in rows if r['scale']==s]
    summarize(f"칸 크기 {s}", rs)
    for m in ['Stage1','Stage2','Stage_Snowfield','Stage_Graveyard']:
        r2=[r for r in rs if r['label']==m]; t=lambda c: sum(r[c] for r in r2)
        eng=t('fight')+t('approachMove')+t('approachStand')+t('queueWait')
        print(f"     - {m:<16} 때릴 수 없던 비율 {100*(eng-t('fight'))/max(1,eng):5.1f}%  방향전환/유닛 {t('reversals')/t('units'):.2f}  교체/유닛 {t('switchAlive')/t('units'):.2f}  1초+정지/유닛 {t('idle1')/t('units'):.2f}  경로막힘 {100*t('routeBlocked')/max(1,t('routeBlocked')+t('routeMove')):.1f}%  미종료 {sum(1 for r in r2 if not r['finished'])}")
    small=[r for r in rs if r['units']<=12]; big=[r for r in rs if r['units']>12]
    for n,g in [('소규모(유닛 12 이하)',small),('대규모(유닛 20 이상)',big)]:
        t=lambda c: sum(r[c] for r in g); eng=t('fight')+t('approachMove')+t('approachStand')+t('queueWait')
        print(f"     * {n}: 때릴 수 없던 비율 {100*(eng-t('fight'))/max(1,eng):5.1f}%  서 있음 {100*(t('approachStand')+t('queueWait'))/max(1,eng):5.1f}%  못 때리고 죽음 {t('sawNeverDead')}/{t('units')}")
for r in rows:
    if not r['finished']: print('미종료:',r['label'],r['scale'],r['walls'],r['lineup'])
