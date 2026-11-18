# 아트 톤 검토 — 2026-11-18

의견 검토이며 확정 기획이나 구현 변경이 아니다. 게임 코드·프롬프트·씬·에셋은 수정하지 않았다.

## 확인한 자료
- `ArtDirection/Reference/Ref_Galvatein_KeyArt.png`, `Ref_Arknights_Battle.png`, `Ref_TileDefense_Diorama.png`, `Ours_FieldUnit_SD.png`, `Ours_Tower.png`
- `ArtDirection/Mockups/A_GalvateinLean/BattleScreen_A_Mockup.png`, `A_GalvateinLean_26sunburst/BattleScreen_A_Mockup.png`, `B_ArknightsLean/BattleScreen_B_Mockup.png` 및 관련 요청서·프롬프트
- `Art Prompts/[Base Prompts].txt`, `[Negative Prompts].txt`, `[Style] 00_Style_Probe.txt`, `[Characters] 02_Party.txt`, `[Enemies] 01_Enemies.txt`
- `Art2D/Portraits/Caster/Caster_Portrait_neutral.png`
- `Tools/_nai_out/unity_enemies3_final.png`, `Tools/_nai_out/unity_caster_skeleton.png`
- `docs/ui-flow.md`, `ProjectBeViolenceDefense/Assets/Scripts/UI/UIPalette.cs`, `DefenseBoardView.cs`, `VoxelDefensePrototype.cs`, 실제 GameScene 준비·전투 화면
- Unity 6000.3.28f1 / URP 17.0.1 / Input System 1.19.0 / uGUI 확인.

## 판단 및 제안
- 부드러운 동화풍 캐릭터, 입체 타일 보드, 정돈된 전술 정보 표현을 결합하는 방향을 제안한다. 갈바테인의 따뜻한 색과 표정, 엠버워드의 디오라마 구성, 명일방주의 정보 위계를 각각 참고한다.
- 제안된 배경·패널 네 색은 이미 UIPalette와 UI 가이드에 존재하며 실제 HUD에도 쓰인다. 남색·자주색 바탕은 유지하고 전장에 따뜻한 돌·탁한 풀색·크림색 등 중간 명도를 보충하는 방향이 적합하다.
- #583642는 붉은 자주빛 갈색으로 보인다. 목재 전체 재질보다는 강조 패널에 적합하다.
- sRGB 상대 휘도로 계산한 인접 면 대비는 Background/Panel 1.14:1, Panel/PanelRaised 1.19:1이다. 미세한 명도 차이만으로 구조를 구분하지 말고 기존 테두리·여백을 함께 사용한다. TextPrimary/Panel은 약 13.97:1, TextSecondary/Panel은 약 7.54:1이다.
- A 목업의 무거운 나무·놋쇠 틀과 굵은 외곽선은 현재 파스텔 초상보다 강하다. B 목업의 정돈된 구성은 참고하되 밝은 양피지 전체 바탕·큰 인물 카드 줄을 그대로 적용할 필요는 없다.
- 기존 A/B 목업의 아군 배치·공격 타워·성문 구성은 현재 게임 규칙과 다르다. 실제 중심 정보는 두 적 팩션의 경로·교전·체력·버프다.
- 프롬프트의 lineless/flat/pastel/deformed 방향은 현재 초상과 일치한다. high complexity, white outline, 밝은 기존 적과 어두운 신규 적의 차이는 용도별로 정리할 후보이며 이번에는 변경하지 않았다.

## 현재 프레임의 시각 평가
아직 임시 형태가 쓰이는 프로토타입에 대한 주관적 평가다. 완성도 인증이 아니다.
- Palette discipline: 3/5 — HUD 역할 색상은 일관됨. 전장의 회청색은 코드에서 지정한 색이며 Default-Material은 검출되지 않음.
- Style uniformity: 2/5 — 초상·3D 시험 모델·실제 블록 전장 사이의 표현 차이가 큼.
- Lighting depth: 2/5 — 타일 높이와 그림자는 보이나 따뜻한 밝은 면/차가운 그림자의 연출은 약함.
- Silhouette: 2/5 — 실제 전투 유닛은 임시 블록이며 색이 주요 구분 수단. 별도 모델 시험 이미지에서는 모자·망토·깃털 등 구분 요소 확인.
- UI intentionality: 3/5 — 아이콘 중심 모서리 HUD와 스킬 슬롯이 팔레트에 맞음. 1600×900 캡처에서 마나 100/100의 마지막 0 줄바꿈 관찰.
- Feedback visibility: 미평가 — 준비 경로·교전 예측 표식은 확인. 버프·힐 시전과 전투 효과의 동적 가독성은 이번 범위에서 검증하지 않음.

## 실행 및 증거
- Unity CLI 1.0.0-beta.13, Pipeline 1.0.0으로 연결. 업데이트 확인은 latestVersion 1.0.1을 반환했으나 리뷰 과정에서 설치·업데이트는 수행하지 않음.
- 시작 상태: Test_Enemies3, Play 정지, 씬 dirty=false, auto-tick=true/16ms, runInBackground=true.
- GameScene을 열어 Play 시작, Time.frameCount 1→2 및 후속 프레임 증가 확인. 기존 StartBattle 메서드를 일회 호출하여 실제 전투 HUD 관찰.
- `ArtDirection/Reviews/20261118-art-tone/warmup.png`: 첫 캡처, 판정 근거에서 제외.
- `ArtDirection/Reviews/20261118-art-tone/preparation.png`: 준비 화면.
- `ArtDirection/Reviews/20261118-art-tone/combat.png`: 전투 화면.
- 종료 시 Play 정지 후 Test_Enemies3 복원, dirty=false, auto-tick와 runInBackground 초기값 유지 확인. Console groundTruth 오류 0, compilationFailed=false. Pipeline 누적 counts.error=1은 시작·종료 동일하고 cursor=2 유지.
- 위 ArtDirection·Tools 자료는 gitignore 대상이므로 원격 저장소 공유 여부는 별도 확인 필요.
- 사용 지침: using-unity, craft-game-floors의 art-direction/visual-scorecard, unity-cli의 integration-advanced/playmode-verification-loop.
- 참고 페이지: https://store.steampowered.com/app/2459550/Emberward/ 및 https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-runInBackground.html

## 결과물 목록
| 파일 | 도구 | 내용·채택 상태 |
| --- | --- | --- |
| 이 파일 | Codex | 검토 근거와 제안 기록, 아트 방향 확정 아님 |
| `ArtDirection/Reviews/20261118-art-tone/warmup.png` | Unity CLI / Pipeline | 첫 프레임 캡처, 평가 제외 |
| `ArtDirection/Reviews/20261118-art-tone/preparation.png` | Unity CLI / Pipeline | 현행 준비 화면 검토 증거 |
| `ArtDirection/Reviews/20261118-art-tone/combat.png` | Unity CLI / Pipeline | 현행 전투 화면 검토 증거 |
