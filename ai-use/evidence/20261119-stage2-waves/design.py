import sys; sys.path.insert(0, sys.argv[1]); from surround import *
D=[ # id, 진영, 등급, dmg, iv, ar, 상대 해골 수, 목표 잔여
("goblin-chief","고블린","T2",13,0.85,1,2,0.06),
("orc","고블린","T3",18,1.0,0,3,0.06),
("zombie","언데드","T2",15,1.0,0,2,0.10),
("living-armor","언데드","T3",12,1.1,3,3,0.10),
("reaper","언데드","T3",24,1.25,0,3,0.10),
("boar","야수","T2",12,0.75,1,2,0.08),
("dire-wolf","야수","T2",10,0.55,0,2,0.08),
("werewolf","야수","T3",12,0.55,0,3,0.08),
("hatchling","용족","T2",13,0.9,1,2,0.13),
("demon","용족","T3",18,0.85,1,3,0.13),
("wyvern","용족","T4",24,1.2,2,3,0.15),
]
for i,f,t,d,iv,ar,n,tg in D:
    hp,r=solve(d,iv,ar,n,tg); u=dict(hp=hp,dmg=d,iv=iv,ar=ar)
    print(f"{i:13}{f:4}{t} hp{hp:4} dmg{d:3} iv{iv:5} ar{ar} | 해골{n}명 잔여 {r*100:5.1f}% | 해골{n-1}명 잔여 {fight(u,n-1)*100:5.1f}% | 해골{n+1}명 {fight(u,n+1)} | 해골 1명 처치 {duel_time(u,SK):.1f}s | 전력 {hp*(d/iv)/1050:.2f}")
