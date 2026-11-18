# 설원·묘지 길 구조 시안 증거

설명은 [설원 시안](../../../docs/level-snowfield.md)과 [묘지 시안](../../../docs/level-graveyard.md)에 있다.

## 파일

| 파일 | 내용 |
| --- | --- |
| snow-map.txt, grave-map.txt | 지형 글자 그림. `gen.py`([2스테이지 증거 폴더](../20261119-stage2-layout/README.md))로 StageData 에셋을 만든 원본 |
| snow-layouts.txt, grave-layouts.txt | 전수 실행에 쓴 벽 배치 15종 |
| snow-batch-a7511e7.txt, grave-batch-a7511e7.txt | 편성 6종 × 벽 배치 15종, 커밋 `a7511e7`의 전투 코드. 설원 90/90, 묘지 90/90 종료 |
| snow-batch-worktree.txt, grave-batch-worktree.txt | 같은 조합을 작업 끝 무렵의 작업 트리 전투 코드(커밋 `efbd3e0` + 스테이징된 재경로 수정, worktree-status.txt)로 실행 |
| snow-batch-reroute-first.txt | 재경로 변경의 첫 판(커밋 전 작업 트리)으로 실행. 1개 미종료 |
| gate-combos-summary-a7511e7.txt | 문을 막는 조합 전체 실행 요약. 묘지 편성 6종 294건, 설원 편성 E·F 704건과 A~D 1,408건 모두 종료. `rejected`는 게임이 길을 막는다고 거부한 배치 |
| snow-gate-combos-ABCD-a7511e7.txt, grave-gate-combos-a7511e7.txt | 위 실행의 줄별 결과. 설원 E·F의 줄별 결과는 덮어써서 요약만 남았다 |
| snow-rejected-one-row-courtyard-map.txt, snow-rejected-one-row-courtyard-batch.txt | 버린 설원 안(코어 앞마당 한 줄)과 그 전수 출력. 대규모 편성, 윗목 (14,12) 1개에서 미종료 1건과 유닛 상태 덤프 |
| StageCheck.cs, chk.sh, batch.sh, stress.py | 검증 도구. `StageCheck.cs`는 2스테이지 때의 것에서 카탈로그의 `spawnStructures` 항목을 건너뛰게 고쳤다 |
| unity-results.txt | Unity 6000.3.28f1 EditMode·Play Mode 결과 |
| StagePlayValidation.cs.txt | Play Mode 자동 확인에 쓴 임시 Editor 스크립트(프로젝트 복사본에만 두었다) |
| snowfield.png, graveyard.png | Play Mode 준비 단계 화면 |

## 편성 기호

A 고블린 전사 6 vs 해골 6, B 늑대 6 vs 좀비 3, C 투석병 5 vs 해골 궁수 5, D 오크 2·투석병 3 vs 해골 2·리빙 아머 1·해골 궁수 3, E 고블린 전사 12 vs 좀비 8, F 오크 3·고블린 전사 8·투석병 4 vs 해골 8·네크로맨서 1·해골 궁수 4·좀비 3.

## 주의

- Unity 밖 빌드(net10.0, `BalanceSim/UnityStubs.cs`)의 수치는 Unity와 부동소수 결과가 다를 수 있다. Unity에서는 지형·경로 길이 테스트와 Play Mode 자동 확인만 했다.
- 작업 도중 다른 작업이 전투 코드(교전 뒤 재경로)를 바꾸고 커밋했다. 문서의 표는 `a7511e7` 기준이다.
