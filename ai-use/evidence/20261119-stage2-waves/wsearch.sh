# usage: wsearch.sh <stage.asset> <lineup> <count> <seed> > out
cd "$(dirname "$0")"
dotnet bin/Release/net10.0/Stage.dll "$1" "$2" | iconv -f cp949 -t utf-8 > /tmp_map_$$.txt 2>/dev/null || dotnet bin/Release/net10.0/Stage.dll "$1" "$2" | iconv -f cp949 -t utf-8 > map_$$.txt
python - "$3" "$4" map_$$.txt > cands_$$.txt <<'PY'
import sys,random,re
n,seed,f=int(sys.argv[1]),int(sys.argv[2]),sys.argv[3]
rows=[l for l in open(f,encoding='utf-8') if re.match(r'^\s*\d+ [.#a-zA-Z*]+$',l.rstrip())]
route=[]
for l in rows:
    y,s=l.split(); y=int(y)
    route+=[(x,y) for x,c in enumerate(s) if c in "abcdef*"]
random.seed(seed)
for i in range(n): print(";".join(f"{x},{y}" for x,y in random.sample(route,random.randint(2,8))))
PY
while read w; do r=$(QUIET=1 WALLS="$w" dotnet bin/Release/net10.0/Stage.dll "$1" "$2" 2>&1 | iconv -f cp949 -t utf-8 | grep "전투\["); [ -n "$r" ] && echo "$w :: $r"; done < cands_$$.txt | sed -E 's/전투\[[^]]*\]: //; s/출전 [0-9]+ //g; s/ \| 첫 타격.*//; s/마법 0\(힐 0\) \| //'
rm -f map_$$.txt cands_$$.txt
