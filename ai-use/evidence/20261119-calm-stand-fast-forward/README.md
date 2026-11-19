# 적이 없는데 서 있는 문제와 빨리감기 증거

설명은 [밀집 전투 문서](../../../docs/crowd-combat.md)의 마지막 절에 있다.

| 파일 | 내용 |
| --- | --- |
| summary-calm.txt | 수정 전후 비교(칸 크기 2·1)와 대안 9가지 비교 |
| traces.txt | 대안별 후열 장면의 돌아섬 횟수, 수정 전 4초 넘게 선 유닛의 추적 |
| c-now21.tsv, c-after.tsv | 수정 전후 저장소 코드의 전투별 원자료(스테이지 132개 + 밀집 8개, 칸 크기 2·1) |
| c-w0.tsv, c-now.tsv, c-fix1.tsv, c-v1.tsv, c-v2.tsv, c-v3.tsv, c-v4.tsv, c-t05.tsv, c-t025.tsv | 대안별 원자료(칸 크기 2). `c-t05`가 채택한 판이고 적용 후 저장소 코드의 칸 크기 2 결과와 같았다 |
| unity-results.txt | Unity EditMode·Play Mode 결과 |
| FastForwardValidation.cs.txt | Play Mode 자동 확인에 쓴 임시 Editor 스크립트(프로젝트 복사본에만 두었다) |
| Diag.cs, Rear.cs, run.py, cmp2.py | 계측·추적 도구 |

"적이 없는데 서 있음"은 살아 있는 유닛이 모두 한 진영인 틱에 움직이지 않은 유닛을 센 것이다. Unity 밖 빌드, 마법 없음.
