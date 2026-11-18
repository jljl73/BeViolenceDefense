# 대기 위치 수정 증거

설명은 [밀집 전투 문서](../../../docs/crowd-combat.md)의 마지막 절에 있다.

| 파일 | 내용 |
| --- | --- |
| summary-waiting.txt | 이전 / 다가가기만 / + 싸움 없는 대기 포기 / 최종(경로 복귀 수정)의 비교표, 칸 크기 2와 1 |
| z-before.tsv, z-after.tsv, z-after2.tsv, z-after3.tsv | 위 네 판의 전투별 원자료(스테이지 132개 + 밀집 8개, 칸 크기 2·1). 열 이름은 cmp2.py의 `cols` |
| traces.txt | 테스트 장면에서 유닛이 돌아선 시점, 중간판에서 끝나지 않은 밀집 전투의 남은 유닛 상태 |
| unity-results.txt | Unity EditMode 결과(이전 코드·중간판에서 새 테스트 실패 확인 포함) |
| change-since-f891a86.patch | 커밋 `f891a86` 이후 전투 코드와 테스트의 변경 전체(우회 한도·시야 통일 포함). `DefenseBattle.cs`의 `HealPercent` 변경은 다른 작업의 것이다 |
| Diag.cs, Dump.cs, Rear.cs, run.py, cmp2.py | 계측·추적 도구 |

"멀리서 대기"는 적까지 거리가 대기선(공격 자리 반경 + 자기 몸통 간격)보다 0.5 넘게 먼 대기이고, "앞이 비어 있음"은 그 상태에서 적 쪽으로 0.4 앞에 다른 몸통과 벽이 없는 경우다.
