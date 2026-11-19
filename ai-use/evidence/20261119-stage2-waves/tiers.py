import sys,itertools; sys.path.insert(0, sys.argv[1]); from surround import *
# id: (진영, 등급, dmg, iv, ar, 상대 해골 수(규칙), 목표 잔여) 또는 고정 hp
FIX={ # T0·원거리는 체력 직접 지정
"goblin-grunt":("고블린","T0",50,8,0.65,0),"goblin-charger":("고블린","T0",40,7,0.55,0),
"skeleton":("언데드","T0",70,12,0.8,0),"wolf":("야수","T0",60,8,0.5,0),"bat":("야수","T0",50,7,0.5,0),
"hatchling":("용족","T0",125,14,0.9,1),
"goblin-slinger":("고블린","원거리",45,7,0.8,0),"skeleton-archer":("언데드","원거리",55,10,1.0,0),"necromancer":("언데드","원거리",80,12,1.1,0)}
RULE={
"goblin-chief":("고블린","T1",13,0.85,0,2,0.05),"boar":("야수","T1",13,0.75,0,2,0.08),"dire-wolf":("야수","T1",11,0.55,0,2,0.08),"zombie":("언데드","T1",15,1.0,0,2,0.11),
"orc":("고블린","T2",18,1.0,0,3,0.05),"werewolf":("야수","T2",13,0.6,0,3,0.07),"reaper":("언데드","T2",20,1.1,0,3,0.10),"living-armor":("언데드","T2",13,1.1,2,3,0.11),
"demon":("용족","T2",17,0.85,1,3,0.13),"wyvern":("용족","T3",21,0.8,0,3,0.15)}
U={}
for k,(f,t,h,d,i,a) in FIX.items(): U[k]=(f,t,dict(hp=h,dmg=d,iv=i,ar=a))
for k,(f,t,d,i,a,n,tg) in RULE.items():
    hp,r=solve(d,i,a,n,tg); U[k]=(f,t,dict(hp=hp,dmg=d,iv=i,ar=a))
def duel(A,B):
    ta,tb=duel_time(A,B),duel_time(B,A)
    if abs(ta-tb)<1e-6: return 0,ta,0
    W,L,tw,s=(A,B,ta,1) if ta<tb else (B,A,tb,-1)
    hits=int(tw/L['iv']+1e-9)+1; return s,tw,1-hits*max(0,L['dmg']-W['ar'])/W['hp']
print("== 수치")
for k,(f,t,u) in U.items():
    extra=""
    if t in("T1","T2","T3"): n=RULE[k][5]; extra=f"해골{n} {fight(u,n)*100:5.1f}%  해골{n-1} {fight(u,n-1)*100:5.1f}%"
    elif k!="skeleton": r=fight(u,1); extra=f"해골1 {'%.0f%%'%(r*100) if r>0 else '패'}"
    print(f"{t:4}{f:4}{k:16}hp{u['hp']:4} dmg{u['dmg']:3} iv{u['iv']:5} ar{u['ar']}  {extra}")
groups={}
for k,(f,t,u) in U.items(): groups.setdefault("T2+" if t in("T2","T3") else t,[]).append(k)
for g,ks in groups.items():
    print("==",g)
    for a,b in itertools.combinations(ks,2):
        s,tw,rem=duel(U[a][2],U[b][2]); w=a if s>0 else b
        print(f"  {a} vs {b}: {'동시사망' if s==0 else f'{w} 승 {tw:.1f}s 잔여 {rem*100:.0f}%'}")
