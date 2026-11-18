
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

### [20261118-1841] LuckyNumber 기준 Assets 폴더 정리
- 시각: 2026-11-18 18:41:00 +09:00
- 작업자: ppzepi
- 사용 도구: Codex / 모델·버전 확인 필요, PowerShell, Unity Editor MCP (Pipeline)
- 목적 및 적용 위치: LuckyNumber/Assets의 번호·역할별 구조를 현재 프로젝트에 적용. Addressables 제외.
- 사용 방식: 기준 폴더와 패키지·씬·코드를 확인하고 AssetDatabase.MoveAsset으로 이동. 기존 GUID와 파일 해시를 이동 전후 비교.
- 결과 및 확인 방법: 폴더 정리 성공. 원본 GUID 누락 0개, 에셋 본문 변경은 AssetPathsGenerator 출력 경로뿐. 컴파일 실패 없음, 씬 Missing Script 0개, Resources 벽 프리팹 로딩 성공. Play Mode에서 게임/UI 초기화 및 프레임 3320→5062, 시간 21.46→33.41초 진행 확인 후 종료. 기존 Verify Enemy Groups Once 검증은 첫 스폰 9명 조건에서 실패; 전체 검증 통과로 판단하지 않음.
- 사람의 개입: 기준 프로젝트와 Addressables 제외를 사용자 지정. 기존 사용자 변경 내용 보존; 추가 직접 개입 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ProjectBeViolenceDefense/Assets/01_Scenes ~ 09_Tests | Codex + Unity Editor MCP | 이동·생성 | 번호별 폴더와 역할 분류 적용. 상세 108개 이동 항목은 docs/asset-folders.md 참고. 사용 중 |
| ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/AssetLoading/Editor/AssetPathsGenerator.cs | Codex | 수정 | 생성 파일 출력 경로 갱신. 사용 중 |
| ProjectBeViolenceDefense/ProjectSettings/EditorBuildSettings.asset | Unity Editor MCP | 경로 확인·갱신 | 기존 씬 GUID로 현재 GameScene 경로 재해석. 사용 중 |
| AGENTS.md, docs/prototype.md, docs/ui-flow.md | Codex | 수정 | 이동된 경로 링크 갱신. 사용 중 |
| docs/asset-folders.md | Codex | 생성 | 폴더 기준 및 실제 이동 목록. 사용 중 |

#### 증거
- docs/asset-folders.md
- Unity MCP 세션 결과: GUID 누락 0, missingScripts=0, wall=true, compilationFailed=false. 별도 실행 로그 파일 없음.

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex의 파일 검사·수정 및 Unity MCP 호출.
- 사용 방식: 폴더 비교, 에셋 이동, 경로 갱신, 해시·GUID 검증, Play Mode 확인.

#### 주요 프롬프트
- 사용 도구: Codex
- 프롬프트 원문: C:\Users\Choij\Documents\Projects\Unity\LuckyNumber\Assets 해당 폴더의 폴더링 구조를 보고 현재 프로젝트도 유사하게 폴더링한다. 단 Addressable은 제외한다.
- 결과 및 실제 활용 방식: 01_Scenes~09_Tests 분류를 적용하고 Resources와 외부 플러그인 위치를 유지.
- 선정 이유: 폴더 정리 범위와 Addressables 제외를 결정한 요청.

#### 미확인 및 후속 확인
- Unity CLI는 unity --version 실행 시 PATH에서 찾지 못함. CLI 컴파일·빌드 미실행. Unity Editor MCP로 컴파일 상태와 Play Mode를 확인.
- 기존 스폰 검증 메뉴의 첫 조건 실패 원인은 이번 폴더 정리 범위에서 수정하지 않음. 관련 런타임·검증 코드 내용은 이동 전후 동일.
- 최초 일괄 이동 중 폴더 등록 시점과 기존 생성기 출력 경로로 오류 발생. 순차 이동과 출력 경로 수정 후 완료. 재생성된 중복 파일은 본문 해시 일치 확인 후 개별 삭제.
- 자동 승인 검토가 Scripts 폴더 전체 삭제를 증거 부족으로 거절. 이후 중복 파일 동등성과 빈 폴더 여부를 확인하여 제한된 삭제 완료. 남은 승인 대기 없음.

### [20261118-1851] TestScene 복제 및 보드 테마 색상 설정 (1단계)
- 시각: 2026-11-18 18:51:00 +09:00
- 작업자: ppzepi
- 사용 도구: Claude Code / Claude Opus 5.5 (claude-opus-5-5), Unity Editor MCP (Pipeline)
- 목적 및 적용 위치: Emberward 레퍼런스(가을 숲) 기반 TestScene 배경·타일 구성의 1단계. GameScene을 TestScene으로 복제하고, 코드로 생성되는 보드 색을 씬별로 바꿀 수 있게 함.
- 사용 방식: 레퍼런스 스크린샷과 기존 보드 생성 코드를 확인한 뒤 사용자에게 테마(가을 숲)·소품 제작 방식(코드 로우폴리)·진행 방식(단계별)을 선택받음. AssetDatabase 복사로 새 GUID의 TestScene 생성, BoardTheme 직렬화 클래스 추가, TestScene에만 가을 팔레트와 따뜻한 조명색 적용.
- 결과 및 확인 방법: 성공. 컴파일 실패 없음. TestScene Play Mode 캡처에서 갈색 타일·밝은 경로 타일·금색 예상 교전 타일·따뜻한 배경 확인. GameScene Play Mode 캡처에서 기존 남색 배경·회색 타일 유지 확인(BoardTheme 기본값 = 기존 상수).
- 사람의 개입: 테마·제작 방식·단계별 진행을 사용자가 선택. 색 수치는 AI 임시 선택, 사람 검토 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ProjectBeViolenceDefense/Assets/01_Scenes/03_Test/TestScene.unity | Unity Editor MCP | 생성(복제) | GameScene 복제. boardTheme 가을 팔레트, Directional Light 색 #FFE0B5. 검토 중 |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/BoardTheme.cs | Claude Code | 생성 | 배경·바닥·경로·교전·벽·장식 색 설정 클래스. 검토 중 |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/DefenseBoardView.cs | Claude Code | 수정 | 하드코딩 색을 BoardTheme 값으로 교체. 검토 중 |
| ProjectBeViolenceDefense/Assets/02_Scripts/00_Core/Flow/VoxelDefensePrototype.cs | Claude Code | 수정 | boardTheme 직렬화 필드 추가 후 보드 뷰에 전달. 검토 중 |
| ProjectBeViolenceDefense/ProjectSettings/EditorBuildSettings.asset | Unity Editor MCP | 수정 | TestScene을 빌드 목록에 추가(R 재시작이 buildIndex를 사용). GameScene 경로는 에디터가 GUID로 자동 갱신. 검토 중 |

#### 증거
- 없음. Play Mode 캡처는 세션 임시 폴더에만 저장.

#### 미확인 및 후속 확인
- Unity CLI는 사용하지 않음. 컴파일·Play Mode 확인은 Unity Editor MCP로 수행.
- 에디터 콘솔에 에러 2건이 표시되나 캡처 버퍼에는 이번 작업 이후 새 항목이 없음. 이전 폴더 정리 작업에서 남은 것으로 보이나 확인 필요.
- 2단계(타일 형태·질감), 3단계(지면·나무·안개·후처리)는 미구현.

### [20261118-1900-main-wall-rollback] 메인 씬 벽 외형 선택 복원
- 시각: 2026-11-18 19:00:00 +09:00
- 작업자: ppzepi (git config user.name)
- 사용 도구: OpenAI Codex / GPT-6, Unity CLI 1.0.0, Unity Editor 6000.3.28f1
- 목적 및 적용 위치: af8883f의 나무 벽 적용을 메인 GameScene에서 되돌리고 TestScene의 나무 벽을 유지.
- 사용 방식: 커밋과 실제 씬·공유 코드 확인 후 BoardTheme에 선택적 wallPrefab을 추가. 미지정 시 기존 블록·캡 생성, TestScene에 기존 WoodBlockWall 연결.
- 결과 및 확인 방법: 성공 / Unity 컴파일 오류·경고 0. 두 씬 Play Mode에서 프레임 진행, 고정 벽·배치 벽 메쉬, 배치·철거 확인. TestScene 고정 벽 색 유지. 새 Console 오류 없음. GameScene 및 프리팹·meta 파일 변경 없음.
- 사람의 개입: 사용자가 롤백 범위와 TestScene 유지 조건 지정. 결과의 사람 검토 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/BoardTheme.cs | Codex | 수정 | 씬별 선택적 벽 프리팹 필드 추가, 적용 |
| ProjectBeViolenceDefense/Assets/02_Scripts/05_World/DefenseBoardView.cs | Codex | 수정 | 전역 나무 벽 로드 대신 테마 프리팹 사용, 미지정 시 기존 외형, 적용 |
| ProjectBeViolenceDefense/Assets/01_Scenes/03_Test/TestScene.unity | Codex + Unity CLI/Editor | 수정 | 기존 WoodBlockWall 프리팹 참조 1줄 추가로 기존 외형 유지, 적용 |
| ai-use/evidence/2026-11-18-wall-rollback/ | Codex + Unity CLI | 생성 | 검증 C# 및 컴파일·Play Mode·Console·최종 상태 JSON, 검증 증거로 사용 |
| ai-use/ppzepi.md | Codex | 추가 | 본 작업 기록, 적용 |

#### 증거
- ai-use/evidence/2026-11-18-wall-rollback/compile.json
- ai-use/evidence/2026-11-18-wall-rollback/testscene.json
- ai-use/evidence/2026-11-18-wall-rollback/gamescene.json
- ai-use/evidence/2026-11-18-wall-rollback/gamescene-frames.json
- ai-use/evidence/2026-11-18-wall-rollback/console.json
- ai-use/evidence/2026-11-18-wall-rollback/final-editor-state.json
- ai-use/evidence/2026-11-18-wall-rollback/validate-walls.cs

#### Unity CLI 사용
- 구현하거나 수행한 작업: 연결 상태·오류 기준선 조회, 컴파일, TestScene 프리팹 연결·저장, 두 씬 Play Mode 검증, 원래 씬 복귀.
- 활용 방식: Codex가 Git 조사·코드 수정을 담당하고 CLI의 recompile, script eval, scene open, editor.playmode, job wait, console get으로 Editor 작업·검증 수행.
- 잘된 점: 실제 생성된 벽 메쉬와 고정 벽 색을 확인했고 프레임 진행·벽 배치·철거를 양쪽 씬에서 검증. 종료 시 TestScene 편집 모드로 복귀하고 빌드 씬 목록을 작업 전 상태로 복원.
- 잘 안된 점 및 개선 필요: 기본 PATH에서 CLI가 검색되지 않아 설치 경로로 실행. 샌드박스 밖 설치 CLI 실행에는 권한 확장이 필요했음. 일회성 검증 코드의 색상 형변환 오류와 스폰 칸 배치 실패를 수정 후 재검증 통과. CLI 1.0.1 업데이트 확인했으나 도구 업데이트는 수행하지 않음.

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex, 위 코드·씬·검증 기록.
- 사용 방식: 사용자 요청 해석, 변경 범위 조사, 최소 코드 수정, Unity CLI 검증, 결과 기록.

#### 주요 프롬프트
- 사용 도구: Codex
- 프롬프트 원문: 커밋 기록 af8883f7ba6c26a62c82fd1839407090798e0b68 에 적용했던 MainScene의 벽 프리팹을 변경했던 것을 롤백해줘. TestScene에 적용된 벽은 그대로 두고.
- 결과 및 실제 활용 방식: 실제 메인 씬 파일명 GameScene 확인 후 씬별 외형 설정으로 요청 적용.
- 선정 이유: 메인 씬만 복원하고 TestScene 외형을 유지해야 하는 변경 범위를 지정.

### [20261118-merge-conflicts] 진행 중인 main 병합 충돌 해결
- 시각: 2026-11-18 19:09:53 +09:00
- 작업자: ppzepi
- 사용 도구: Codex / GPT-6, Git, Unity MCP
- 목적 및 적용 위치: feature/first-stage의 main 병합 충돌 13개 경로 해결.
- 사용 방식: 양쪽 변경과 실제 Unity 설정을 확인하고 현재 폴더 구조·보드 테마와 main의 전투 기능·모델·임시 밸런스를 통합했다. 기존 자동 병합 내용은 유지했다.
- 결과 및 확인 방법: 성공 / Unity 6000.3.28f1 컴파일 오류 0개, 기존 적 그룹 검증 통과, GameScene 참조 및 Play Mode 전투·2배속·조준 일시정지/재개 검증 통과. 신규 Console 오류·경고 없음.
- 사람의 개입: 사용자가 충돌 해결 실행을 요청함. 개별 결과의 수동 검토 여부는 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| 충돌 13개 경로 — [전체 목록](evidence/20261118-merge-conflicts.md) | Codex / Git | 4개 내용 통합·9개 수신 파일 채택 | 재배치 경로와 두 브랜치 기능 유지, 작업 트리에 적용 |
| ai-use/evidence/20261118-merge-conflicts.md | Codex | 생성 | 파일별 해결 내역·실제 검증·제한 기록, 사용 |
| ai-use/ppzepi.md | Codex | 추가 | 이번 작업 기록, 사용 |

#### 증거
- [충돌 해결 및 Unity 검증 기록](evidence/20261118-merge-conflicts.md)

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex / GPT-6, 저장소 충돌 분석·코드/문서 통합·검증 기록.
- 사용 방식: Git 양쪽 버전 비교, 생성자와 호출부 통합, Unity MCP로 실제 컴파일 및 전투 검증.

#### 주요 프롬프트
- 사용 도구: Codex
- 프롬프트 원문: 현재 발생한 깃 Conflict를 네 선에서 해결 가능한가? 가능하다면 실행해
- 결과 및 실제 활용 방식: 충돌 13개 경로를 직접 해결하고 Unity에서 검증했다.
- 선정 이유: 이번 충돌 해결 실행의 직접 요청.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 셸 PATH에 Unity CLI가 없어 `unity --help` 실행 불가, Unity MCP로 검증함. Windows Player 빌드·수동 입력 전체 플레이·BalanceSim 독립 실행은 미검증. 상세 제한은 증거 문서 참고.
