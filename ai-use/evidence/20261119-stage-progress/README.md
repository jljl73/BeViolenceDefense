# 스테이지 진행 검증 — 2026-11-19

Unity 6000.3.28f1. 원본 에디터와 사용자 저장 데이터를 보호하기 위해 `/tmp/be-stage-progress-20261119` 복제본에서 검증했다. 패키지, 렌더 파이프라인, 원본 Player Settings는 변경하지 않았다.

## 결과

| 검증 | 결과 | 증거 |
| --- | --- | --- |
| EditMode 전체 | 130개 중 129 통과, 기존 묘지 크기 기대값 테스트 1개 실패 | `editmode-final.xml` |
| 새 진행 테스트 | 10개 모두 통과: 초기 상태, 순차 해금, 마지막 클리어, 전체 해금, 초기화, 구 저장 이관, 잘못된 번호, 유한 웨이브 | `editmode-final.xml`의 StageProgressTests |
| Play Mode 통합 | 성공, 프로세스 종료 코드 0 | `playmode-checks.txt`, `playmode-excerpt.txt` |
| Mac Standalone | Succeeded, 오류 0, 경고 6 | `build-StandaloneOSX.txt`, `mac-build-fresh-cache.log.excerpt.txt` |
| Windows 64 Standalone | Succeeded, 오류 0, 경고 10 | `build-StandaloneWindows64.txt`, `windows-build.log.excerpt.txt` |
| Mac 실제 앱 | 실행 후 Metal/입력/어셈블리 초기화 로그 확인. 화면 클릭 검증은 도구 오류로 미완료 | `mac-player-startup.log` |
| Windows 실제 앱 | Windows 실행 환경이 없어 미검증 | — |

기존 실패는 `StageContentTests.GraveyardLayoutKeepsTheSceneryFrameAndLengthensRoutesWhenItsGatesAreWalled`의 맵 크기 예상 17 / 실제 23 불일치다. 작업 전 빌드 점검에서도 동일하게 발생했다. 이 작업에서 묘지 지형이나 기존 테스트는 수정하지 않았다.

## Play Mode 검증 방식

`StageProgressSmoke.cs.txt`는 검증용 복제본에만 추가한 에디터 실행 코드다. 복제본 StageProgress의 기본 저장 접두사를 `BeViolenceDefense.Validation20261119.Progress.`로 바꾸어 사용자 진행과 분리했다. 빌드 전에 원본 소스로 되돌렸다. 별도 EditMode 테스트도 매번 고유 접두사를 사용한다.

실제 GameScene을 열고 컨트롤러와 팝업을 통해 다음 흐름을 확인했다.

1. 신규 저장은 해안가만 열림. 컨트롤러에 직접 요청해도 잠긴 왕도에 진입하지 못함.
2. 해안가의 마지막 웨이브를 끝내고 실제 VoxelDefensePrototype.Update에서 승리 처리 → 첫 클리어 저장.
3. 결과 창의 스테이지 선택 버튼 → 씬 재로드 → 해안가 클리어, 단풍 열림, 설원 잠김.
4. 단풍 승리 후 이동속도 스킬 해금.
5. 전체 해금 확인 → 다섯 맵 입장 가능, 가짜 클리어와 추가 스킬 해금 없음.
6. 씬 재로드 후 기록 유지. 초기화 취소 시 기록 유지.
7. 초기화 확인 → 클리어·전체 해금·획득 스킬 초기화, 재로드 후에도 해안가만 열림.

검증 시간을 줄이기 위해 테스트 코드에서 적을 제거하고 고정 틱을 진행했다. 마지막 승리 확정은 게임의 실제 Update 경로로 처리했다. 이 결과는 진행/저장 연결 검증이며, 전투 난이도나 일반 플레이로의 클리어 가능성을 보장하는 밸런스 테스트는 아니다.

## 화면

실제 Play Mode 캔버스를 1280×720으로 렌더링한 이미지다.

- `01-fresh.png`: 신규 저장, 첫 맵만 열림.
- `02-management.png`: 진행 관리 메뉴.
- `03-reset-confirmation.png`: 초기화 범위 설명과 취소/확인.
- `05-after-first-clear.png`: 첫 승리 후 선택 화면. 등장 애니메이션 중 캡처되어 일부 글자가 흐리다.
- `06-unlock-confirmation.png`: 전체 해금의 범위 설명.

## 빌드와 제한

`StageProgressBuild.cs.txt`는 복제본에서만 사용한 빌드 코드다. Mac 검증 앱은 회사/제품/앱 식별자를 별도로 지정해 실제 사용자 저장소와 분리했다. Windows 빌드는 원래 식별자를 사용했다. 빌드는 기존 활성 씬, BuildOptions.None으로 실행했다. 결과물은 `/tmp/be-stage-progress-results/mac/StageProgressCheck.app`, `/tmp/be-stage-progress-results/windows/BeViolenceDefense.exe`에 있다. 배포용 결과물은 아니다.

Mac 빌드 시도 두 번은 Burst 내부 CompileMethodGroup에서 비정상 종료했다. 복제본의 `Library/BurstCache`를 별도 임시 경로로 옮겨 재생성한 뒤 Mac과 Windows 빌드가 성공했다. Burst를 끄거나 패키지를 바꾸지 않았다. 근본 원인은 확정하지 않았다. `burst-retry-crash-excerpt.txt`에 스택 일부를 보존했다. Play Mode 로그에는 UnityEditor.Search 초기화 예외도 있으나 게임 검증은 완료됐다.

기존 효과 셰이더 pow 경고 등이 빌드에 남아 있다. 빌드 성공과 별개로 Windows GPU 출력은 실제 기기에서 확인해야 한다. Mac 앱의 네이티브 화면 확인은 CUA가 `unknown or expired node_repl continuation` 오류로 실패해 완료로 취급하지 않았다.

## 변경 파일 목록

모든 Assets 경로 앞에 `ProjectBeViolenceDefense/`가 붙는다. 생성·수정 도구는 Codex의 파일 편집 도구이며, 아래 항목은 작업 폴더에 적용했다.

| 경로 | 구분 | 내용 |
| --- | --- | --- |
| Assets/02_Scripts/00_Core/Flow/StageProgress.cs 및 .meta | 생성 | 실제 클리어, 전체 입장 해제, 초기화, 구 저장 이관 |
| Assets/02_Scripts/00_Core/Flow/StageSpellUnlocks.cs | 수정 | 동일 클리어 기록으로 스킬 해금 |
| Assets/02_Scripts/00_Core/Flow/StageSelectionController.cs | 수정 | 실제 진행 연결, 잠긴 맵 차단, 결과에서 선택 화면 복귀 |
| Assets/02_Scripts/00_Core/Flow/VoxelDefensePrototype.cs | 수정 | 승리 결과에 선택 화면 복귀 콜백 연결 |
| Assets/02_Scripts/06_UI/StageSelectionPopup.cs | 수정 | 임시 클리어 제거, 실제 클리어/입장 가능 상태 표시 |
| Assets/02_Scripts/06_UI/StageProgressMenu.cs 및 .meta | 생성 | 진행 관리와 두 확인 팝업 |
| Assets/02_Scripts/06_UI/VictoryPanelHud.cs | 수정 | 결과 버튼의 선택 화면 문구 설정 |
| Assets/03_Prefabs/05_Stages/StageSelectionPopup.prefab | 수정 | 임시 unlockedStages 필드 제거 |
| Assets/04_Data/04_Stages/Stage1.asset, Stage4.asset | 수정 | 해안가·왕도 임시 증원 종료 기준 3웨이브 |
| Assets/Resources/GameText.ko.json | 수정 | 진행 관리/확인/입장 가능/결과 버튼 문구 |
| Assets/09_Tests/Editor/StageProgressTests.cs 및 .meta | 생성 | 진행 저장·해금 회귀 테스트 |
| docs/stage-progression.md | 생성 | 진행 규칙, 저장 이관, 임시 웨이브 설명 |
| docs/ui-flow.md | 수정 | 실제 진행 관리 흐름 반영 |
| ai-use/evidence/20261119-stage-progress/ | 생성 | Unity CLI 로그·결과·검증 코드·화면·이 문서 |

이전 작업의 `docs/build-readiness-20261119.md`와 `ai-use/evidence/20261119-build-readiness/`는 이번 기능의 생성 결과가 아니며 그대로 유지했다.
