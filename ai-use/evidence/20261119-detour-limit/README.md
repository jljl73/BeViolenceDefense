# 우회 한도와 시야·이동 판정 통일 증거

설명은 [밀집 전투 문서](../../../docs/crowd-combat.md)의 마지막 절에 있다.

## 조건

- 기준: 커밋 `f891a86`의 전투 코드(snapshot2.txt). 게임의 칸 크기는 이 커밋에서 2다(`VoxelDefensePrototype.mapScale`).
- 전투: 해안가(Stage1), 설원, 묘지, 삭제 전 2스테이지 시안(OldStage2), Stage2_Temp, Stage3, Stage4 × 벽 배치 × 편성 6종 = 132개, 그리고 밀집 전투 8개. 칸 크기 2와 1로 각각. 마법 없음, Unity 밖 빌드.
- `y-repo.tsv`는 수정을 적용한 저장소 코드로 돌린 결과이며 실험판 `detour3 sight`(y-d3s, 여기에는 넣지 않음)와 바이트 단위로 같았다. 그 뒤에 넣은 도발 예외는 이 전투들(마법 없음)에 영향이 없다.

## 파일

| 파일 | 내용 |
| --- | --- |
| summary-detour.txt | 한도 2·3·4·6칸, 시야 통일, 적용 후 저장소 코드의 비교표 |
| y-*.tsv | 전투별 원자료. 열 이름은 cmp2.py의 `cols` |
| change.patch | 게임 코드와 테스트의 변경 |
| unity-results.txt | Unity EditMode 결과(고치기 전 코드에서 새 테스트 실패 확인 포함) |
| Diag.cs, run.py, cmp2.py, variants.py, detour_patch.py | 계측·실행·비교·실험판 생성 |

## 지표

"직선보다 N칸 넘게 걸은 접근"은 한 타깃을 정한 때부터 때릴 수 있게 되거나 타깃이 바뀔 때까지 걸은 거리에서 처음 직선 거리를 뺀 값이다. 달아나는 적을 쫓을 때도 커진다.
