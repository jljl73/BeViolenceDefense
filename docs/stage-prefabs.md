# 스테이지 선택과 프리팹 구조

플레이 진입 씬은 `Assets/01_Scenes/02_Gameplay/GameScene.unity` 하나다. Build Settings의 기존 GameScene 항목과 GUID를 유지한다. 실행하면 선택 팝업을 먼저 표시하고, 버튼을 누른 뒤 선택한 스테이지를 생성한다.

## 구성

- **GameScene**: 공통 UI Canvas·EventSystem·전투 컨트롤러·StageSelectionController·선택 화면 카메라.
- **StageSelectionPopup.prefab**: 기본 전장·단풍·설원·묘지 4개 버튼. 기존 UIManager의 Popup 레이어와 모달 입력 차단을 사용한다. `UIPalette` 색상과 기존 패널 스프라이트를 사용한다.
- **StageContent 프리팹**: StageData, BoardTheme, 카메라, 조명, Global Volume, 환경 장식과 StageAmbience. 전투 컨트롤러·공통 HUD·EventSystem을 복제하지 않는다.
- **VoxelDefensePrototype**: 공통 씬에서는 선택 전 초기화를 대기한다. `InitializeStage`에서 선택한 설정을 받아 기존 전투/HUD를 만든다. 별도의 기존 테스트 씬은 이전처럼 Awake에서 시작한다.

## 네 스테이지

경로는 `ProjectBeViolenceDefense/` 기준이다.

| 버튼 | 프리팹 | 지형 데이터 | 이관 원본 |
| --- | --- | --- | --- |
| 기본 전장 | Assets/03_Prefabs/05_Stages/Stage_Default.prefab | 기존 GameScene의 StageData 참조 유지, 25×25 | GameScene |
| 단풍 | Assets/03_Prefabs/05_Stages/Stage_GoldenMaple.prefab | Assets/04_Data/04_Stages/Stage2.asset, 17×17 ([2스테이지 길 구조 시안](level-stage2.md)) | Stage_GoldenMaple |
| 설원 | Assets/03_Prefabs/05_Stages/Stage_Snowfield.prefab | Assets/04_Data/04_Stages/Stage_Snowfield.asset, 17×17 ([설원 길 구조 시안](level-snowfield.md)) | Stage_Snowfield |
| 묘지 | Assets/03_Prefabs/05_Stages/Stage_Graveyard.prefab | Assets/04_Data/04_Stages/Stage_Graveyard.asset, 17×17 ([묘지 길 구조 시안](level-graveyard.md)) | Stage_Graveyard |

테마 씬3개는 원래 StageData가 비어 있어 기본 지형을 사용했다. 같은 크기·코어·스폰·장애물 구성을 명시적인 StageData로 옮겼다. 이후 단풍은 같은 크기·코어·스폰을 쓰는 `Stage2.asset`으로 바꿔 연결했고, 기본 지형을 담은 `Stage_GoldenMaple.asset`은 참조 없이 남겨 두었다. 설원과 묘지는 프리팹 참조를 그대로 두고, 같은 에셋의 고정 벽만 새 길 구조로 바꿨다(크기·코어·스폰 유지). 웨이브 에셋은 원래처럼 비어 있으며 기존 무한 증원 규칙을 유지한다. 신규 웨이브나 난이도는 추가하지 않았다. 공통 HUD는 GameScene을 기준으로 통일했다.

원본 `03_Test` 씬들은 비교/테스트용으로 보존했다. 실제 게임 진입과 스테이지 선택은 이 씬들을 로드하지 않는다. `TestScene`과 `GameScene_LeftCoreHud`는 선택 목록에 넣지 않았다.

## 편집 위치

- **지형·스폰·웨이브**: 각 프리팹의 StageContent → Stage 참조를 열어 기존 Stage Editor/Stage Waves Editor에서 편집한다.
- **타일·벽·코어 외형**: StageContent → Theme.
- **카메라·조명·후처리·환경 장식·파티클**: 해당 프리팹의 자식 오브젝트.
- **환경광·안개**: StageContent에 보관한 RenderSettings 값. 프리팹 인스턴스 생성 후 적용한다.
- **선택 목록 순서**: GameScene의 Stage Selection → Stages 배열.
- **표시 이름**: StageContent의 Display Name Key 및 `Assets/Resources/GameText.ko.json`.
- **팝업 배치**: `Assets/03_Prefabs/05_Stages/StageSelectionPopup.prefab`.

시작 화면은 반드시 스테이지를 선택해야 닫힌다. 연속 클릭은 한 번만 처리한다. 재시작/R은 공통 씬을 다시 로드하고 같은 선택을 한 번 복원해 준비 단계로 돌아간다. 선택 값은 저장 파일이나 PlayerPrefs에 기록하지 않는다. 새 Play 세션에서는 초기화한다.

## 확인 범위

4개 버튼 진입, 모달 입력 차단, 유효하지 않은 선택 무시, 중복 클릭 방지, 각 스테이지의 카메라/지형, 전투 시작 후75틱 이상과 유닛 스폰, 4개 스테이지별 재시작 초기화, 공통 UI/EventSystem/AudioListener 중복 방지를 Unity Play Mode에서 확인했다. 스테이지별 전체 웨이브 클리어·플랫폼 빌드는 이번 확인 범위에 포함하지 않았다.
