# 코어 승리 연출 검증

- Unity 6000.3.28f1, macOS, 검증용 프로젝트 복사본에서 컴파일 및 Play Mode 실행.
- 실행: `Unity -batchmode -projectPath /tmp/be-victory-validation -executeMethod VictoryValidation.Run -logFile /tmp/be-victory-playmode.log`
- `VictoryValidation.cs`는 검증 복사본의 `Assets/09_Tests/Editor`에서 실행한 임시 검증 코드다.
- PASS: 정면 확대·포즈 유지·자동 복귀, 원래 카메라 위치/회전/줌 복구, HUD 숨김/복원, 다음 라운드 시작 차단, 반복 시작/건너뛰기, 컴포넌트 비활성화 정리, Animator 없는 코어 대체 처리, timeScale=0 실시간 연출, 실제 전투 클리어의 자동 연출 연결.
- `pose.png`: Play Mode 카메라의 1600×900 렌더. 캡처 중 Canvas를 ScreenSpaceCamera로 잠시 전환하고 원상 복구했다.
- 관련 C# 컴파일 오류 없음. Unity Search 인덱서의 시작 예외와 기존 라이선스 채널 handshake 재시도는 로그에 남았으나 검증은 정상 완료(exit 0).
- 원본 프로젝트는 이미 Unity에서 열려 있어 직접 CLI 실행 불가. 복사본의 첫 샌드박스 실행은 라이선스 IPC 접근 제한으로 실패했고, 승인된 확장 실행으로 검증 완료.
- Windows 빌드 및 다른 씬의 시각 검수는 수행하지 않았다.
