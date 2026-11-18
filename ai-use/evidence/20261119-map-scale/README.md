# 맵 2배 실험 검증 (2026-11-19)

`VoxelDefensePrototype.mapScale = 2`(칸 한 변 2)로 유닛 값은 그대로 두고 맵을 키운 상태의 확인 기록.

- EditMode: 프로젝트 복제본에서 108/108 통과(`MapScaleTests` 3개 포함). 기존 테스트는 모두 칸 한 변 1로 돈다.
- Play Mode: `Stage_GoldenMaple` 테스트 씬, 3배속. 첫 교전 914틱, 웨이브 2327틱에 준비 단계로 돌아옴(코어 113, 남은 적 0). 유닛 화면 위치와 전투 좌표 차이 최대 0. 유닛 루트 배율 0.5.
- 캡처: `0-preparation.png`, `1-battle.png`, `2-battle-zoom.png`, `3-after-wave.png`. 요약 `summary.txt`.
- `ArgumentOutOfRangeException` 1건은 에디터 시작 시 SearchDatabase 색인 예외로, 이전 검증에서도 나온 에디터 잡음이다.
- `MapScalePlayValidation.cs.txt`는 검증 복사본의 `Assets/Editor`에 넣어 `-executeMethod MapScalePlayValidation.Run`으로 실행한 스크립트다. GameScene은 스테이지 선택을 기다리므로 이 스크립트로는 시작되지 않는다.
