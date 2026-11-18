# 4개 스테이지 프리팹 이관 검증

사용자 확인: GameScene + Stage_GoldenMaple + Stage_Snowfield + Stage_Graveyard.

- 원본 프로젝트를 `/tmp/be-stage-prefab-validation`로 복제하여 Unity6000.3.28f1 CLI로 에셋 생성·검증 후 적용했다.
- `StagePrefabMigration.cs`: 이관 당시 사용한 Editor 스크립트. 원본 씬이 있는 복제 프로젝트용 기록이며, 이미 변환된 GameScene에서 다시 Run하지 않는다. 첫 저장에서 선택 목록의 Unity Object 참조가 비어 EditMode 검사가 실패했으며, 저장된 에셋 경로에서 프리팹을 다시 읽어 연결한 뒤 최종3/3 통과했다.
- `StageSelectionPlayValidation.cs`: 실제 GameScene에서 버튼별 선택, 선택 전 초기화 차단, 모달 입력 차단, 잘못된 인덱스, 연속 클릭, 환경/카메라/지형, UI/EventSystem/AudioListener 개수,75틱 전투 진행/스폰, 동일 스테이지 재시작 초기화를 검사한다.4개 모두 통과했다.
- `selection.png`: 시작 팝업. `stage-0~3.png`: 기본/단풍/설원/묘지 준비 화면. UI 캡처를 위해 Canvas를 잠시 Screen Space Camera로 바꾸어 오프스크린 렌더링했다. 런타임 설정은 Overlay를 유지한다.
- 기존3개 테마 씬은 StageData가 없었다. 17×17 기본 지형을 각각 명시적인 에셋으로 만들었고, 임의 웨이브는 만들지 않았다.
- 기존 테스트 씬과 다른 작업에서 생성된 Stage2.asset은 수정하지 않았다.
- Unity SearchDatabase 시작 색인 예외가 있었으나 스테이지 런타임 예외는 관찰되지 않았고 검사 완료.
- 전체 전투/웨이브 클리어, Windows 빌드는 미실시. 전투 모델 코드는 변경하지 않았다.

최종 결과물 경로는 output-manifest.txt, 내용 해시는 source-hashes.txt를 참고한다.
