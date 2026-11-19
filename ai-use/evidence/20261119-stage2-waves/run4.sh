cd "$(dirname "$0")"
while IFS='|' read -r n a b c d; do
  for W in $WALLSETS; do w=${W//_/;}; [ "$W" = none ] && w="";
    r=$(QUIET=1 WALLS="$w" dotnet bin/Release/net10.0/Stage.dll "$STAGE" "$a|$b|$c|$d" 2>&1 | iconv -f cp949 -t utf-8 | grep "전투\[")
    echo "$n ${W:0:5} $r" | sed -E 's/전투\[[^]]*\]: //; s/ \| 첫 타격.*//; s/마법 0\(힐 0\) \| //; s/출전 //g; s/코어도달/도달/g; s/진영0/G/; s/진영1/U/; s/진영2/B/; s/진영3/D/; s/ 공격 [0-9]+회//g'
  done
done < "$1"
