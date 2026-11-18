# 결정론

같은 지형, 같은 벽 배치, 같은 틱에 건 같은 마법이면 PC 성능이나 프레임 속도와 관계없이 항상 같은 전투 결과가 나오게 한다. 대상 플랫폼이 Windows PC 하나이므로 같은 빌드 안에서의 일치를 목표로 하고, 고정소수점 같은 OS·CPU 간 일치는 다루지 않는다.

## 구조

| 항목 | 규칙 | 위치 |
| --- | --- | --- |
| 고정 틱 | 전투 로직은 `DefenseBattle.TickDuration`(초당 60틱) 단위로만 진행한다. 공격 간격 0.65초는 39틱처럼 지금 수치가 모두 틱 단위로 떨어진다 | `DefenseBattle` |
| 틱 누적 | `BattleClock`이 실제 흐른 시간 × 재생 속도를 쌓아 두었다가 한 틱 분량이 찰 때마다 틱을 돌린다. 한 프레임에 최대 8틱까지 돌리고, 넘치는 시간은 버려 게임이 잠시 느려진다 | `BattleClock` |
| 배속·조준 슬로우 | 틱이 도는 속도만 바꾼다. 틱 길이는 그대로라 결과에 영향을 주지 않으므로 바로 적용한다 | `BattleClock.BeginFrame` |
| 마나 회복 | 틱 안에서 회복한다 | `DefenseBattle.Tick` |
| 마법 | 클릭하면 `QueueSpell`로 큐에 넣고 다음 틱이 시작될 때 적용한다. 적용 결과는 `SpellResolved` 이벤트로 화면에 알린다 | `DefenseBattle`, `VoxelDefensePrototype` |
| 명령 기록 | 적용한 마법은 틱 번호와 함께 `CommandHistory`에 남는다. `ScheduleSpell`로 같은 틱에 다시 넣으면 같은 판이 재현된다 | `DefenseBattle` |
| 화면 보간 | 유닛 모델은 직전 틱 위치와 현재 틱 위치 사이를 `BattleClock.Interpolation`으로 보간해 그린다. 조준 슬로우처럼 틱이 드문드문 돌아도 부드럽게 움직인다 | `DefenseBoardView.SyncEnemy` |

벽 배치는 준비 단계에서만 하므로 따로 큐에 넣지 않는다.

## 검증

`BalanceSim determinism <라운드>`는 게임 루프와 같은 `BattleClock`에 서로 다른 프레임 패턴을 넣고, 틱마다 상태 해시(틱 번호, 마나, 스폰 수, 코어 체력, 유닛별 위치·체력·공격 횟수·버프)를 비교한다. 마법 두 번(틱 420, 780)을 함께 건다.

| 프레임 패턴 | W1 | W2 | W3 |
| --- | --- | --- | --- |
| 30fps / 60fps / 144fps | 일치 | 일치 | 일치 |
| 20~144fps 무작위 | 일치 | 일치 | 일치 |
| 2배속·조준 슬로우·0.5초 멈칫 섞음 | 일치 | 일치 | 일치 |
| 비교: 예전 방식(프레임 시간 = 틱 길이) | 코어 117 / 119 / 118 | 해시 3개 모두 다름 | 코어 116 / 116 / 118 |

예전 방식은 프레임 속도에 따라 W1 코어 체력이 30fps 117, 60fps 119, 144fps 118로 갈렸다. 고정 틱에서는 모든 패턴이 모든 틱에서 같은 상태를 낸다.

## 로직과 화면 분리

| 규칙 | 내용 |
| --- | --- |
| 전투 로직 파일 | `BattleClock`, `CombatPositioning`, `EnemyUnit`(`EnemyFaction` 포함), `DefenseBattle`, `DefenseGrid` |
| 금지 | `Time`, `Random`, `Transform`, `Animator`, `GameObject`·`Material`·`Renderer` 같은 화면 데이터, 물리, 화면·UI·씬 코드(`DefenseBoardView`, `DefenseHud`, `EnemyCatalog` 등) |
| 화면 데이터 | 모델·애니메이터·머티리얼·크기는 `EnemyFaction`에 두지 않는다. 화면이 `UnitId`로 `EnemyCatalog.TryGetVisual`을 찾아 쓴다 |
| 정렬 | 마지막 비교에서 `SpawnOrder`로 동점을 깬다 |
| 난수 | 필요해지면 스테이지·라운드로 시드를 정한 전투 전용 난수를 전투 로직 안에 둔다 |

### 자동 검사

Unity `Window > General > Test Runner` → `EditMode`에서 실행한다. 테스트는 `Assets/09_Tests/Editor`에 있다.

| 테스트 | 확인하는 것 |
| --- | --- |
| `SimulationDeterminismTests.SameTicksAcrossFramePatterns` | 60fps 기준과 30fps, 144fps, 20~144fps 무작위, 2배속·슬로우·멈칫 패턴의 모든 틱 상태가 같은지 |
| `SimulationDeterminismTests.CommandHistoryReplaysTheSameBattle` | `CommandHistory`를 다시 넣으면 같은 판이 재현되는지 |
| `SimulationPurityTests.SimulationFilesAvoidFrameAndSceneDependencies` | 전투 로직 파일에 금지 API가 있는지. 주석과 문자열은 무시하고 파일·줄·이유를 알려준다 |

테스트의 유닛 수치는 카탈로그와 무관하게 고정해서, 밸런스를 바꿔도 결정론만 검사한다. 전투 로직 클래스는 `internal`이므로 `Assets/02_Scripts/00_Core/AssemblyInfo.cs`에서 에디터 어셈블리에만 공개한다.

## 남은 단계

1. **어셈블리 분리**: 전투 로직을 별도 폴더와 asmdef로 옮겨, 화면 코드를 참조하면 컴파일 에러가 나게 한다. 동시 작업이 줄었을 때 진행한다.
2. **리플레이 저장**: 벽 배치와 `CommandHistory`를 파일로 저장·재생한다.
3. **Unity와 BalanceSim 일치 확인**: 같은 리플레이의 최종 해시를 비교한다.

## 밸런스 측정에 미치는 영향

이전 `BalanceSim`은 프레임 시간을 무작위로 흔들어 결과의 분포를 냈다. 이제 같은 조건이면 결과가 하나이므로 `SEEDS`는 기본 1이고, 분포는 벽 배치(`LAYOUTS`)와 마법 선택의 차이로만 생긴다. 고정 틱 이전에 잰 수치(`balance.md`의 8라운드 결과)는 다시 측정해야 한다.
