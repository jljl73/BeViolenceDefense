# 정체 시 타깃 재선정 검증

- Unity 6000.3.28f1 검증 복사본 `/tmp/be-victory-validation`: EditMode **30/30**, 실제 GameScene Play Mode **코어 118, 생존 적 0, 다음 준비 단계**. 반경 기준 최소 여유 0.00032칸. 원본은 열린 에디터와 충돌하므로 복사본에서 실행했다.
- 검증한 세 전투 파일과 최종 소스가 같은지 확인했다. `source-hashes.txt`에 수정 전후 해시, `combat-change.patch`에 이번 변경만 저장했다.
- `unity-validation.txt`는 두 실제 Unity 로그의 결과 및 Search 인덱서 예외 발췌다. C# 컴파일 오류 없음. `editmode.xml`이 전체 NUnit 결과다.
- `StalledRetargetPlayValidation.cs`는 복사본 Editor 폴더에만 배치한 검사 코드다. `combat.png`는 해당 실행의 카메라 확대 캡처다.

## 정체 재현 비교

`RetargetProbe.cs`는 앞을 막은 같은 편 5명, 접근 실패 중인 적 1명, 옆의 접근 가능한 적 1명을 고정 배치하고 공격자만 이동한다. 360틱(6초), 8틱마다 계획한다. 수정 전에는 `RETARGET`를 정의하지 않고 이전 세 전투 소스와 링크한다. 수정 후에는 `RETARGET`를 정의하고 현재 소스와 링크한다. `BalanceSim/UnityStubs.cs`, `DefenseGrid.cs`, `BattleClock.cs`도 함께 링크한다. 같은 사례가 Unity `StalledSidestepsSwitchToReachableNeighborAndStartAttacking` 회귀 테스트에 들어 있다.

- 수정 전: 전환/공격 가능 틱 없음(-1), 총 10.2칸을 맴돎.
- 수정 후: 48틱(0.8초)에 옆의 적으로 전환, 78틱(1.3초)에 공격 가능. 그 대상을 계속 유지.
- Probe의 firstAttackTick은 첫 공격 가능 틱이다. Unity 테스트에서는 실제 `Attack`을 호출해 반복 공격까지 확인했다.

## 밀집 전투 비교

`Benchmark.cs`로 각 편 9/20/40명, 벽 없음/4개를 비교했다. 작은 편 반경 0.3, 큰 편 0.48칸. 호스트 .NET 8 타기팅 팩이 없어 임시 net10.0 프로젝트에서만 실행했고 저장소 .csproj는 수정하지 않았다. `dotnet build -c Release --disable-build-servers -p:UseSharedCompilation=false -p:NuGetAudit=false`를 사용했다.

후속 버전 6개 조합 모두 준비 단계로 끝나고 간격을 지켰다. 40명+벽 조합의 .NET 재현은 이전 14,400틱 제한까지 전투 중이었고 이후 10,684틱에 종료했다. 코어 체력은 이전 제한 시점 91, 이후 종료 시점 88이므로 동일 종료 상태의 직접 비교로 해석하지 않는다. 40명+벽 없음은 양쪽 코어 94, 종료 틱 8,128 → 7,996. 나머지 4개 조합 결과는 같았다. 누적 정지 지표에는 정상 대기도 들어 있어 모든 값의 감소를 보장하지 않는다. 이는 Unity 화면 성능이나 OS 간 결정론 측정이 아니다.

Windows 빌드, 모든 맵·모든 유닛 조합, 사용자가 본 원본 장면의 동일 조건 비교는 미확인이다.
