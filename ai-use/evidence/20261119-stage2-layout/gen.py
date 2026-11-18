import sys, uuid
# usage: gen.py map.txt out.asset name displayNameKey  — map rows top(y=max) first; '#' wall, 'C' core, digits 0-3 spawn of that faction, '.' open
rows=[l.rstrip('\n') for l in open(sys.argv[1]) if l.strip() and not l.startswith(';')]
d=len(rows); w=len(rows[0]); assert all(len(r)==w for r in rows), [len(r) for r in rows]
cx=[];cy=[];sp=[];ob=[]
for i,r in enumerate(rows):
    y=d-1-i
    for x,ch in enumerate(r):
        if ch=='C': cx.append(x); cy.append(y)
        elif ch in '0123': sp.append((x,y,int(ch)))
        elif ch=='#': ob.append((x,y))
sp.sort(key=lambda s:(s[2],s[0]))
out=f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 2073990d430741046825d63736970c68, type: 3}}
  m_Name: {sys.argv[3]}
  m_EditorClassIdentifier: Assembly-CSharp::StageData
  displayNameKey: {sys.argv[4]}
  width: {w}
  depth: {d}
  core:
    x: {min(cx)}
    y: {min(cy)}
    width: {max(cx)-min(cx)+1}
    height: {max(cy)-min(cy)+1}
  spawns:
"""
for x,y,f in sp: out+=f"  - cell: {{x: {x}, y: {y}}}\n    faction: {f}\n"
out+="  obstacles:\n"
for x,y in ob: out+=f"  - prefab: {{fileID: 0}}\n    cell: {{x: {x}, y: {y}}}\n    size: {{x: 1, y: 1}}\n    rotation: 0\n"
out+="  waves: {fileID: 0}\n"
open(sys.argv[2],'w').write(out)
print(f"{w}x{d} walls {len(ob)} spawns {sp}")
