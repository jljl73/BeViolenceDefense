cd "$(dirname "$0")"
while IFS='|' read -r n a b; do
  for W in $WALLSETS; do for M in $MAGICS; do w=${W//_/;}; [ "$W" = none ] && w="";
    r=$(MAGIC=$M QUIET=1 WALLS="$w" dotnet bin/Release/net10.0/Stage.dll "$STAGE" "$a|$b" 2>&1 | iconv -f cp949 -t utf-8 | grep "전투\[")
    echo "$n ${W:0:4} m$M $r" | sed -E 's/전투\[[^]]*\]: //; s/ \| 첫 타격.*//; s/출전 //g; s/코어도달/도달/g; s/진영0/G/; s/진영1/U/'
  done; done
done < "$1"
