# 코어 시야 수정과 단풍 스테이지 연결 검증 자료

| 파일 | 내용 |
| --- | --- |
| `change.patch` | `DefenseGrid.cs`, 테스트 2개 파일, `Stage_GoldenMaple.prefab`의 변경 |
| `tests-before-outside-unity.txt` | 고치기 전 전투 소스에 현재 테스트를 돌린 결과. 새 테스트 3개 실패 |
| `tests-after-outside-unity.txt` | 고친 뒤 64개 통과 (`CombatPositioningTests`, `SimulationDeterminismTests`) |
| `jitter-and-crowd.txt` | 떨림 계측 합계 세 가지(고치기 전, 채택안, 미채택안)와 밀집 전투 8개 |
| `stage2-batch-after.txt` | 2스테이지 편성 6종 × 벽 배치 17종. 102개 모두 종료. 맨 위 두 줄은 이전에 끝나지 않던 재현 2개 |
| `unity-results.txt` | Unity 6000.3.28f1 EditMode 92/92, Play Mode 확인 로그 |
| `Stage2PlayValidation.cs.txt` | 프로젝트 복사본에만 넣은 Play Mode 확인 스크립트 |
| `stage2-maple.png` | Play Mode에서 단풍 버튼을 누른 뒤의 준비 화면 |

계측·검증 도구는 [떨림 계측](../20261119-corridor-jitter/README.md)과 [2스테이지 지형](../20261119-stage2-layout/README.md) 폴더의 것을 그대로 썼다.

## 한계

- Unity 밖 빌드는 Unity와 실수 계산이 달라질 수 있다. 새 테스트 3개와 전체 EditMode는 Unity에서 통과를 확인했다. 떨림 계측과 102개 조합은 Unity 밖에서만 돌렸다.
- Play Mode는 자동 스크립트로 단풍 버튼, 지형, 경로, 전투 600틱까지만 봤다.
