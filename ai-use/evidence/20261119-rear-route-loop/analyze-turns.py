from pathlib import Path
import csv, math, collections, gzip, json

def analyze(path):
    by=collections.defaultdict(list)
    with path.open() as f:
        for r in csv.DictReader(f): by[int(r['id'])].append(r)
    result={}
    for order, rows in sorted(by.items()):
        turns=[]
        for a,b in zip(rows,rows[1:]):
            if b['target'] or b['queue']: continue
            ax,az=float(a['hx']),float(a['hz']);bx,bz=float(b['hx']),float(b['hz'])
            m=math.hypot(ax,az)*math.hypot(bx,bz)
            if m and (ax*bx+az*bz)/m < math.cos(math.radians(100)):
                turns.append(int(b['tick']))
        result[order]={'route_turns_over_100':len(turns),'ticks':turns,'attacks':int(rows[-1]['attacks'])}
    return result
if __name__=='__main__':
    import sys
    for name in sys.argv[1:]: print(name, json.dumps(analyze(Path(name)),ensure_ascii=False,indent=2))
