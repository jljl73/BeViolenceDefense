# 왕도 바닥 비교 증거

- `before/`, `after/`: Unity 실제 캡처 5장씩과 카메라·런타임 테마 메타데이터.
- `capital-comparison.png`: 준비 화면 전체와 통제 배치 확대, 왼쪽 이전 / 오른쪽 적용.
- `capital-detail-comparison.png`: 통제 배치 확대, 위 이전 / 아래 적용.
- `prefab-before.txt`, `prefab-after.txt`: 대상 프리팹 전후 스냅샷.
- `make_board_texture.py`: 저장소 루트에서 실행하는 결정론적 텍스처 생성기. Pillow, NumPy 필요.
- `CapitalFloorCapture.cs.txt`: 임시 복제 프로젝트의 Assets/Editor에 두고 실행한 후 버전 캡처 도구. 제품 코드에는 추가하지 않았다. 이전 실행은 출력 경로가 `/tmp/capital-floor-before`이고 변경 후 테마 검증 구문이 없는 버전이었다.
- `before-validation.txt`, `after-validation.txt`: Unity 로그에서 성공 표시·예외·할당 경고를 발췌. 전체 로그는 임시 경로 `/tmp/capital-floor-before.log`, `/tmp/capital-floor-after.log`에 생성했으며 저장소에 포함하지 않았다.
- `asset-checks.txt`: 원본 텍스처 보존, 전후 카메라·배치·유닛 수 일치, 적용 후 런타임 테마 값 확인.

CLI: Unity 6000.3.28f1, `-batchmode -projectPath /tmp/be-capital-floor-validation -executeMethod CapitalFloorCapture.Run -logFile <출력 로그>`.

확대 장면은 같은 모델·색상·좌표로 만든 비교용 배치다. 전투 캡처는 고정 틱 600회 진행 기준이며 HUD 시간은 별도로 진행하지 않는다. 패턴과 줄눈을 함께 줄인 비교이므로 두 변경의 개별 기여도는 분리 측정하지 않았다.
