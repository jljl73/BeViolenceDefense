# 사용: steer_patch.py <dst 이름> <옵션...>  옵션: world2 (우회 한도 2, 칸 크기와 무관), cone (옆걸음은 앞쪽 60도 안), lane (옆걸음 뒤 목표까지 길이 열려 있어야 함)
import sys, shutil, os
D=os.path.dirname(os.path.abspath(__file__)); name, opts = sys.argv[1], set(sys.argv[2:])
dst=f'{D}/src-{name}'; shutil.rmtree(dst, ignore_errors=True); shutil.copytree(f'{D}/src-r0', dst)
p=f'{dst}/00_Core/CombatPositioning.cs'; t=open(p).read()
def rep(a,b):
    global t
    assert t.count(a)==1,(a[:60],t.count(a)); t=t.replace(a,b)
if 'world2' in opts:
    rep('internal const float MaxPursuitDetour = 3f;','internal const float MaxPursuitDetour = 2f;')
    rep('enemy.ActiveTaunter == target ? float.PositiveInfinity : MaxPursuitDetour * grid.CellSize;','enemy.ActiveTaunter == target ? float.PositiveInfinity : MaxPursuitDetour;')
if 'cone' in opts:
    rep('if (Vector3.Dot(direction, desired) < -0.001f)','if (Vector3.Dot(direction, desired) < 0.5f)')
if 'lane' in opts:
    rep('''            if (cost >= shortest || !CanMove(enemy, target, enemy.Position, point))
                continue;
            shortest = cost;
            waypoint = point;''','''            if (cost >= shortest || !CanMove(enemy, target, enemy.Position, point) ||
                !CanMove(enemy, target, point, goal))
                continue;
            shortest = cost;
            waypoint = point;''')
open(p,'w').write(t); print('made',name,sorted(opts))
