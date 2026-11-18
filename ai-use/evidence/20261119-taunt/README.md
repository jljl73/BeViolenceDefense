# 도발 마법 Play Mode 확인 — 2026-11-19

원본 에디터가 열려 있어 스크래치 복사본(복사본의 `Library/ilpp.pid` 삭제 후)에서 Unity 6000.3.28f1 `-batchmode -executeMethod TauntPlayValidation.Run`으로 실행했다. 검증 스크립트는 복사본의 `Assets/Editor`에만 넣었다.

- 가상 키보드·마우스(Input System)로 `5` 키 조준 → 유닛 위 마우스 → 왼쪽 클릭 시전까지 실제 입력 경로를 거쳤다.
- GameScene과 Stage_GoldenMaple 모두 종료 코드 0. 도발 대상 최대 체력 75→113, 화면 배율 최고 1.40, 다른 적과 싸우던 유닛 1명이 도발 유닛으로 표적을 바꾸고 공격했다. GoldenMaple에서는 도발 종료 후 최대 체력·배율 복구와 끌려온 상태 해제를 확인했다(GameScene에서는 도발 유닛이 도중에 죽었다). 두 씬 모두 첫 웨이브를 생존 적 0으로 끝냈다.
- HUD 슬롯 5개가 겹치지 않고 횟수 표시가 없다. 캡처는 보드 카메라에 캔버스를 잠시 붙여 렌더했으므로 머리 위 체력바 위치는 실제 화면과 다르다.
- 로그의 예외는 Unity 검색 인덱서(`SearchDatabase.cs:453`) 시작 시 1건뿐이다.
- 실제 씬에서는 벽 너머 유닛을 끌어오는 경우가 나오지 않았다. 이 경우는 EditMode `TauntTests.TauntReachesThroughAWallAndTheUnitWalksAroundIt`로만 확인했다. 끌려온 유닛은 매번 1명이었다.

## 조정 후 재확인 (반경 3.5 → 2.0칸, 거대화 1.4 → 2배)

사용자 피드백(원형 범위가 너무 크고 1.4배는 티가 안 남)에 따라 `DefenseBattle.TauntRadius`를 2칸, `DefenseBoardView.TauntGiantScale`을 2배(커지는 속도 초당 4)로 바꿨다. EditMode 81개 통과, Stage_GoldenMaple Play Mode 종료 코드 0, 화면 배율 최고 2.00, 교전 중이던 유닛 1명이 도발 유닛으로 전환·공격, 웨이브 완료. 이번 실행에서는 도발 유닛이 도발 중에 죽어 종료 후 복구는 이전 실행 결과로 갈음한다. 캡처 `goldenmaple-r2-*.png`, 요약 `goldenmaple-r2-summary.txt`.
