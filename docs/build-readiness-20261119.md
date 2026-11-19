# macOS · Windows 빌드 사전 점검

2026-11-19. Unity 6000.3.28f1. 현재 작업 트리의 Assets·Packages·ProjectSettings·Library를 임시 프로젝트로 복제하여 검증했다. 게임 코드, 에셋, 플레이어 설정은 원본에서 수정하지 않았다.

## 결론

두 플랫폼의 Mono 릴리스 빌드가 성공했다. 현재 확인 범위에서 빌드를 차단하는 C# 오류·누락 스크립트·끊어진 직렬화 참조는 없다. 외부 배포 전에는 테스트 입력 제거, 미완성 메뉴와 진행 표시 정리, 앱 식별자 확정, macOS 배포 서명, 실패 중인 테스트 정리가 필요하다. Windows 실행 검증은 별도로 남아 있다.

| 검증 | 결과 | 범위 |
| --- | --- | --- |
| macOS 빌드 | 성공 | Mono, 비 Development, Metal, Universal arm64+x86_64. BuildReport 오류 0, 경고 6 |
| macOS 실제 실행 | 확인 | macOS 15.1, Apple Silicon에서 1280×720 창 모드. 타이틀 → 시작 → 프롤로그 → 건너뛰기 → 스테이지 선택 → 왕도 → 전투 시작 → 마법 조준 → 줌 → R 재시작 → 앱 종료 |
| Windows 빌드 | 성공 | Mono, 비 Development, Win64 x86-64 PE 실행 파일. Graphics API 설정은 D3D12 우선, D3D11 대체 |
| Windows 실제 실행 | 미검증 | Windows PC 및 GPU 환경 없음. D3D11/12 화면·입력·오디오·종료·다시 시작은 실기 확인 필요 |
| EditMode 테스트 | 119/120 통과 | 실패 1개는 묘지 맵 크기의 오래된 기대값 |
| 씬·프리팹·SO 참조 | 누락 0 | 활성 빌드 씬 2개에 연결된 재귀 의존성 1,162개 중 대상 객체 검사 |
| Assets 파일 | 충돌 없음 | 중복 GUID, 경로 대소문자 충돌, 파일 meta 누락 없음. 별도 네이티브 플러그인 파일 없음 |

처음에는 Unity.app 내부 PlaybackEngines만 확인하여 Windows 모듈이 없다고 판단했지만, 실제 모듈은 에디터 설치 루트의 `PlaybackEngines/WindowsStandaloneSupport`에 있었다. Unity 모듈 등록 로그와 실제 빌드 성공으로 확인했다.

## 배포 전에 처리할 항목

### 1. 릴리스 앱에서 테스트 대화창이 열림

`Assets/01_Scenes/02_Gameplay/GameScene.unity`의 `Dialogue Test (T)` 오브젝트와 `DialogueHudTester`가 활성 상태다. `Assets/02_Scripts/06_UI/DialogueHudTester.cs:24`는 에디터/개발 빌드 조건 없이 T키를 받는다. 실제 macOS 릴리스 앱에서 왕도 입장 후 T키로 샘플 대화가 열리는 것을 재현했다.

배포 씬에서 테스트 오브젝트를 빼거나, 입력 코드를 `UNITY_EDITOR || DEVELOPMENT_BUILD`로 제한할 필요가 있다. 승리·패배 패널의 `testKey`도 프리팹 기본값은 켜져 있으나 게임 초기화의 `Bind()`에서 꺼지므로 같은 문제라고 단정하지 않는다.

### 2. 설정과 화면 이동이 배포용 흐름으로 완성되지 않음

`VoxelDefensePrototype.cs:188`의 설정 버튼은 `settings_pending` 말풍선에 연결되어 있다. 플레이 중 일시정지 메뉴, 스테이지 선택으로 돌아가기, 게임 내 종료 동작이 제공되지 않는다. 타이틀에는 종료 버튼이 있고 macOS Cmd+Q는 정상 종료했다. OS 종료 수단은 있지만 게임 내부 탐색 동선은 부족하다. `resizableWindow=0`, 네이티브 해상도 전체 화면 기본값이므로 Windows에서 해상도·창 모드 전환 UX도 확인해야 한다.

### 3. 클리어 표시는 실제 저장 진행도가 아님

`StageSelectionPopup`의 `unlockedStages=5` 임시 값 때문에 1~4번은 클리어, 왕도는 현재 스테이지로 표시된다. 실제 앱에서도 이 상태를 확인했다. `StageSpellUnlocks`는 별도의 PlayerPrefs 비트로 스킬 해금을 저장하므로 선택 화면의 클리어 표시와 스킬 해금 상태가 일치하지 않을 수 있다. 전 스테이지 공개 체험판으로 갈지, 실제 진행도를 연동할지 정해야 한다.

### 4. 회사명·앱 이름·식별자가 템플릿 값

`ProjectSettings/ProjectSettings.asset`의 `companyName=DefaultCompany`, `productName=ProjectBeViolenceDefense`, Standalone 식별자 `com.Unity-Technologies.com.unity.template.urp-blank`가 실제 앱에도 들어간다. 게임 창 제목 역시 ProjectBeViolenceDefense로 표시된다. 배포 이름과 식별자를 확정한 뒤 저장 데이터 이전 여부를 검토해야 한다. 현재 진행도·오디오 설정은 PlayerPrefs를 사용한다.

### 5. macOS 외부 배포 서명 미완료

생성된 앱은 `codesign --verify --deep --strict` 검증을 통과했으나 **ad-hoc 서명**, `TeamIdentifier=not set` 상태다. 로컬 실행 성공은 다운로드한 다른 Mac에서의 Gatekeeper 통과를 보장하지 않는다. 일반 사용자에게 인터넷으로 배포할 때는 Developer ID 서명과 notarization 절차를 준비해야 한다. 서명·인증서·공증은 이번 점검에서 변경하거나 제출하지 않았다. [Unity macOS 서명 및 공증 안내](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/apple-mac/macos-delivery/macos-building-notarization)

### 6. 기존 테스트 한 개가 현재 지형과 불일치

`StageContentTests.cs:43`의 묘지 테스트는 기본 17×17 프레임을 기대하지만 실제 묘지는 23칸이다. 실패: `Expected: 17 / But was: 23`. 실제 맵 의도에 맞게 크기뿐 아니라 스폰·코어·길목·경로 길이 검증을 함께 갱신해야 한다. 왕도는 기존 `EveryStageIsSelfContained...` 순회 목록에도 빠져 있어 해당 회귀 범위를 보완하는 편이 좋다. 통과 숫자를 맞추기 위해 테스트를 삭제하거나 값만 바꾸지는 않았다.

## 비차단 경고

- 방어막·공격력·이동속도 버프 셰이더의 `pow()` 입력 관련 경고가 Metal과 Windows D3D 셰이더 컴파일에 나타난다. 특히 `pow(1-facing, .65/.7)`는 정규화 및 half 정밀도 오차를 고려해 입력을 0~1로 제한하는 보완 후보다. GPU별 실제 시각 오류를 재현한 것은 아니다. 색상 거듭제곱도 음수 입력을 막을 수 있다.
- macOS 플레이어에서 TMP `Netmarble Medium SDF`의 Ellipsis 글리프 부재 경고 17회를 확인했다. 긴 텍스트의 Ellipsis 처리 대신 Truncate로 전환된다. 관찰한 한글 타이틀·대화·HUD는 표시됐으며, 전체 한글 글리프 검증을 의미하지 않는다.
- macOS BuildReport의 경고 6개는 위 셰이더 경고 5개와 Unity Cloud 토큰 부재로 네이티브 심볼을 업로드하지 못한다는 경고 1개다. 로컬 빌드 성공과는 별개다.
- Windows 대상 전환 중 사용하지 않는 URP 레이 트레이싱 셰이더의 호스트 컴파일 지원 경고가 있었다. 최종 Windows 빌드는 성공했으며 현재 보드의 렌더링 실패로 단정하지 않는다.
- Unity 출력에 `ProjectBeViolenceDefense_BurstDebugInformation_DoNotShip` 폴더가 생긴다. 배포 압축에는 넣지 않는다. Windows는 exe만 전달하지 말고 `_Data`, `UnityPlayer.dll`, `MonoBleedingEdge`, D3D12 등 런타임 파일을 함께 묶어야 한다.

## 확인된 구성과 후속 검증

빌드 씬은 `TitleScene`과 `GameScene` 두 개다. 다섯 스테이지는 GameScene의 프리팹 참조로 포함된다. URP·신 Input System·UGUI/TMP 구성이며, 게임 런타임의 UnityEditor 호출은 에디터 조건 또는 Editor 디렉터리에 격리되어 있다. 버프 셰이더는 Resources로 포함된다. 빌드에서 실제 왕도 렌더와 조준 화면은 확인했다.

현재 검증은 **Mono + managed stripping Disabled** 기준이다. IL2CPP나 stripping을 변경하면 이번 결과를 그대로 적용할 수 없다. Windows IL2CPP를 선택한다면 Windows 네이티브 컴파일 환경에서 별도 검증해야 한다. [Unity IL2CPP 플랫폼 도구 요구](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/scripting-backends/il2cpp/il2cpp-introduction)

다음 실기 확인은 Windows D3D12/D3D11 각각의 5개 스테이지, Intel Mac, 다양한 화면 비율, 새 저장 데이터에서의 진행/스킬 해금, 모든 마법과 전체 웨이브, 오디오 출력, 앱 재실행 후 저장 복원이다. macOS 오디오의 청취 검증이나 정량 성능 측정은 하지 않았다. 임시 복제본의 URP 빌드 필터와 동적 폰트 데이터는 Unity 빌드 과정에서 재직렬화됐으며 원본 프로젝트에 되돌려 적용하지 않았다.

증거와 재현 명령: [검증 기록](../ai-use/evidence/20261119-build-readiness/README.md).
