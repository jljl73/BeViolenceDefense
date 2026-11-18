# BrokenStoneWall 사용 안내

- 프리팹: `ProjectBeViolenceDefense/Assets/Resources/Prefabs/BrokenStoneWall.prefab`
- 원본: 사용자 제공 `Meshy_AI_Broken_Stone_Wall_20261118_texture_fbx.zip`의 FBX 및 PNG.
- 현재 그리드 한 칸은 1×1 Unity 단위. 루트 위치를 `(cell.x, 0, cell.y)`, 회전을 0, 스케일을 1로 사용한다.
- 루트 피벗은 바닥 중앙. 시각 메시 크기 X/Y/Z는 0.92/0.8/0.314797. XZ 비율을 유지하고 높이를 기존 벽 규격에 맞췄다.
- BoxCollider 크기 X/Y/Z는 0.92/0.8/0.92, 중심은 (0,0.4,0). 돌벽은 얇지만 게임 규칙상 한 칸을 점유한다.
- `GridFit` 자식은 FBX 축 변환을 보정하며, `Visual`은 원본 모델 참조를 유지한다.
- URP/Lit 재질, BaseMap 텍스처, Metallic 0, Smoothness 0.15.
- `BoardTheme.wallPrefab`에 이 프리팹을 할당하면 기존 벽 생성 코드에서 사용할 수 있다. 이번 작업은 프리팹 제작까지이며 씬의 기존 벽 선택은 변경하지 않았다.
- 씬에 드래그하는 것만으로 DefenseGrid에 벽이 등록되지는 않는다. 게임 벽 배치 흐름을 통해 생성해야 경로·인지 판정에 반영된다.
- 검증: 저장된 프리팹 재생성 후 bounds·피벗·콜라이더 확인 PASS, URP 미리보기 확인, 컴파일 실패 없음. Play Mode 배치·철거 동작은 미검증.
- 증거: `ai-use/evidence/broken-stone-wall/validation.txt`, `preview.png`. 같은 폴더의 스크립트는 실행 기록이며 일괄 재실행용 도구가 아니다.
