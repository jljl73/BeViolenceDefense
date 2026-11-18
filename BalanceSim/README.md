# BalanceSim — 1스테이지 진영 밸런스 시뮬레이터

Unity 없이 실제 전투 코드(`DefenseBattle`, `CombatPositioning`, `DefenseGrid`, `EnemyUnit`)를 그대로 컴파일해서
고블린과 좀비가 마법 없이 싸웠을 때의 결과를 반복 집계한다. Unity API는 `UnityStubs.cs`의 최소 구현으로 대신한다.

## 실행

.NET 8 SDK가 필요하다.

```bash
cd BalanceSim
# 두 유닛만 맞붙이기: <A 체력 공격력 간격 수> <B 체력 공격력 간격 수>
dotnet run -c Release -- 50 10 0.65 6 90 15 1.0 4

# 좀비 수치 훑기: sweep <고블린 4값> <좀비 간격> <체력 최소> <체력 최대> <공격력 최소> <공격력 최대>
dotnet run -c Release -- sweep 50 10 0.65 6 1.0 80 120 14 20

# 라운드 편성 검증: EnemyCatalog.asset 의 유닛 수치와 GameScene 의 Rounds 를 그대로 읽는다
dotnet run -c Release -- rounds
# 편성 후보 비교: 한 줄에 "메모 | coral-id*수, id*수 | teal-id*수"
dotnet run -c Release -- rounds candidates.txt

# 결정론 검증: 여러 프레임 패턴(30/60/144fps, 들쭉날쭉, 배속·슬로우)에서 틱마다 상태 해시가 같은지 확인 (인자는 라운드 번호)
dotnet run -c Release -- determinism 1

# 교전 구도와 "한쪽에만 마법 걸기" 측정 (1대1/난전 비율, 반경별 조준 허용 범위, 구도별 승패)
dotnet run -c Release -- mix 50 10 0.65 6 90 15 1.0 4
```

환경 변수
- `LAYOUTS` (기본 200): 벽 없음 1개 + 무작위 벽 배치 수
- `SEEDS` (기본 1): 배치마다 반복 실행 횟수. 전투가 고정 틱이라 같은 입력이면 결과가 같으므로 보통 1이면 된다
- `RANGE` (기본 1.5): 두 진영 공통 공격 사거리. 씬 값과 맞춘다
- `WALLS` (예: `"5,7;8,7"`): 무작위 배치 대신 지정한 플레이어 벽 배치 하나만 쓴다. 빈 문자열이면 벽 없음
- `MAP` (파일 경로): 17줄 글자 맵으로 지형을 바꿔 시험한다. 없으면 `GameScene`의 `boardLayout`을 쓰고, 그것도 없으면 기본 지형

## 결과 읽기

- **남은 체력 비율 차**: 라운드가 끝났을 때 (고블린 남은 체력 / 고블린 전체 체력) − (좀비 남은 체력 / 좀비 전체 체력). 0에 가까울수록 비슷하게 소모되었다는 뜻이다.
- **승·무승부**: 위 차이가 ±0.2를 넘으면 승패, 아니면 무승부로 센다.
- 체력·공격력이 정수라 "몇 대에 쓰러지는지"가 바뀌는 지점에서 결과가 계단처럼 바뀐다. 한 값만 보지 말고 주변 값도 함께 확인한다.
