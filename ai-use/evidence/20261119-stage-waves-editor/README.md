# StageWaves 편집기 검증 / 결과물 목록

- 검증일: 2026-11-19 (Asia/Seoul)
- 환경: feature/first-stage, Unity 6000.3.28f1, Unity CLI 1.0.0, URP 17.0.1, Input System 1.19.0, 기존 Editor IMGUI / 게임 uGUI.
- 기존 `StageWaves`, `EnemyCatalog`, `StageData`의 데이터 형식과 전투 로직은 변경하지 않음.

## 확인 결과

- `compile.json`: 최종 컴파일 오류 0, 경고 0.
- `tests.json`: 관련 EditMode 테스트 12개 전부 통과. 신규 편집 테스트 7개 + 기존 StageWaves 테스트 5개.
- `editor.png`: 실제 기존 StageTEST_Waves를 연 편집 화면. 팩션별 유닛 선택 목록도 Computer Use로 열어 확인.
- 임시 복사본 `Assets/__WaveEditor_QA.asset`에서 후보 그룹 복제 버튼 실행 → 2개 그룹의 확률 각 50%, 총병력 10명 유지. 저장 버튼 실행 뒤 AssetDatabase 재임포트 → 그룹 2개, 각 수량 6, dirty=false 확인. 임시 에셋과 meta는 검증 후 AssetDatabase로 삭제함.
- `play-frames.json`: GameScene Play Mode의 프레임 카운터 2237 → 2247, 조건 충족. 전투 전체 클리어 플레이는 별도로 수행하지 않음.
- `play-readonly.png`: Play Mode 동안 편성 변경 버튼과 입력 비활성화.
- `play-console.json`: 이 작업 중 해결된 초기 CS0117 기록이 CLI 누적 버퍼에 남아 있으나 현재 Unity Console은 오류·경고 0, compilationFailed=false. 초기 비공개 API 사용을 제거한 후 재컴파일로 해결함.
- `final-console.json`: 수정 후 커서 이후 새 오류 0. 최종 Unity Console 오류·경고 0.
- `final-state.json`: 원본 StageTEST_Waves dirty=false, 첫 팩션 후보 1개 유지, 임시 에셋 삭제 완료, Play Mode 종료.
- UI 입력 자동화에서는 별도 팝업에 키보드 포커스를 유지하지 못해 검색어 입력·결과 선택의 전체 UI 경로는 미확인. 선택기는 Unity 기본 AdvancedDropdown을 사용하고 팩션별 선택 목록 표시까지 확인함.
- Git diff로 기존 밸런스 에셋, 씬, 패키지 변경이 없음을 확인. Player 빌드는 수행하지 않음.

## 결과물 목록

경로는 저장소 기준. 아래 소스·문서는 Codex가 생성/수정했고 현재 작업 트리에 적용했다. 최종 사람 검토·채택 여부는 확인 필요.

| 경로 | 구분 | 내용 |
| --- | --- | --- |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/Editor/StageWavesEditing.cs | 생성 | 직렬화 추가·복제, 후보별 병력 범위, 전체 편성 검사 |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/Editor/StageWavesEditorWindow.cs | 생성 | 전용 창, 목록/상세 편집, 검색 선택기, 저장, Play Mode 잠금 |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/Editor/StageWavesEditor.cs | 생성 | 커스텀 Inspector 진입점과 고급 원본 보기 |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/Editor/StageEditorWindow.cs | 수정 | StageData 웨이브 참조 편집과 전용 창 연결 |
| ProjectBeViolenceDefense/Assets/09_Tests/Editor/StageWavesEditingTests.cs | 생성 | 편집 동작 회귀 테스트 7개 |
| 위 신규 C# 4개 파일의 .meta | 생성 (Unity Editor) | 신규 스크립트 GUID |
| docs/stage-waves-editor.md | 생성 | 사용법, 규칙, 공개 UI 참고 출처 |
| ai-use/evidence/20261119-stage-waves-editor/ | 생성 (Codex + Unity CLI) | 본 목록, 컴파일/테스트/콘솔/상태 JSON, 화면 PNG |
| ai-use/ppzepi.md | 추가 | 이번 작업의 한국어 AI 사용 기록 |

UI 참고: [2D Tower Defense Maker — Wave Timeline](https://hyunter777.github.io/2d-tower-defense-maker/editor-tools/#wave-timeline). 공개 문서의 목록/상세, 복제/재정렬, 검사 후 이동 흐름을 참고했고 외부 에셋·코드는 설치하거나 복제하지 않았다.
