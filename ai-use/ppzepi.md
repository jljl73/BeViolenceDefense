
### [20261118-1816] WoodBlock 임포트 점검 및 벽 프리팹 교체
- 시각: 2026-11-18 18:16:53 +09:00
- 작업자: ppzepi
- 사용 도구: Claude Code / Claude Opus 5.5, Unity CLI(unity pipeline) / Unity 6000.3.28f1
- 목적 및 적용 위치: 추가한 WoodBlock 에셋의 임포트 상태를 점검하고, GameScene 벽 표시를 WoodBlock 프리팹으로 교체
- 사용 방식: AI가 .meta와 Editor.log로 임포트를 점검하고 노멀맵 설정을 수정했다. 이어서 머티리얼과 프리팹을 생성하고 DefenseBoardView.CreateWall에서 프리팹을 로드하도록 바꿨다. 프리팹이 없으면 기존 박스 벽을 그대로 쓴다.
- 결과 및 확인 방법: 성공 / 재컴파일 오류 0건. Play Mode에서 WoodBlock 메시를 쓰는 벽 19개를 확인했고 콘솔 에러는 0건이었다. Game View 캡처로 외형도 확인했다.
- 사람의 개입: 요청 지시. 결과 검토는 확인 필요

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ProjectBeViolenceDefense/Assets/Art/WoodBlock_Normal.png.meta | Claude Code | 수정 | Texture Type을 Normal map으로, sRGB를 off로 변경 / 사용 |
| ProjectBeViolenceDefense/Assets/Art/WoodBlock.mat | Claude Code + Unity CLI | 생성 | URP Lit 머티리얼(BaseColor·Normal 연결) / 사용 |
| ProjectBeViolenceDefense/Assets/Resources/Prefabs/WoodBlockWall.prefab | Claude Code + Unity CLI | 생성 | 크기 0.92×0.8×0.92로 벽 판정 범위에 맞춤 / 사용 |
| ProjectBeViolenceDefense/Assets/Scripts/AssetLoading/AssetPaths.g.cs | Unity(자동 생성기) | 수정 | Prefabs/WoodBlockWall 경로 추가 / 사용 |
| ProjectBeViolenceDefense/Assets/Scripts/DefenseBoardView.cs | Claude Code | 수정 | CreateWall에서 프리팹 사용, 고정 벽은 어두운 틴트 적용 / 사용 |

#### 증거
- 없음 (캡처는 Temp/woodwall.png로, 저장소에 포함되지 않음)

#### Unity CLI 사용
- 구현하거나 수행한 작업: 노멀맵 재임포트, FBX 크기·머티리얼 조사, 머티리얼·프리팹 생성, AssetPaths 재생성, 재컴파일, Play Mode 확인, Game View 캡처
- 활용 방식: 임시 빌더 스크립트를 `script run`으로 실행한 뒤 삭제. `script recompile`, `editor.playmode start/stop`, `capture game_view` 사용
- 잘된 점: 에디터를 켠 채로 에셋 생성부터 런타임 확인까지 이어서 진행할 수 있었다.
- 잘 안된 점 및 개선 필요: capture 옵션 이름(--output-path)을 처음에 잘못 써서 재시도했다.

#### 미확인 및 후속 확인
- 준비 단계에서 플레이어가 직접 벽을 배치·철거하는 동작은 직접 조작해 확인하지 않았다.
- Netmarble Medium SDF.asset과 URPProjectSettings.asset의 변경은 에디터가 만든 것으로 보이며 이번 작업 범위가 아니다. 커밋 여부를 확인해야 한다.
