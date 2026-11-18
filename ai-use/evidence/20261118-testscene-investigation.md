# TestScene 실행 조사 — 2026-11-18

## 환경 및 범위
- Unity 6000.3.28f1, Pipeline 1.0.0, Unity CLI 1.0.0, Input System 1.19.0.
- CLI 1.0.1 업데이트가 있음을 확인했으나 업데이트는 수행하지 않았다.
- URP, New Input System, uGUI. 조사 시작 당시 TestScene이 열려 있고 Play Mode는 중지 상태였다.
- 기존 미커밋 변경은 보존했다. 게임 코드·씬·패키지·프로젝트 설정을 수정하지 않았다.

## 재현 및 관찰
1. CLI로 임시 auto-tick 및 run-in-background를 활성화하고 TestScene Play Mode 진입.
2. frameCount 334 → 3188 → 6205 → 10501로 증가. 초기화된 grid/view/battle/hud/ui/boardCamera가 모두 존재.
3. 현재 컴파일 실패 없음. Console cursor 21 이후 이번 재현에서 새 오류 없음. 버퍼에 남은 과거 import loop·CS0101·검증 실패는 현재 오류와 구분했다.
4. InputSystem.devices.Count=0, Keyboard.current=null, Mouse.current=null. Application.isFocused=true, Editor pause=false. 네이티브 런타임은 NativeInputRuntime이며 available device 수도 0.
5. 설정상 new input backend=true, old=false. UI 입력 모듈 활성화, DefaultInputActions의 Point와 Click 액션도 활성화됨.
6. VoxelDefensePrototype.StartBattle을 리플렉션으로 직접 한 번 호출: Preparation → Combat.
7. 이후 Round=2, CurrentPhase=Preparation, SpawnCount=10, EnemiesPerRound=10, CoreHealth=118/120, 생존 적=0. 전투 시뮬레이션이 진행되어 라운드가 종료됨을 확인.
8. 종료 후 Play Mode stopped, auto-tick=false, runInBackground=false로 복원.

## 결론과 한계
- 확인된 직접 원인: 현재 에디터 세션의 Input System에 키보드·마우스 장치가 없어 Space, 보드 클릭, UI 클릭을 통한 조작이 전달될 수 없다. 전투 시작 함수를 직접 호출하면 전투는 진행된다.
- 장치가 발견되지 않는 더 깊은 원인은 미확정. 설정 활성화 후 에디터 재시작 누락 또는 네이티브 입력 초기화 이상 가능성이 있으며, 재시작 후 장치 인식과 실제 물리 입력 재검증이 필요하다. 이번 조사에서는 에디터를 재시작하지 않았다.
- 별도 정적 결함: EditorBuildSettings.asset에는 GameScene만 등록되어 있으나 Restart는 SceneManager.LoadScene(gameObject.scene.buildIndex)를 사용한다. 빌드 목록에 없는 TestScene의 인덱스를 이용한 재시작은 유효하지 않다. 이번 조사에서 Restart 호출은 하지 않았다.
- TestScene의 적 모델·애니메이션 참조는 비어 있다. 이는 모델 표현 차이이며 전투 진행 중단의 원인은 아니다.
- 실제 키보드·마우스 입력과 독립 실행 빌드는 검증하지 않았다.

## 근거 위치
- Assets/02_Scripts/00_Core/Flow/VoxelDefensePrototype.cs:43,65,131,132,239
- ProjectSettings/EditorBuildSettings.asset:7
- 스크린샷: [2라운드 준비 화면](20261118-testscene-investigation.png)
- Input System 1.19 설치/백엔드 활성화 안내: https://docs.unity.cn/Packages/com.unity.inputsystem%401.19/manual/Installation.html
