#!/bin/zsh
# usage: batch.sh <map.txt> <layouts-file>
D="${0:a:h}"
L=("goblin-grunt-1500*6|skeleton*6" "wolf*6|zombie*3" "goblin-slinger*5|skeleton-archer*5" "orc*2,goblin-slinger*3|skeleton*2,living-armor*1,skeleton-archer*3" "goblin-grunt-1500*12|zombie*8" "orc*3,goblin-grunt-1500*8,goblin-slinger*4|skeleton*8,necromancer*1,skeleton-archer*4,zombie*3")
while IFS= read -r w; do
  [[ "$w" == \#* ]] && { echo "$w"; continue; }
  [[ "$w" == "-" ]] && w=""
  for l in $L; do QUIET=1 "$D/chk.sh" "$1" "$w" "$l" | grep -v "교전 예측" ; done
done < "$2"
