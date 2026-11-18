# 20261118-merge-conflicts — 충돌 해결 및 검증

- 대상: `feature/first-stage`의 진행 중인 `main` 병합.
- 시작 커밋: HEAD `7cc9843`, MERGE_HEAD `8a84d79`.
- Unity: `6000.3.28f1`, URP, Input System, uGUI. 에디터·패키지 설정 변경 없음.
- 작성 도구: Codex / GPT-6, Git, Unity MCP.

## 충돌 해결 결과물

아래 13개 경로를 해결했다. 새 기능을 별도로 구현하지 않고 두 브랜치의 변경을 통합했다.

| 경로 | 적용 내용 |
| --- | --- |
| `AGENTS.md` | 현재 UI 경로와 main의 BattleClock 사용 규칙을 함께 유지 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/Flow/VoxelDefensePrototype.cs` | BoardTheme, 고블린 프리팹, main의 임시 팩션 밸런스를 함께 유지하고 생성자 호출 통합 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/05_World/DefenseBoardView.cs` | 생성자에서 BoardTheme와 고블린 프리팹을 함께 받도록 통합 |
| `docs/ui-flow.md` | 재배치된 HUD 경로와 main의 실제 HUD 표시 규칙을 함께 유지 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/BattleClock.cs` | main 원문을 재배치된 경로에 채택 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/BattleClock.cs.meta` | main 메타데이터·GUID 보존 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/CombatPositioning.cs` | main 원문을 재배치된 경로에 채택 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/CombatPositioning.cs.meta` | main 메타데이터·GUID 보존 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/Effects.meta` | main 폴더 메타데이터·GUID 보존 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/Effects/HitEffect.cs` | main 원문을 재배치된 경로에 채택 |
| `ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/Effects/HitEffect.cs.meta` | main 메타데이터·GUID 보존 |
| `ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500.prefab` | main 프리팹 원문 채택 |
| `ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Enemies/Goblin_Grunt_1500/Goblin_Grunt_1500.prefab.meta` | main 메타데이터·GUID 보존 |

후반 9개 파일은 `git hash-object`와 인덱스 stage 3의 해시가 모두 일치했다. 원문과 메타데이터를 수정하지 않았다. 이미 자동 병합된 나머지 파일은 유지했다.

## 실제 검증

- Unity MCP `script.recompile`: `status=completed`, `failed=false`, `errors=[]`, `compilationFailed=false`.
- 기존 메뉴 `Tools/Verify Enemy Groups Once`: `ENEMY_GROUPS_PASS: contacts=4, kills=14, forecast=6`. 스폰 간격, 시야·추격 차단, 공격자 상한, 유닛 간격, 라운드 정산 검증 통과.
- GameScene 직렬화 검사: Missing Script 0개, 필수 참조 7개 유효, BoardTheme 존재, 고블린 6·좀비 4 설정 유지. 고블린 프리팹은 재배치된 경로로 정상 해석.
- GameScene Play Mode: 보드·코어 생성 및 전투 시작 확인. 프레임 895→2015, 게임 시간 7.207962→17.0425682초로 진행했으며 생존 적 9개와 Animator 9개 확인.
- 2배속 `StepCount=2`, 조준 중 `DeltaTime=0`, 취소 후 `StepCount=2`로 재개 확인.
- 검증 후 Console: 신규 경고·오류 없음, 현재 Console 오류 0개·경고 0개, 컴파일 실패 없음. 기존 Console 기록은 지우지 않았다.
- Play Mode 종료 후 원래 열려 있던 `Assets/01_Scenes/03_Test/TestScene.unity`를 다시 열었다. 씬을 저장하지 않았다.

## 기준 상태 및 제한

- 시작 시 MCP 기록 버퍼에는 이전 에셋 이동 중 import loop·AssetPaths 중복·구버전 검증 예외 이력이 있었지만, 현재 Console 오류·경고는 0개였고 컴파일 실패 상태도 아니었다. 이번 검증 중 재발하지 않았다.
- `unity --help` 실행을 시도했으나 셸 PATH에 Unity CLI가 없어 실행하지 못했다. 설치·업데이트 없이 기존 Unity MCP 연결로 실제 에디터 컴파일과 Play Mode 검증을 수행했다.
- Windows Player 빌드와 수동 입력 전체 플레이 테스트는 수행하지 않았다.
- main에서 수신한 BalanceSim은 추적된 `.csproj`가 없어 독립 실행을 검증하지 않았다. 이번 Unity 충돌 해결 범위에 포함하지 않았다.

## 최종 Git 상태

- 충돌 13개 경로와 작업 기록을 스테이징했다. `git ls-files -u` 0건, 대상 코드·씬·문서의 충돌 마커 0건.
- `git diff --cached --check`는 원문 그대로 채택한 Unity `.meta` 2개 파일의 기존 후행 공백 6줄을 보고했다. 해당 2개 파일을 제외한 검사에는 문제가 없었다. 메타데이터 원문을 보존했다.
- 병합 커밋과 push는 실행하지 않았다. Git 상태는 `All conflicts fixed but you are still merging`이며 병합 커밋을 만들 수 있다.
