# 좁은 길 떨림·타깃 교체 정리 — 검증 자료 (2026-11-19)

설명은 [군중 전투 문서](../../../docs/crowd-combat.md)의 "좁은 길의 떨림과 타깃 교체 정리" 절에 있다.

| 파일 | 내용 |
| --- | --- |
| `change.patch` | 이번 작업의 변경분만 담은 패치. 작업 시작 시점의 작업 트리(커밋되지 않은 이전 변경 포함) 대비 |
| `source-hashes.txt` | 검증에 쓴 네 파일의 SHA-256 |
| `editmode.xml`, `unity-results.txt` | Unity 6000.3.28f1 EditMode 결과(64/64)와 수정 도중 실패한 실행 요약 |
| `JitterBenchmark.cs` | 전투 코드를 Unity 밖에서 돌려 방향 전환·타깃 교체·교전 시간을 세는 계측 코드 |
| `benchmark-before.txt`, `benchmark-after.txt` | 24개 전투의 전후 출력 |
| `crowd-before.txt`, `crowd-after.txt` | 진영당 9·20·40·60명 밀집 전투 8개의 전후 출력(제한 20,000틱) |
| `OutsideUnityTestRunner.cs`, `tests-*-outside-unity.txt` | Unity 내장 `nunit.framework.dll`로 EditMode 검사 일부를 Unity 밖에서 실행한 결과 |

## 계측 열

- `engaged`: 타깃을 공격할 수 있는 상태였던 유닛·초. `sees-noatk`: 인지 거리 안에 벽에 가리지 않은 적이 있는데 공격 가능 상태가 아니었던 유닛·초이며 `noTgt`/`queue`/`appr`는 그 내역(타깃 없음/대기/접근)이다.
- `rev`: 직전 이동 방향과 100도 넘게 달라진 이동. `rapid`: 같은 유닛의 직전 전환 후 30틱 안에 다시 일어난 전환. `hflip`: `Heading`이 100도 넘게 바뀐 틱.
- `switch`: 살아 있는 타깃을 두고 다른 타깃으로 바뀐 횟수. `wiggle`: 1초 구간의 이동 거리에서 순 변위를 뺀 값의 합.
- `minClr`: 두 유닛 중심 거리에서 필요한 간격을 뺀 최솟값. `cross`: 벽을 통과한 이동이 있으면 `X`.

## 재현

`JitterBenchmark.cs`, `BalanceSim/UnityStubs.cs`와 `DefenseBattle`, `DefenseGrid`, `EnemyUnit`, `CombatPositioning`, `BattleClock` 소스를 한 콘솔 프로젝트로 컴파일한다. 카탈로그와 스테이지 에셋은 `BEVD_ASSETS` 환경 변수(없으면 이 폴더 기준 상대 경로)의 Assets 폴더에서 읽는다. 인자 `all`은 24개 전투, `crowd40`은 밀집 전투, `corr2/W1`처럼 이름을 주면 한 전투의 전환·교체 기록을 출력한다. 호스트에 .NET 8 타기팅 팩이 없어 임시 프로젝트를 net10.0으로 만들었다. 저장소의 프로젝트 설정은 바꾸지 않았다.

## 한계

- Unity 밖 실행은 `UnityStubs.cs`의 수학 구현을 쓰므로 Unity와 부동소수 결과가 조금 다르다. 같은 전투가 한쪽에서만 교착되는 경우가 있었다. 최종 판단은 Unity EditMode 결과로 했다.
- 실제 씬 Play Mode, 화면 캡처, Windows 빌드는 수행하지 않았다.
