# 2스테이지 길 구조 시안 검증 자료

| 파일 | 내용 |
| --- | --- |
| `stage2-map.txt` | 지형 글자 그림 원본. 첫 줄이 y=16, `#` 벽, `C` 코어, `0`·`1` 진영별 스폰 |
| `gen.py` | 글자 그림을 `StageData` 에셋 YAML로 바꾸는 스크립트 |
| `StageCheck.cs` | 에셋을 읽어 게임의 `DefenseGrid`·`DefenseBattle`로 경로, 교전 예측 칸, 마법 없는 전투 결과를 출력하는 콘솔 도구 |
| `routes-outside-unity.txt` | 벽 배치별 경로 그림과 전투 결과 (Unity 밖 빌드) |
| `batch-outside-unity.txt` | 편성 6종 × 벽 배치 17종 일괄 실행. 102개 중 1개 미종료 |
| `Stage2DiagTests.cs.txt` | Unity 프로젝트 복사본에만 넣고 돌린 임시 EditMode 테스트. 프로젝트에는 넣지 않았다 |
| `unity-results.txt` | Unity 6000.3.28f1 EditMode 결과 요약과 임시 테스트 출력 |

## 다시 돌리기

`StageCheck.cs`는 `BalanceSim/UnityStubs.cs`와 전투 소스 5개(`DefenseBattle`, `DefenseGrid`, `EnemyUnit`, `CombatPositioning`, `BattleClock`)를 함께 컴파일한다. 이 기기에는 net8.0 타기팅 팩이 없어 net10.0으로 빌드했다.

```bash
python3 gen.py stage2-map.txt Stage2.asset Stage2 stage.2
CATALOG=<EnemyCatalog.asset 경로> WALLS="0,8;1,8;15,8;16,8" dotnet Stage.dll Stage2.asset "goblin-grunt-1500*6|skeleton*6"
```

## 한계

- Unity 밖 빌드는 Unity와 실수 계산이 달라질 수 있다. 이번에 Unity에서 대조한 12개 전투는 같은 값이었다.
- 편성은 길을 보기 위한 임시 값이다.
