#!/bin/zsh
# usage: chk.sh <map.txt> <walls> [lineup]   env BIN=<build dir> (default bin/Release/net10.0)
D="${0:a:h}"; T=$(mktemp "$D/cur.XXXXXX")
python3 "$D/gen.py" "$1" "$T" Stage stage.x >/dev/null || exit 1
CATALOG=/Users/mignon/BeViolenceDefense/ProjectBeViolenceDefense/Assets/04_Data/03_Balance/EnemyCatalog.asset WALLS="$2" dotnet "${BIN:-$D/bin/Release/net10.0}/Stage.dll" "$T" ${3:+"$3"}
rm -f "$T"
