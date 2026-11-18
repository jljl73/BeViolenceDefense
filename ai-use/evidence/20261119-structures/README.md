# 풍차·무너진 타워 방해물 임포트

- 원본: 사용자가 지정한 Downloads의 Meshy FBX/PNG 두 쌍. 원본 수정 없음.
- 프리팹: `ProjectBeViolenceDefense/Assets/03_Prefabs/04_World/CozyWindmillCottage_2x2.prefab`, `ToppledStoneTower_2x2.prefab` 및 각각의 .meta.
- 모델/텍스처/URP Lit 머티리얼: `ProjectBeViolenceDefense/Assets/05_Art/05_Meshes/Environment/CozyWindmillCottage/`, `ToppledStoneTower/` 및 폴더·파일 .meta.
- 균일 스케일, 바닥 Y=0, XZ 중심=0. 긴 수평 축을 X로 맞춰 기존 FitObstacle 규칙과 호환. X 폭 1.92, Z 폭 각각 1.8239 / 1.7755. BoxCollider XZ 1.92×1.92. 이름의 2x2로 스테이지 팔레트 자동 인식.
- 검증: validation.json (이름 파싱·4칸 차단·치수·머티리얼), 두 PNG (실제 Unity 렌더), playmode.json (두 프리팹 4회전 배치 통과), console.json (컴파일 실패 false, 오류/경고 0).
- import.cs / validate.cs / playmode.cs는 CLI script eval로 실행한 작업·검증 스크립트이며 Assets 외부에 보관.
- CLI 1.0.0 사용. 1.0.1 업데이트 알림만 전달. 초기 sandbox 경로 접근 제한은 승인된 실행으로 해결. Play Mode 검증 스크립트의 내부 타입 접근 오류는 리플렉션으로 수정. Play 전환 중 일시적으로 EDITOR_NOT_RUNNING이 발생했으나 status에서 같은 PID의 ready 및 프레임 진행을 확인한 뒤 검증 완료, Play 종료.
- 스테이지/씬 배치는 변경하지 않음. 작업 중 별도 변경으로 나타난 Netmarble Medium SDF.asset은 직접 편집하거나 되돌리지 않음(동적 폰트 갱신 여부 확인 필요).
