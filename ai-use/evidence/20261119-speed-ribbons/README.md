# 이동속도 흐름 강조

- Unity 6000.3.28f1 CLI가 `/tmp/be-speed-ribbon-validation` 복제 프로젝트에서 `SpeedAuraValidation.Run`을 실행했다. 원본 씬·전투 코드와 별도로 검증했다.
- 실제 단풍 스테이지에서 정지 유닛, 이동·90도 회전·정지 유닛, 같은 움직임에 공격력·방어막을 더한 유닛을 비교했다. 효과를 보기 위해 카메라를 확대하고 경로 화살표와 Falling Leaves는 복제본에서만 숨겼다.
- `rest.png`, `turn.png`, `stopped.png`는 실제 URP 렌더다. `preview.gif`는 연속 프레임을 크롭하고 라벨만 추가했다. 잔광의 밝은 마디가 뒤로 이동하고, 지나온 경로에 따라 빛이 휘어지는 모습을 확인했다.
- 20종의 공유 띠 메시·하체 위치·컬링 영역·이동·회전 경로·정지 수축·일시정지 시 경로 고정·거대화·원본 재질·독립 만료·중첩·사망 숨김·셰이더 컴파일을 검증했다.
- 최종 결과는 `results.txt`, 소스 해시는 `source-hashes.txt`, 검증 코드는 `SpeedAuraValidation.cs.txt`다. Windows 빌드와 대규모 동시 버프 성능은 미확인이다.
