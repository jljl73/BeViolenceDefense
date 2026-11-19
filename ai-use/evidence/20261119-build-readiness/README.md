# macOS · Windows 빌드 점검 증거

원본 작업 트리는 시작 시 clean이었다. 정확한 HEAD는 `source-state.txt`에 있다. 원본의 제품 코드·에셋·설정은 바꾸지 않았다. 검증용 복제본: `/tmp/be-build-audit-20261119`.

## 산출물

| 파일 | 내용 |
| --- | --- |
| preflight.txt | Unity API로 읽은 Mono·stripping·Universal·그래픽 API·플랫폼 지원, 참조 및 셰이더 점검 |
| dependencies.txt | 활성 빌드 씬의 의존성 1,162개 목록 |
| build-summary.txt | macOS BuildReport 성공, 오류 0, 경고 6, 421,840,622바이트 |
| build-messages.txt | macOS BuildReport 경고 상세 |
| windows-build-excerpt.txt / windows-build-final-excerpt.txt | Windows 최초 및 재검증 실행의 성공·빌드 용량·경고 발췌 |
| binary-checks.txt | 실제 PE x86-64 / Mach-O arm64+x86_64 파일, ad-hoc 서명 및 검증 결과 |
| editmode.xml | 실제 EditMode 120개, 통과 119, 실패 1 |
| static-checks.json | Assets 경로 대소문자·GUID 중복·파일 meta·플러그인 파일 점검 |
| mac-player-excerpt.txt | 실제 앱 시작 정보, Ellipsis 경고 17회와 런타임 오류 검색 결과 |
| mac-test-dialogue.png | 비 Development 앱에서 T키 테스트 대화 재현 |
| mac-capital-battle.png | 왕도 전투 실행 화면 |
| mac-capital-restart.png | R키 재시작 후 준비 화면 |
| BuildReadinessAudit.cs.txt | 복제본 Assets/Editor에만 추가한 점검·macOS 빌드 스크립트 |

실제 빌드 결과물은 git 제외 경로 `ProjectBeViolenceDefense/Builds/Readiness-20261119/`에 보관했다. 정식 배포본이 아닌 현 상태 검증본이다. `macOS/BeViolenceDefense.app`, `windows/BeViolenceDefense.exe` 및 런타임 파일, 각 플랫폼의 별도 DoNotShip 디버그 정보가 있다.

## 재현 명령

Unity 실행 파일: `/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity`.

1. 원본 Assets·Packages·ProjectSettings·Library를 macOS COW 복사로 임시 프로젝트에 복제. 위 점검 스크립트를 Assets/Editor에 추가.
2. `-batchmode -projectPath /tmp/be-build-audit-20261119 -buildTarget StandaloneOSX -executeMethod BuildReadinessAudit.Run -logFile /tmp/be-build-audit-results/mac-build.log` → exit 0.
3. `-batchmode -projectPath /tmp/be-build-audit-20261119 -runTests -testPlatform EditMode -testResults /tmp/be-build-audit-results/editmode.xml -logFile /tmp/be-build-audit-results/editmode.log` → exit 2, 오래된 묘지 크기 기대값 실패 1개.
4. `-batchmode -quit -projectPath /tmp/be-build-audit-20261119 -buildTarget Win64 -buildWindows64Player /tmp/be-build-audit-results/windows/BeViolenceDefense.exe -logFile /tmp/be-build-audit-results/windows-build.log` → exit 0, `Build Finished, Result: Success.`.
5. 동일 Windows 명령을 `windows-build-final.log`로 재검증 → exit 0. 빌드 보고서의 PerformanceTestRunSettings 리소스를 확인하기 위한 재검증으로, 이는 빌드 전처리기가 자동 생성하는 23바이트 파일이며 에디터 종료 후 Assets에 남아 있지 않았음. 원본 제품 코드는 변경하지 않았다.
6. macOS 플레이어 실행에 `-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile /tmp/be-build-audit-results/player-mac.log` 사용. CUA로 시작·프롤로그 건너뛰기·왕도 선택·T 테스트 대화·S 건너뛰기·Space 전투·공격 버프 조준·Escape 취소·줌·R 재시작·Cmd+Q 종료 확인. 프로세스 exit 0.

원본 전체 CLI 로그는 `/tmp/be-build-audit-results/`에 있다. 저장소에는 필요한 발췌만 보존한다. 테스트용 Unity 빌드는 원본과 같은 패키지 잠금 파일 및 버전을 사용했으며 패키지나 에디터 설치를 변경하지 않았다.
