### [20261118-art-3d-pipeline] 아트 디렉션 세팅과 적·주인공 3D 유닛 파이프라인
- 시각: 2026-11-18 17:16:00 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Fable 5.2, 세션 중 Claude Opus 5.5로 전환) / NovelAI Diffusion V5 / OpenAI Images API (gpt-image-2, gpt-image-2.6-sunburst·flare) / Codex CLI 0.170.0 (gpt-6-sol) + Blender MCP / Blender 4.5 / Meshy API / Unity CLI + Pipeline (Unity 6000.3.28f1)
- 목적 및 적용 위치: 원화(NAI) → GPT 4면 시트 → Meshy 메시·리깅·동작 → Codex/Blender 보정 → Unity 임포트로 이어지는 유닛 제작 흐름을 만들고, 고블린·늑대·해골 병사·주인공 술사(지팡이 포함)를 `Assets/Art/Enemies`, `Assets/Art/Heroes`에 넣음
- 사용 방식: Claude Code가 프롬프트 파일 작성, 생성 API 호출, 결과 검수, Codex용 Blender 작업 요청서 작성·실행, Unity 임포트·프리팹·확인 씬 구성을 수행. 긴 생성 작업 감시는 Claude 하위 에이전트(Haiku)에 맡김. 컨셉 선택, 면 수(잡몹 1.5k·주인공 10k), 동작 종류(공격·피격·쓰러짐), 지팡이 분리 여부는 사용자가 결정
- 결과 및 확인 방법: 부분 성공. 사람형은 Meshy 리깅·동작으로, 4족(늑대)은 Codex+Blender 리깅으로 Unity 재생까지 확인. 확인 방법: 별도 headless Blender 렌더로 6방향·동작 프레임 캡처, Unity 미리보기 씬 렌더(스킨드 메시 굽기)로 동작 필름스트립, Play Mode에서 `Time.frameCount` 진행 확인 후 스크린샷, 콘솔 오류 0 확인. 고블린 60기 씬에서 드로우콜 246·셋패스 12 측정(에디터 비포커스 상태라 프레임 시간은 신뢰 불가)
- 사람의 개입: 컨셉·배리에이션 선택, 지팡이 위치 재작업 지시, 주인공 얼굴 품질 지적(→ Unlit+2048로 해결), GPT 화면 목업 반려, gitignore 범위 결정

#### 생성·수정 결과물
- 목록: [20261118-art-3d-pipeline 결과물](manifests/20261118-art-3d-pipeline.md)

| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Art Prompts/` (Base·Negative·Style·Enemies·Characters·Props) | Claude Code | 생성 | Base·Negative는 사용자 제공 원문 그대로. 나머지는 섹션별 NAI 프롬프트와 채택 시드 기록. 사용 중 |
| `.gitignore` | Claude Code | 수정 | 작업 폴더(`Art3D/`, `ArtDirection/`, `Tools/`)와 Unity 확인용 씬·미리보기 컨트롤러 제외, Codex 작업 찌꺼기 제외 |

#### 증거
- 없음 (확인 캡처·작업 로그는 로컬 `Art3D/`, `Tools/`에 있으며 저장소 미포함)

#### Unity CLI 사용
- 구현하거나 수행한 작업: FBX 임포트 설정(Generic, 클립 이름·반복), 머티리얼·AnimatorController·프리팹 생성, 확인용 씬 구성, Play Mode 검증과 스크린샷, 미리보기 씬 동작 필름스트립 렌더, 콘솔 확인
- 활용 방식: `unity command script eval`로 C# 실행, `editor set_auto_tick`·`editor.playmode set_run_in_background` 후 Play Mode 진입, `job wait`로 프레임 진행 확인, 종료 후 두 설정 원복
- 잘된 점: 에디터 비포커스 상태에서도 Play Mode 검증과 캡처가 가능했고, 다른 프로젝트 에디터가 열려 있어도 `--project-path`로 대상을 고정할 수 있었음
- 잘 안된 점 및 개선 필요: 미리보기 씬에서 스킨드 메시를 직접 렌더하면 모든 프레임이 같은 포즈로 찍힘(BakeMesh로 해결). 외부에서 복사한 파일은 `AssetDatabase.Refresh` 전에는 보이지 않음. Play Mode가 아닐 때 `ScreenCapture`는 파일을 쓰지 않으며, 그 상태에서 카메라를 옮긴 스크립트가 편집 모드 씬 오브젝트를 건드렸음(저장 전이라 씬 다시 열기로 원복, 손실 없음)

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex CLI(`codex exec`, Blender MCP 도구만 실행 단위로 승인)로 Blender 작업 5건 — 고블린 3k·1.5k 재베이크, 늑대 4족 리깅+동작 4개, 술사 지팡이 손뼈 부착 v1·v2. OpenAI Images API로 유닛 4면 시트 생성, Codex 경유로 전투 화면 목업 4장 생성
- 사용 방식: Claude Code가 요청서(REQUEST.md)를 쓰고 Codex를 실행, Codex가 실행 중인 Blender에서 작업 후 확인 캡처·README를 남기면 Claude Code가 캡처를 직접 열어 검수하고 Unity에 반영
- 특별히 활용한 점: 원화 한 장을 그대로 옮기는 4면 시트는 GPT 이미지가 정확했음. 반면 화면 전체 목업은 우리 그림체를 따르지 못해 반려됨

#### 주요 프롬프트
- 사용 도구: Claude Code
- 프롬프트 원문 또는 저장소 내 원문 파일 경로: [20261118-art-3d-pipeline-01](prompts/20261118-art-3d-pipeline-01.md)
- 결과 및 실제 활용 방식: 원화→4면→메시→Blender 보정 파이프라인을 실제로 구축하고 Claude Code 스킬로 정리
- 선정 이유: 이후 모든 유닛 제작 절차의 기준이 된 요청

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 프로젝트 테스트 카메라는 X축 55° 직교(AGENTS.md)인데, 이번 가독성·확인 캡처는 70° 원근 카메라 기준이었음. 55° 직교에서 유닛 가독성 재확인 필요. 실제 게임 씬에서의 프레임 시간 미측정. Meshy 전투 동작은 몸이 30~60° 비튼 자세로 시작함

### [20261118-caster-portraits] 주인공 대화창 상반신 표정 5종
- 시각: 2026-11-18 17:16:00 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Opus 5.5) / NovelAI Diffusion V5 (img2img)
- 목적 및 적용 위치: 대화창용 주인공 상반신 기본·웃음·놀람·곤란·뾰로통, `Art2D/Portraits/Caster/`
- 사용 방식: 채택한 전신 배리에이션에서 상반신을 잘라 img2img(강도 0.45)로 고해상도 재작성, 표정 태그만 바꿔 같은 방식으로 표정 생성, 얼굴 영역만 부드러운 마스크로 기본 그림에 합성하고 눈동자·피부 색을 기본에 맞춤
- 결과 및 확인 방법: 성공. 5장이 머리·옷·지팡이 픽셀 단위 동일, 얼굴 확대 비교로 경계 확인. 레이어 분리본(Base + Face) 재합성 시 평균 오차 0.2/255
- 사람의 개입: 놀람 표정 혈색 지적 → 재생성 후 교체. 곤란 표정의 땀방울 점·눈썹 표현은 수작업 다듬기 대상

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Art2D/Portraits/Caster/Caster_Portrait_{neutral,happy,surprised,troubled,pout}.png` | NAI, Claude Code(합성·색 보정) | 생성 | 993×1536 투명 PNG, 같은 위치. 사용 후보 |
| `Art2D/Portraits/Caster/Layers/Caster_Base.png`, `Caster_Face_*.png` | Claude Code | 생성 | 수작업 수정용 레이어(몸 공유 + 표정별 얼굴) |
| `Art Prompts/[Characters] 02_Party.txt` 1-E 섹션 | Claude Code | 수정 | 대화창 상반신 프롬프트 추가 |

#### 증거
- 없음

#### 주요 프롬프트
- 사용 도구: Claude Code
- 프롬프트 원문 또는 저장소 내 원문 파일 경로: "다이얼로그용 상반신 에셋도 뽑아봐. 주인공. 프롬 따로 적어뒀었지? / 일관성은 손으로 수정해야 하려나 ㅇㅅㅇ 최대한 보정해보고..."
- 결과 및 실제 활용 방식: 손 보정 없이 일관성을 맞추는 img2img + 얼굴 합성 방식을 정함
- 선정 이유: 표정 스프라이트 제작 방식을 결정한 요청

### [20261118-pull-fix] pull 충돌 해결
- 시각: 2026-11-18 17:16:00 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Opus 5.5) / git
- 목적 및 적용 위치: `packages-lock.json` 로컬 변경 때문에 막힌 pull 해결
- 사용 방식: 로컬 변경이 원격과 동일한 내용(com.unity.inputsystem 1.12.0 → 1.19.0, Unity 자동 수정)임을 확인 후 로컬 변경 폐기. pull이 추가하는 `Assets/Art.meta`와 충돌하는 로컬 미추적 `Art.meta`를 백업 후 제거하고 원격본 채택. `git pull --ff-only`
- 결과 및 확인 방법: 성공. fast-forward 4커밋 반영, `git status`로 확인. 남은 설정 파일 변경은 줄바꿈 차이뿐
- 사람의 개입: 해결 요청

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `ProjectBeViolenceDefense/Packages/packages-lock.json` | git | 수정 | 로컬 변경 폐기 후 원격본 사용 |
| `ProjectBeViolenceDefense/Assets/Art.meta` | git | 수정 | 로컬 생성본 대신 원격본 사용(폴더 메타만 해당, 하위 에셋 GUID 영향 없음) |

#### 증거
- 없음

### [20261118-enemies-armor-zombie-werewolf] 리빙 아머·좀비·늑대인간 3D 유닛
- 시각: 2026-11-18 17:50:00 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Opus 5.5) / NovelAI Diffusion V5 / OpenAI Images API (gpt-image-2.6-sunburst) / Meshy API / Unity CLI + Pipeline (Unity 6000.3.28f1)
- 목적 및 적용 위치: 적 3종 추가, `Assets/Art/Enemies/LivingArmor`, `Zombie`, `Werewolf`
- 사용 방식: `unit-3d-pipeline` 스킬 절차대로 NAI 컨셉 3장씩 생성 → 사용자 선택(1·2·1번) → GPT 4면 시트 → Meshy 1.5k 메시·리깅·동작(공격 4, 피격 179, 쓰러짐 189) → Unity 임포트. Meshy 단계는 Claude 하위 에이전트(Haiku)가 실행·감시
- 결과 및 확인 방법: 성공. headless Blender 6방향 렌더로 메시 확인(삼각형 1,546·1,506·1,557), Unity에서 X 55° 직교 카메라(AGENTS.md 테스트 카메라) 확인 씬을 Play Mode로 재생해 스크린샷 확인, 콘솔 오류 0
- 사람의 개입: 컨셉 선택

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `ProjectBeViolenceDefense/Assets/Art/Enemies/{LivingArmor,Zombie,Werewolf}/` (Walk·Combat FBX, Color PNG, .mat, .controller) | Meshy, Claude Code+Unity CLI | 생성 | 사용 후보 |
| `Art Prompts/[Enemies] 01_Enemies.txt` 1-D·1-E·1-F 섹션 | Claude Code | 수정 | 프롬프트 추가 |
| `Assets/Scenes/Test_Enemies3.unity`, 각 `Preview/` 컨트롤러 | Claude Code+Unity CLI | 생성 | 확인용, .gitignore로 제외 |

#### 증거
- 없음

#### Unity CLI 사용
- 구현하거나 수행한 작업: 3종 임포트 설정·머티리얼·애니메이터 생성, 55° 직교 카메라 확인 씬 생성과 Play Mode 캡처
- 활용 방식: 스킬의 `unity_setup_unit.cs`를 `script eval`로 실행, Play Mode 진입 후 `job wait`로 프레임 진행 확인
- 잘된 점: 세 유닛이 같은 스크립트로 한 번에 설정됨
- 잘 안된 점 및 개선 필요: 확인 씬 스크립트 수정 실수로 두 줄이 겹쳐 배치됨 → 위치 확인 후 수정

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 실제 게임 씬 카메라 크기에서의 가독성, 리빙 아머 왼쪽 옆모습은 GPT 4면에서 칼이 빠져 있었음(메시에는 칼 있음)

### [20261118-armor-sword-zombie-bald] 리빙 아머 칼 분리·교체, 좀비 민머리 재작업
- 시각: 2026-11-18 18:10:00 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Opus 5.5) / NovelAI Diffusion V5 / OpenAI Images API (gpt-image-2.6-sunburst) / Meshy API / Codex CLI 0.170.0 + Blender MCP / Unity CLI + Pipeline
- 목적 및 적용 위치: 리빙 아머 칼을 별도 소품으로 바꾸고, 좀비를 위에서 좀비로 읽히게 민머리로 변경. `Assets/Art/Enemies/LivingArmor`, `Assets/Art/Enemies/Zombie`
- 사용 방식: 칼은 NAI 원화 3장 → 1번 선택(Claude Code) → Meshy 500 메시 → Codex가 Blender에서 몸에 붙은 칼 면 61개 삭제·새 칼을 RightHand 뼈에 부착·동작별 손목 보정 → Claude Code가 Unity에서 칼의 비균일 스케일을 균일하게 수정해 프리팹 생성. 좀비는 확정된 GPT 4면 시트를 Images API로 "머리만 변경" 편집 → Meshy 메시·리깅·동작 → Unity 기존 파일 덮어쓰기(GUID 유지)
- 결과 및 확인 방법: 성공. headless Blender 렌더, Codex 확인 캡처(옆·55° 직교, 바닥선), Unity 55° 직교 확인 씬 Play Mode 캡처, 콘솔 오류 0
- 사람의 개입: 칼 재생성·좀비 머리 제거 지시

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `ProjectBeViolenceDefense/Assets/Art/Enemies/LivingArmor/` (`LivingArmor_WithSword.fbx`, `LivingArmor_Sword_Color.png`, `LivingArmor_Sword.mat`, `LivingArmor.controller`, `LivingArmor.prefab`) | Meshy, Codex+Blender, Claude Code+Unity CLI | 생성·수정 | 칼 분리 버전. 프리팹이 최종 사용 후보(칼 스케일 균일화 포함) |
| `ProjectBeViolenceDefense/Assets/Art/Enemies/Zombie/` (`Zombie_Walk.fbx`, `Zombie_Combat.fbx`, `Zombie_Color.png`) | OpenAI Images API, Meshy, Claude Code | 수정 | 모자·머리카락 제거한 민머리 좀비로 교체 |
| `Art Prompts/[Props] 03_Weapons.txt` 2-A | Claude Code | 수정 | 브로드소드 프롬프트 |

#### 증거
- 없음

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex CLI(Blender 작업 1건), Images API(좀비 4면 시트 편집 1건)
- 사용 방식: Claude Code가 요청서 작성·실행 후 캡처 검수
- 특별히 활용한 점: 확정된 4면 시트에서 머리만 바꾸는 편집으로 나머지 디자인 유지

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: Codex가 칼을 비균일 스케일로 붙여 Unity 프리팹에서 보정함(원본 FBX는 비균일 상태). 칼 손잡이 위치가 손바닥과 정확히 맞는지는 근접 확인 필요

### [20261118-art-tone-review] 프로젝트 아트·UI 톤 검토
- 시각: 2026-11-18 18:06:29 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / GPT-6, Unity CLI 1.0.0-beta.13 / Pipeline 1.0.0 / Unity 6000.3.28f1, 파일·이미지 열람, 웹 조회
- 목적 및 적용 위치: 현행 아트·프롬프트·레퍼런스와 남색·자주색 UI 팔레트의 조화 검토.
- 사용 방식: 로컬 레퍼런스와 A/B 목업, 캐릭터 초상·3D 시험 캡처를 비교하고 실제 GameScene의 준비·전투 HUD를 확인했다.
- 결과 및 확인 방법: 성공 / 실제 프레임 증가·준비/전투 캡처·Console groundTruth 오류 0 확인. 기존 씬과 실행 상태 복원. 게임 코드·프롬프트·에셋은 수정하지 않음. 세부 검토와 제한은 아래 매니페스트에 기록.
- 사람의 개입: 사용자가 네 UI 기본색과 갈바테인·명일방주·엠버워드 참고 대상을 제시. 제안 채택 여부 확인 필요. 작업 중 등장한 다른 에셋 변경은 이 작업의 산출물이 아니며 작업 주체 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ai-use/manifests/20261118-art-tone-review.md | Codex | 생성 | 검토·캡처 목록과 권고 기록. 기획 확정·구현 반영 없음 |
| ArtDirection/Reviews/20261118-art-tone/ | Unity CLI / Pipeline | 생성 | warmup·preparation·combat PNG 3장. 첫 캡처 제외, 나머지는 리뷰 증거로 사용 |

#### 증거
- ai-use/manifests/20261118-art-tone-review.md
- ArtDirection/Reviews/20261118-art-tone/preparation.png
- ArtDirection/Reviews/20261118-art-tone/combat.png

#### Unity CLI 사용
- 구현하거나 수행한 작업: 에디터·콘솔·씬 상태 확인, GameScene Play 및 전투 화면 관찰, 캡처, 초기 씬 복원.
- 활용 방식: Codex가 자료 비교와 판단을 담당하고 Unity CLI/Pipeline이 실제 에디터 조회·일회 전투 시작·프레임 확인·PNG 캡처를 수행.
- 잘된 점: 실제 HUD에 팔레트가 사용됨을 확인하고 레퍼런스 목업과 현행 구현을 구분함. Play 종료 후 Test_Enemies3 및 기존 백그라운드 설정 유지.
- 잘 안된 점 및 개선 필요: CLI 도움말은 구버전에서 일반 도움말을 반환하여 명령 카탈로그로 확인. 1600×900 화면에서 마나 숫자 줄바꿈 관찰. 코드 변경이나 빌드는 수행하지 않음.

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex의 파일·이미지 열람 및 실행 도구, 웹 조회.
- 사용 방식: 기존 이미지·프롬프트·코드를 검토하고 아트 방향을 제안. 신규 생성형 이미지 요청 없음.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 권고 방향 채택 여부, 최종 모델을 전장에 적용한 화면, 버프·힐 시전 효과와 혼전 가독성. ArtDirection 캡처는 gitignore 대상이라 팀 공유 여부 별도 확인.

### [20261118-art-tone-mockup-c] 현행 아트 방향 목업 요청 준비 및 생성 시도
- 시각: 2026-11-18 18:16:50 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / GPT-6, codex-art-request 로컬 CLI 실행 스크립트, 파일·이미지 열람
- 목적 및 적용 위치: 현행 GameScene·캐릭터·UI 팔레트를 반영한 전투 화면 목업 C 제작.
- 사용 방식: 상세 요청서를 작성하고 기존 이미지 6장을 복사해 로컬 Codex 작업 1건에 전달. 읽기 전용 로그인 점검 후 workspace-write 샌드박스로 실행.
- 결과 및 확인 방법: 부분 성공 / 요청서·참조 준비 완료. 실제 실행 도구는 registered Core launch requires an installed app 오류, 내장 imagegen 도구 미제공으로 생성 실패. 래퍼 exit 0이지만 이미지 산출물은 0개. 자동 재실행이나 API 호출 없음.
- 사람의 개입: 사용자가 현행 아트 방향의 목업 생성을 요청. API 대체 경로 동의 및 결과 선택은 미확인.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/C_CurrentDirection_20261118/REQUEST.md | Codex | 생성 | 두 적 팩션 교전·소녀 술사 코어·고정 UI 팔레트·입체 보드의 제작 명세. 준비 완료, 이미지 미생성 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/Reference/ | PowerShell 파일 복사 | 생성 | 기존 전투·준비·술사·고블린·해골·전장 스타일 이미지 6장 사본. 생성 입력으로 전달 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/README.md | Codex | 생성 | 실패 원인, 준비 파일, 미확인 사항 기록 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/_codex_last.txt 및 _codex_log.txt | 로컬 Codex CLI 래퍼 | 생성 | 실패 진단 기록. 작업용·커밋 제외 |

#### 증거
- ArtDirection/Mockups/C_CurrentDirection_20261118/README.md
- ArtDirection/Mockups/C_CurrentDirection_20261118/_codex_last.txt
- ArtDirection 폴더는 gitignore 대상. 팀 공유 여부 확인 필요.

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex 및 로컬 Codex CLI / 목업 요청 폴더.
- 사용 방식: 요청서와 레퍼런스 전달. 내장 이미지 생성 도구가 없어 실제 이미지 호출은 실행되지 않음. API 키는 환경 설정 존재 여부만 확인, 값 미출력.

#### 주요 프롬프트
- 사용 도구: Codex
- 프롬프트 원문: 한번 현행 아트 디렉션 기반으로 어떻게 작업해야 할지 목업 이미지 생성해볼래?
- 결과 및 실제 활용 방식: REQUEST.md와 참조 6장으로 구체화. 생성 실패로 적용된 목업 없음.
- 선정 이유: 현행 아트 방향을 실제 게임 구도로 시각화하는 제작 요청.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: API 대체 경로 사용 동의 또는 내장 imagegen 도구 복구. 목업 검토·분절·Unity 적용 미수행. 래퍼가 범위 밖 에셋 등 29개 변경을 감지했으나 이 작업에서 수정·복구하지 않았으며 변경 주체는 확인 필요.

### [20261118-art-tone-mockup-c-api] 현행 아트 방향 목업 C API 생성
- 시각: 2026-11-18 18:24:00 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / GPT-6, Image API gpt-image-2.6-sunburst high, imagegen CLI, OpenAI Python SDK 3.55.0, Pillow, 공식 문서 조회
- 목적 및 적용 위치: 현재 GameScene 구도와 파스텔 캐릭터·남색/자주색 UI를 합친 전투 화면 목업 제작.
- 사용 방식: 사용자 API 동의 후 공식 문서와 계정에서 모델 확인. 기존 CLI에 실제 화면·캐릭터 참조 6장과 프롬프트를 전달해 1장 생성.
- 결과 및 확인 방법: 성공 / CLI exit 0, 생성 52.2초, 1672×941 RGB PNG와 파일 해시 확인. 직접 이미지 열람으로 두 팩션 교전·술사 코어·6슬롯·숫자·잘림 검토. 게임 적용·Unity 실행 없음.
- 사람의 개입: 사용자가 API 사용에 동의하고 2.5의 상위 여부 질문. 확인 후 2.6 Sunburst high 선택을 설명하고 생성. 목업 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/C_CurrentDirection_20261118/BattleScreen_C_Mockup.png | Image API gpt-image-2.6-sunburst | 생성 | 미채택 전투 목업. 생성 원본 보존, 게임 적용 없음 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/PROMPTS.txt | Codex | 생성 | 실제 전송 프롬프트와 이미지 역할·순서 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/api-request.json | imagegen CLI dry-run | 생성 | 모델·품질·입출력 설정, 비밀정보 없음 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/REQUEST.md | Codex | 수정 | 사용자 API 동의와 선택 모델 반영 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/README.md | Codex | 수정 | 생성 결과·시각 검토·해상도 차이·구현 순서 |
| ArtDirection/Mockups/C_CurrentDirection_20261118/api-generation.log | imagegen CLI | 생성 | 성공·경과 시간 기록 |
| Tools/.imagegen-venv/ | Python venv / pip | 생성 | CLI용 격리 환경과 openai 3.55.0 설치. 작업용·커밋 제외 |

#### 증거
- ArtDirection/Mockups/C_CurrentDirection_20261118/README.md
- ArtDirection/Mockups/C_CurrentDirection_20261118/BattleScreen_C_Mockup.png
- ArtDirection/Mockups/C_CurrentDirection_20261118/api-generation.log
- https://developers.openai.com/api/docs/models/gpt-image-2.6-sunburst

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex, OpenAI Image API / 목업 폴더.
- 사용 방식: 사용자 환경 API 키를 프로세스 환경으로만 전달. 기존 image_gen.py edit 명령에 참조 6장, high, size=auto, 1장 요청. 키 값 출력·파일 저장 없음.
- 특별히 활용한 점: 오래된 로컬 기본 모델 안내를 공식 문서·계정 조회로 보정. 실제 게임 구조를 참조해 아군 카드·공격 타워 없는 목업 생성.

#### 주요 프롬프트
- 사용 도구: Codex / Image API
- 사용자 프롬프트 원문: ㅇㅇ. 근데 2.5가 더 낫지 않아? 그게 더 상위 아닌감...?
- 생성 프롬프트 원문 파일: ArtDirection/Mockups/C_CurrentDirection_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 2.6 Sunburst 목업 1장 생성. 이미지 검토와 다음 아트 방향 제안에 사용.
- 선정 이유: API 경로 사용 동의와 모델 선택 재검토를 반영한 요청.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 목업 채택 여부. 원본은 희망 2048×1152보다 작은 1672×941이며 확대하지 않음. 정확한 벽/타일 개수·경로·hex 일치는 이미지가 보증하지 않음. 분절·Unity 적용·혼전 효과 검증 미수행. 과금액·토큰 사용량 미확인. ArtDirection/Tools는 gitignore 대상이라 공유 여부 별도 확인.

### [20261118-enemies-batch3] 적 7종 추가(리빙 아머 v2·해골 궁수·네크로맨서·악마·고블린 투석병·오크·사신)와 무기 부착
- 시각: 2026-11-18 (세부 시각 확인 필요)
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Opus 5.5), NovelAI V5, OpenAI Images API(gpt-image-2.6-sunburst), Meshy API, Codex CLI 0.170.0 + Blender 4.5 MCP, Unity CLI
- 목적 및 적용 위치: 일반 적 유닛 3D 모델 확충. `ProjectBeViolenceDefense/Assets/Art/Enemies/{LivingArmor,SkeletonArcher,Necromancer,Demon,GoblinSlinger,Orc,Reaper}`
- 사용 방식: NAI 컨셉(사용자 선택) → GPT 4면도 → Meshy 메시(1.5k)·리깅·동작 → 무기는 GPT로 따로 만들어 Meshy 메시화 후 Codex가 Blender에서 손뼈에 부착(묶음 1·2, 네크로맨서 수정 1회) → Unity 프리팹 생성 스크립트로 가져오기
- 결과 및 확인 방법: 성공. Codex 검증 보고(무기 균일 스케일, 원점, 뒤집힌 프레임 0)와 측면·55° 캡처를 직접 확인. Unity에서 클립 4개(Walk/Attack/Hit/Death)와 프리팹 생성을 확인. 게임 코드 연결은 하지 않음(사용자 지시로 보류)
- 사람의 개입: 컨셉 선택(네크로맨서 1번, 악마 3번, 오크 1번, 사신 2번), 리빙 아머 흑색·무두 변경 지시, 사신 다리 노출 지시

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Assets/Art/Enemies/LivingArmor/` | Meshy, Codex, Unity CLI | 수정 | v2(흑색·무두)로 FBX·텍스처 교체, v1 동작 FBX 삭제. 사용 |
| `Assets/Art/Enemies/{SkeletonArcher,Necromancer,Demon,GoblinSlinger,Orc,Reaper}/` | NAI, GPT, Meshy, Codex, Unity CLI | 생성 | FBX, 텍스처, 머티리얼, 컨트롤러, 프리팹. 사용 |
| `Art Prompts/[Enemies] 01_Enemies.txt`, `[Props] 03_Weapons.txt` | Claude Code | 수정 | 1-G~1-M, 2-B, 무기 3-A·4-A 섹션 추가. 사용 |

#### 증거
- `Art3D/_PropAttach_Batch1/`, `_PropAttach_Batch1_fix/`, `_PropAttach_Batch2/`의 README·Check_*.png (git 제외 폴더)

#### Unity CLI 사용
- 구현하거나 수행한 작업: FBX 가져오기 설정, 머티리얼·컨트롤러·프리팹 생성
- 활용 방식: `script eval`로 스킬의 프리팹 스크립트를 유닛별 값으로 실행
- 잘된 점: 클립 이름 정리와 무기 스케일·원점 검사가 자동으로 보고됨
- 잘 안된 점 및 개선 필요: 관찰된 문제 없음

#### OpenAI 도구 사용
- 도구 및 사용 위치: Images API(4면도, 무기 단품), Codex CLI(Blender 무기 부착)
- 사용 방식: Claude가 요청서 작성 → Codex 실행 → 캡처·README를 Claude가 검토
- 특별히 활용한 점: 네크로맨서 지팡이 뒤집힘을 캡처에서 발견해 Codex 수정 작업으로 해결

#### 미확인 및 후속 확인
- 해골 궁수 공격 약 7.9초, 고블린 투석병 4.3초로 길어 게임에서 재생 속도 조절이나 구간 잘라내기가 필요
- 오크 손가락이 펼쳐져 있고, 사신 쓰러짐 자세에서 몸이 바닥 아래로 내려감(Codex 수정 범위 밖)

### [20261118-art-tone-grassland] 전투 목업 외곽 초원 배경 수정
- 시각: 2026-11-18 18:49:33 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / GPT-6, Image API gpt-image-2.6-sunburst high, imagegen CLI, Pillow
- 목적 및 적용 위치: 스테이지 뒤의 넓은 빈 공간을 초원으로 채운 전투 목업 수정안.
- 사용 방식: 이전에 승인된 API 경로로 원본 목업 1장을 참조하여 배경 중심 편집 1회. 전장·캐릭터·HUD 유지 조건을 프롬프트에 명시.
- 결과 및 확인 방법: 성공 / CLI exit 0, 47.9초, 1672×941 RGB PNG 생성. 직접 이미지 열람으로 초원·풀/꽃/바위·보드 접지감, 중앙 전투와 HUD의 큰 구성 유지 및 잘림 여부 확인. 기존 목업 원본 보존.
- 사람의 개입: 사용자가 스테이지 뒤 초원 느낌과 빈 공간 개선을 요청. 생성 결과의 최종 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/C_Grassland_20261118/BattleScreen_C_Grassland.png | Image API gpt-image-2.6-sunburst | 생성 | 남색 외곽을 초원으로 바꾼 검토용 목업. Unity 미적용 |
| ArtDirection/Mockups/C_Grassland_20261118/REQUEST.md 및 PROMPTS.txt | Codex | 생성 | 배경 편집 범위·유지 조건과 실제 API 프롬프트 |
| ArtDirection/Mockups/C_Grassland_20261118/README.md | Codex | 생성 | 생성 설정, 실제 해상도·해시·시각 검토·적용 참고사항 |
| ArtDirection/Mockups/C_Grassland_20261118/Reference/Current_Mockup.png | PowerShell 파일 복사 | 생성 | 이전 목업 사본, 수정 입력으로 사용 |
| ArtDirection/Mockups/C_Grassland_20261118/api-generation.log | imagegen CLI | 생성 | 생성 완료와 경과 시간 기록 |

#### 증거
- ArtDirection/Mockups/C_Grassland_20261118/BattleScreen_C_Grassland.png
- ArtDirection/Mockups/C_Grassland_20261118/README.md
- ArtDirection/Mockups/C_Grassland_20261118/api-generation.log

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex, OpenAI Image API / 새 목업 폴더.
- 사용 방식: 기존 API 동의에 따라 gpt-image-2.6-sunburst high로 원본 1장 편집. 키는 프로세스 환경으로만 사용하고 출력·파일 저장하지 않음. 생성 CLI 원본 변경 없음.
- 특별히 활용한 점: 원본 목업을 직접 참조해 전장과 HUD의 큰 구성은 유지하면서 외곽 환경만 초원으로 변경.

#### 주요 프롬프트
- 사용 도구: Codex / Image API
- 사용자 프롬프트 원문:
  > 살짝 뒤도 초원 느낌으로 하는게 낫지 않아? 스테이지 뒤의 백그라운드.
  > 너무 비는 공간이 많음.
- 생성 프롬프트 원문 파일: ArtDirection/Mockups/C_Grassland_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 배경 수정안 1장 생성·검토. 게임 반영 없음.
- 선정 이유: 전투 화면의 외곽 환경과 공간 밀도를 결정하는 아트 피드백.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 결과 채택 여부. 전경 픽셀의 완전한 동일성·실제 게임의 시인성은 미검증. 잔디 위 마나 숫자의 대비 보강 검토. 분절·Unity 적용 없음. ArtDirection은 gitignore 대상이라 팀 공유 여부 별도 확인.

### [20261118-art-tone-full-direction-d] 프로토타입 구도를 벗어난 D안 전투 목업
- 시각: 2026-11-18 18:56:22 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / imagegen 스킬의 image_gen.py / OpenAI Image API gpt-image-2.6-sunburst high / Pillow
- 목적 및 적용 위치: 기존 C안이 프로토타입 강화 수준이라는 사용자 피드백을 반영하여 환경·교전·버튼·HUD의 완성작 아트 방향을 제안.
- 사용 방식: 레퍼런스 게임 3장과 기존 캐릭터/필드 스타일 4장만 입력하고 프로토타입 및 C안 이미지 입력은 제외. 요청서와 프롬프트를 작성한 뒤 기존 승인 API 경로로 1회 생성.
- 결과 및 확인 방법: 성공. API 완료 61.6초, 원본 PNG 1672×941 RGB, 3,002,173 bytes. 이미지를 직접 열어 초원 유적·물길·중앙 교전·힐·버프·마법서 버튼·코어 구성을 확인. 금색 장식 반복, 큰 하단 HUD, 회화적 표현 등 요청과의 차이를 README에 기록. Unity 코드·씬·에셋 변경 및 Unity CLI 실행 없음.
- 사람의 개입: 사용자가 기존 프로토타입 구도에 대한 교정과 다채롭고 다이나믹한 목업을 요청함. D안 검토 및 채택 여부는 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/D_FullArtDirection_20261118/REQUEST.md | Codex | 생성 | 환경·UI·교전 재설계 요청서. 생성에 사용. |
| ArtDirection/Mockups/D_FullArtDirection_20261118/PROMPTS.txt | Codex | 생성 | 실제 API 입력 프롬프트. 생성에 사용. |
| ArtDirection/Mockups/D_FullArtDirection_20261118/Reference/ | 파일 복사 | 생성 | 기존 참조 7장 사본. 이미지 입력에 사용; 원본 수정 없음. |
| ArtDirection/Mockups/D_FullArtDirection_20261118/BattleScreen_D_Mockup.png | OpenAI Image API gpt-image-2.6-sunburst | 생성 | 초원 유적과 마법서 HUD 전투 목업. 미채택 제안, 게임 적용 없음. |
| ArtDirection/Mockups/D_FullArtDirection_20261118/api-generation.log | image_gen.py | 생성 | 1회 생성 완료 기록. |
| ArtDirection/Mockups/D_FullArtDirection_20261118/README.md | Codex | 생성 | 크기·해시·시각 검토·요청과의 차이 및 실제 구현 검증 범위 기록. |

#### 증거
- ArtDirection/Mockups/D_FullArtDirection_20261118/api-generation.log
- ArtDirection/Mockups/D_FullArtDirection_20261118/BattleScreen_D_Mockup.png
- ArtDirection/Mockups/D_FullArtDirection_20261118/README.md
- PNG SHA-256: 9c98fdbc12c77540b0d793f990227f360b475d61ac0d8d2509e5743b040d5045

#### OpenAI 도구 사용
- 도구 및 사용 위치: OpenAI Image API, D안 목업 폴더.
- 사용 방식: 이미지 7장을 참조하는 edit 요청, high 품질, 1장 생성. 이전 목업 배치를 고정하지 않는 프롬프트를 전달하고 결과를 육안 검토.
- 특별히 활용한 점: 게임별 참조 역할을 환경 입체감·따뜻한 색·정보 위계로 나누고, 실제 캐릭터 정체성을 별도 입력.

#### 주요 프롬프트
- 사용 도구: Codex, OpenAI Image API
- 사용자 프롬프트 원문:
> 지금 이미지는 살짝 그냥 프로토타입의 강화판이잖아? 저건 어디까지나 프로토타입인걸. 다른 사람들은 아트 디렉션이 구체적으로 어떻게 짜여있는지도 몰라.
> 버튼도 그렇고 레퍼런스 게임에 맞게 다이나믹하고 다채롭게 생성해봐. 목업.
- API 프롬프트 원문: ArtDirection/Mockups/D_FullArtDirection_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 프로토타입의 외곽·벽·HUD 배치 고정을 해제한 새 목업 생성. 분절 및 Unity 적용 없음.
- 선정 이유: 기존 시안의 구도 제약을 제거하고 버튼·환경·액션의 아트 제안을 구체화한 핵심 교정.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: D안의 동화풍 강도와 HUD 장식 채택 여부. 실제 3D 구현, 카메라 가독성, 벽 배치 및 유효 경로, 숫자/적 수 일치는 이번 생성에서 검증하지 않음.

### [20261118-enemy-bat] 비행형 적: 큰 박쥐
- 시각: 2026-11-18 (세부 시각 확인 필요)
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Opus 5.5), NovelAI V5, OpenAI Images API(gpt-image-2.6-sunburst), Meshy API, Codex CLI 0.170.0 + Blender 4.5 MCP, Unity CLI
- 목적 및 적용 위치: 날갯짓하는 비행형 적. `ProjectBeViolenceDefense/Assets/Art/Enemies/Bat/`
- 사용 방식: NAI 컨셉 3안 → 사용자 1안 선택 → GPT 4면도 → Meshy 메시(1,528 tris) → Codex가 Blender에서 뼈 26개 리깅과 동작 4개(날갯짓 반복 `Walk`, 돌진 물기 `Attack`, `Hit`, 추락 `Death`) 제작 → Unity 가져오기·프리팹
- 결과 및 확인 방법: 성공. Codex 캡처(측면·55°) 직접 확인, Unity Play Mode 55° 직교 미리보기 씬(Test_Enemies4, git 제외)에서 새 적 8종과 함께 재생 확인
- 사람의 개입: 컨셉 1안 선택

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Assets/Art/Enemies/Bat/` (Bat.fbx, Bat_Color.png, Bat.mat, Bat.controller, Bat.prefab) | NAI, GPT, Meshy, Codex, Unity CLI | 생성 | 비행 높이 약 0.8 m가 모델에 포함, 원점은 바닥. 사용 |
| `Art Prompts/[Enemies] 01_Enemies.txt` 2-B | Claude Code | 수정 | 박쥐 섹션. 사용 |

#### 증거
- `Art3D/Bat/Blender/README.md`, `Anim_*_side.png`, `Anim_*_top55.png` (git 제외 폴더)

#### Unity CLI 사용
- 구현하거나 수행한 작업: 박쥐 가져오기·프리팹, 새 적 8종 55° 미리보기 씬, Play Mode 캡처
- 활용 방식: `script eval`로 설정·씬 생성, `editor.playmode start` 후 `ScreenCapture`, 뼈 위치 측정
- 잘된 점: 뼈 높이를 직접 재서 쓰러짐 칸이 T자세처럼 보이는 것이 오류가 아니라 시점 착시임을 확인
- 잘 안된 점 및 개선 필요: 확인 중 Play Mode가 한 번 꺼져 있었음(원인 확인 필요). 위에서 보면 뒤로 누운 시체가 서 있는 T자세처럼 보임

#### 미확인 및 후속 확인
- 게임에서 시체를 사라지게(페이드·가라앉기) 처리할지, 앞으로 엎어지는 쓰러짐 동작으로 바꿀지 결정 필요

### [20261118-art-tone-restrained-e] 첨부 화면에 절제된 디테일을 추가한 E안
- 시각: 2026-11-18 19:18:40 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / imagegen 스킬 image_gen.py / OpenAI Image API gpt-image-2.6-sunburst high / Python Pillow
- 목적 및 적용 위치: 사용자가 과한 장식의 D안을 거절하고 지정한 원본 화면에 디테일을 더하도록 요청함. 기존 구도와 차분한 톤을 유지한 검토용 목업 제작.
- 사용 방식: 사용자 첨부 이미지를 유일한 편집 입력으로 사용. HUD·입구·일부 외곽에 마스크를 제한하여 1회 생성 후 허용 영역만 원본 위에 합성. 모델이 바꾼 체력 HUD와 스킬 그림은 원본에서 복원.
- 결과 및 확인 방법: 성공. API 완료 45.4초. 최종 1672×941 RGB PNG, 2,140,001 bytes. 전체 이미지와 UI 확대본 육안 확인. 최종 허용 영역 밖 87.45% 픽셀의 원본 동일성 검증 성공. 분절·Unity 적용·Unity CLI 실행 없음.
- 사람의 개입: 사용자가 기준 이미지를 직접 지정하고 과한 장식을 배제하도록 교정함. E안 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/REQUEST.md | Codex | 생성 | 사용자 원문과 절제된 편집 범위. 생성에 사용. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/PROMPTS.txt | Codex | 생성 | 실제 API 전송 프롬프트. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/Reference/UserBase.png | 파일 복사 | 생성 | 사용자 첨부 원본 사본. 유일한 이미지 입력. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/local_edit_regions.py | Codex / Pillow | 생성·수정 | 마스크 생성 및 원본 보존 합성. 체력 HUD·스킬 그림 복원. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/EditMask.png, EditRegions.png, FinalEditRegions.png | Pillow | 생성 | 모델 편집 마스크와 최종 합성 영역. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/BattleScreen_E_Generated.png | OpenAI Image API | 생성 | 1회 생성한 편집 원본. 증거용 보존, 직접 적용 안 함. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/BattleScreen_E_Mockup.png | OpenAI Image API + Pillow 합성 | 생성 | 작은 깃발·테두리·버튼 깊이·일부 식생 추가. 검토용 최종 목업, 미채택·미적용. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/UI_Review.png | Pillow | 생성·수정 | 최종 하단 HUD 확대 검토본. |
| ArtDirection/Mockups/E_RestrainedAdditions_20261118/README.md, validation.json, api-generation.log | Codex / Pillow / imagegen CLI | 생성 | 시각 검토, 원본 복원, 생성·보존 검증 기록. |

#### 증거
- ArtDirection/Mockups/E_RestrainedAdditions_20261118/api-generation.log
- ArtDirection/Mockups/E_RestrainedAdditions_20261118/validation.json
- ArtDirection/Mockups/E_RestrainedAdditions_20261118/README.md
- 최종 PNG SHA-256: 914818b8502934fef3bd60fffeaab9bb58c0fc8be5b9235e5845241131c7f685

#### OpenAI 도구 사용
- 도구 및 사용 위치: OpenAI Image API, E안 목업 폴더.
- 사용 방식: 기존 승인 CLI/API 경로의 high 품질 단일 edit. 이미지와 마스크를 입력하여 제한된 디테일을 그린 뒤 Pillow로 원본과 합성.
- 특별히 활용한 점: 생성 모델의 전체 화면 재해석을 그대로 채택하지 않고 원본 보존 영역을 기계적으로 고정. 체력 바 제거와 스킬 그림 단순화는 원본 복원으로 수정.

#### 주요 프롬프트
- 사용 도구: Codex / OpenAI Image API
- 사용자 프롬프트 원문:
> 여기다가 추가하는 식으로 꾸며봐.
> 저건 과한 ai 슬롭임.
- 실제 이미지 입력: ArtDirection/Mockups/E_RestrainedAdditions_20261118/Reference/UserBase.png
- API 프롬프트 원문: ArtDirection/Mockups/E_RestrainedAdditions_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 추가 편집 범위를 제한한 E안 목업. 사용자 검토용으로 제시, Unity 적용 없음.
- 선정 이유: 완성 화면 재설계에서 사용자 지정 화면의 절제된 추가 편집으로 방향을 바로잡은 핵심 교정.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: E안 디테일과 하단 통합 패널 채택 여부. 런타임 조작·배치 경로·실제 3D 구현은 검증하지 않음.

### [20261118-status-hud-f] 좌상단 HUD의 정보 구조 및 아트 재설계
- 시각: 2026-11-18 19:29:23 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / OpenAI Image API gpt-image-2.6-sunburst high / 기존 imagegen 스킬 image_gen.py / Python Pillow
- 목적 및 적용 위치: 장식만 추가한 이전 목업에 대한 피드백을 반영하여 좌상단 상태 HUD를 명일방주·갈바테인·프로젝트 원화 기반으로 재설계.
- 사용 방식: 실제 DefenseHud.Refresh와 DefenseBattle의 카운터 의미를 읽어 18/18이 출현 진행임을 확인. 코어 체력 위계 강화, 출현 진행과 팩션별 생존 수 분리, 작은 캐릭터 초상 및 사선 구획을 설계. 7장 참조와 마스크로 단일 이미지 생성 후 좌상단만 합성.
- 결과 및 확인 방법: 성공. API 47.5초, 생성 원본 1399×1124. 전체 목업 1672×941, HUD 350×214. 확대·전체·실제 배치 크기에서 문구와 수치 육안 확인. HUD 외부 약 95.45% 픽셀 동일성 검증 성공. 팩션 수 5/5는 목업 예시이며 새 표시 기능은 미구현.
- 사람의 개입: 사용자가 변경할 좌상단 영역과 참조 방향을 직접 지정함. 최종 F안 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/F_StatusHud_20261118/REQUEST.md, PROMPTS.txt | Codex | 생성 | 정보 구조·색상·상태 제안 및 실제 API 프롬프트. 생성에 사용. |
| ArtDirection/Mockups/F_StatusHud_20261118/Reference/ | 파일 복사 / Pillow | 생성 | 사용자 첨부, 기준 전장, 확대 편집 대상, 게임 및 캐릭터 참조. 원본 미수정. |
| ArtDirection/Mockups/F_StatusHud_20261118/EditMask.png | Pillow | 생성 | 좌상단 편집 범위. API 입력에 사용. |
| ArtDirection/Mockups/F_StatusHud_20261118/StatusHud_F_Generated.png | OpenAI Image API | 생성 | 좌상단 HUD 생성 원본. 보존. |
| ArtDirection/Mockups/F_StatusHud_20261118/StatusHud_F_Closeup.png | Pillow | 생성 | 원본에서 자른 확대 검토본. 미채택 제안. |
| ArtDirection/Mockups/F_StatusHud_20261118/BattleScreen_F_Mockup.png | OpenAI Image API + Pillow | 생성 | 좌상단만 교체한 전체 화면 목업. Unity 미적용. |
| ArtDirection/Mockups/F_StatusHud_20261118/StatusHud_F_GameScale.png | Pillow | 생성 | 실제 배치 크기 확인본. |
| ArtDirection/Mockups/F_StatusHud_20261118/compose_hud.py, CompositeRegion.png, validation.json | Codex / Pillow | 생성 | 관찰한 패널 외곽으로 전체 HUD 합성, 외부 픽셀 보존 검증. 생산용 분절 아님. |
| ArtDirection/Mockups/F_StatusHud_20261118/README.md, api-generation.log | Codex / imagegen CLI | 생성 | 시각 검토·차이점·데이터 의미·실행 기록. |

#### 증거
- ArtDirection/Mockups/F_StatusHud_20261118/api-generation.log
- ArtDirection/Mockups/F_StatusHud_20261118/validation.json
- ArtDirection/Mockups/F_StatusHud_20261118/README.md
- 전체 목업 SHA-256: 0ece110fc06b1e8f5aefc7a02404ce681951e4d938495f8a23e089c3f8647ef7
- 데이터 의미 확인: ProjectBeViolenceDefense/Assets/Scripts/DefenseHud.cs의 Refresh 및 DefenseBattle.cs의 SpawnCount, Enemies. 읽기만 수행.

#### OpenAI 도구 사용
- 도구 및 사용 위치: 승인된 fallback CLI 경로의 OpenAI Image API, F안 폴더.
- 사용 방식: 참조 역할을 분리한 7장 입력과 국소 마스크, high 품질 1회 생성. 배경과 생성 원본의 상단 노이즈 띠는 제외하고 HUD만 비율 유지 축소·합성.
- 특별히 활용한 점: 실제 코드의 출현 카운터 의미를 시각 구조에 반영하고 생존 인원 표시를 별도 제안. 체력 대상은 실제 소녀 원화의 얼굴로 연결.

#### 주요 프롬프트
- 사용 도구: Codex / OpenAI Image API
- 사용자 프롬프트 원문:
> ui/UX적으로 추가된게 아무도 없는데...?
> 이 좌상단 ui를 갈바테인/명일방주/우리 아트 디렉팅 풍으로 바꿔보자.
- API 프롬프트 원문: ArtDirection/Mockups/F_StatusHud_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 좌상단의 정보 위계와 생존 수 표시를 포함한 목업, 미구현·미채택 제안.
- 선정 이유: 단순 미술 장식에서 상태 정보 구조와 기능 표시 제안으로 작업 범위를 명확히 교정.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: HUD의 커진 점유 면적과 초상 크기, 생존 수 표시 및 인원 비율 띠 채택 여부. 실제 런타임 수치 갱신·툴팁·피격 반응은 구현하거나 검증하지 않음. Unity CLI 실행 없음.

### [20261118-status-hud-f2] 상단이 긴 형태와 약한 그라데이션 반영
- 시각: 2026-11-18 19:34:38 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / imagegen 스킬 image_gen.py / OpenAI Image API gpt-image-2.6-sunburst high / Python Pillow
- 목적 및 적용 위치: 방향이 승인된 F안 좌상단 HUD의 상부를 더 길게 만들고 패널에 은은한 그라데이션 추가.
- 사용 방식: F안 원본을 단일 참조로 편집 1회. 새 패널 외곽을 관찰해 기존 전장에 비율 유지 합성. 정보 구조·수치의 의미 유지.
- 결과 및 확인 방법: 성공. API 39.4초. 확대본 및 전체 화면 육안 확인. 상부 폭 1451px, 하단 폭 1271px로 상부가 약 14% 긴 것 확인. 패널 빈 면 RGB (45,34,63)→(71,50,75)의 남색/자주색 변화 확인. 전체 화면의 HUD 밖 약 94.84% 픽셀 동일성 검증 성공.
- 사람의 개입: 사용자가 기존 디자인 방향을 긍정하고 외곽 비율·그라데이션 강도를 교정. F2 최종 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/F2_UpperWideGradient_20261118/REQUEST.md, PROMPTS.txt | Codex | 생성 | 사용자 교정과 실제 편집 프롬프트. 생성에 사용. |
| ArtDirection/Mockups/F2_UpperWideGradient_20261118/Reference/StatusHud_F_WithRoom.png | Pillow | 생성 | F안 원본에서 잘라 편집 여유를 확보한 입력. |
| ArtDirection/Mockups/F2_UpperWideGradient_20261118/StatusHud_F2_Generated.png | OpenAI Image API | 생성 | 상단 확장·하단 들임·약한 그라데이션의 편집 원본. 보존. |
| ArtDirection/Mockups/F2_UpperWideGradient_20261118/StatusHud_F2_Closeup.png, BattleScreen_F2_Mockup.png, StatusHud_F2_GameScale.png | OpenAI Image API + Pillow | 생성 | 확대·전체 화면·실제 크기 검토본. 게임 미적용. |
| ArtDirection/Mockups/F2_UpperWideGradient_20261118/compose_hud.py, CompositeRegion.png, validation.json | Codex / Pillow | 생성 | 전체 HUD 합성 및 외부 픽셀 보존·폭 검증. 생산용 분절 아님. |
| ArtDirection/Mockups/F2_UpperWideGradient_20261118/README.md, api-generation.log | Codex / imagegen CLI | 생성 | 결과·원본과의 차이·실행 증거. |

#### 증거
- ArtDirection/Mockups/F2_UpperWideGradient_20261118/api-generation.log
- ArtDirection/Mockups/F2_UpperWideGradient_20261118/validation.json
- ArtDirection/Mockups/F2_UpperWideGradient_20261118/README.md

#### OpenAI 도구 사용
- 도구 및 사용 위치: 승인된 fallback CLI 경로의 OpenAI Image API, F2안 폴더.
- 사용 방식: 이전 디자인의 외곽과 그라데이션만 지정하여 단일 edit 호출. 생성 후 원본 프레임과 같은 높이로 합성하여 확인.

#### 주요 프롬프트
- 사용 도구: Codex / OpenAI Image API
- 프롬프트 원문:
> 디자인 방향은 괜찮은데, 상단부가 더 긴게 낫겠다. 지금은 하반신이 더 기니까.
> 그리고 살짝 단조로운데 그라데이션 약하게나마 넣어볼 수 있어?
- API 원문: ArtDirection/Mockups/F2_UpperWideGradient_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 상부가 긴 외곽과 낮은 대비의 그라데이션을 적용한 목업. Unity 적용 없음.
- 선정 이유: 승인 방향을 유지하며 형태와 면의 단조로움을 개선한 구체적인 사용자 교정.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 커진 가로 폭과 그라데이션 강도 채택 여부. 코드·씬·생산 에셋 수정 및 Unity CLI 실행 없음.

### [20261118-status-hud-f3] 큰 세로 초상을 중심으로 좌측 HUD 재배치
- 시각: 2026-11-18 19:40:08 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / imagegen 스킬 image_gen.py / OpenAI Image API gpt-image-2.6-sunburst high / Python Pillow
- 목적 및 적용 위치: 사용자가 포트레잇 공간 확대와 세로형 구성을 제안하여, 왼쪽 여백을 사용하는 큰 초상 중심 HUD 목업 제작.
- 사용 방식: F2의 승인 방향, 원본 소녀 초상, 전체 화면을 입력하여 세로형 1회 생성. 라운드→초상→체력→출현→팩션 수 순으로 재배치하고 실제 전장에 합성.
- 결과 및 확인 방법: 성공. API 44.0초, 원본 1024×1536. 전체 목업 1672×941, HUD 282×584, 초상 프레임 약 244×283. 확대·전체·실제 크기에서 수치와 구성 육안 확인. HUD 외부 약 89.80%와 왼쪽 진입/깃발 영역의 기준 전장 동일성 검증 성공.
- 사람의 개입: 사용자가 화면의 빈 느낌을 지적하고 초상 확대·세로 구성을 제안. F3안 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/F3_PortraitTall_20261118/REQUEST.md, PROMPTS.txt | Codex | 생성 | 사용자 원문, 세로 배치·초상 범위, 실제 API 프롬프트. 생성에 사용. |
| ArtDirection/Mockups/F3_PortraitTall_20261118/Reference/ | 파일 복사 | 생성 | F2 HUD·원본 소녀·F2 전체 화면 입력 사본. 원본 미수정. |
| ArtDirection/Mockups/F3_PortraitTall_20261118/StatusHud_F3_Generated.png | OpenAI Image API | 생성 | 큰 상반신 초상과 세로형 정보 배열의 생성 원본. 보존. |
| ArtDirection/Mockups/F3_PortraitTall_20261118/BattleScreen_F3_Mockup.png, StatusHud_F3_Closeup.png, StatusHud_F3_GameScale.png | OpenAI Image API + Pillow | 생성 | 전체 화면·확대·실제 배치 크기 검토본. 게임 미적용. |
| ArtDirection/Mockups/F3_PortraitTall_20261118/compose_hud.py, CompositeRegion.png, validation.json | Codex / Pillow | 생성 | 전체 HUD 합성, 외부 픽셀 및 진입 지점 보존 검증. 생산용 분절 아님. |
| ArtDirection/Mockups/F3_PortraitTall_20261118/README.md, api-generation.log | Codex / imagegen CLI | 생성 | 생성·배치·검토와 목표 대비 실제 규격 기록. |

#### 증거
- ArtDirection/Mockups/F3_PortraitTall_20261118/api-generation.log
- ArtDirection/Mockups/F3_PortraitTall_20261118/validation.json
- ArtDirection/Mockups/F3_PortraitTall_20261118/README.md

#### OpenAI 도구 사용
- 도구 및 사용 위치: 승인된 fallback CLI 경로의 OpenAI Image API, F3안 폴더.
- 사용 방식: 원화의 머리·어깨·가슴·지팡이를 보여주는 세로 포트레잇을 기존 HUD 언어로 재구성. 1회 생성 후 비율 유지 합성하여 전체 화면 검토.

#### 주요 프롬프트
- 사용 도구: Codex / OpenAI Image API
- 프롬프트 원문:
> 흠...
> 포트레잇 공간을 좀 더 키우고, 세로로 길게 하면 어때?
> 지금 전체 화면 자체가 꽤 비워져있는 느낌임 ㅇㅅㅇ
- API 원문: ArtDirection/Mockups/F3_PortraitTall_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 가로 상태 패널을 큰 세로 초상 중심으로 바꾼 검토용 목업. Unity 미적용.
- 선정 이유: 초상 면적과 화면의 시각적 밀도를 조정하는 핵심 레이아웃 교정.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 높아진 HUD 점유 면적과 초상 크기 채택 여부. 생성 결과가 목표보다 길쭉하여 282×584로 배치함. 코드·씬·생산 에셋 수정 및 Unity CLI 실행 없음.

### [20261118-status-hud-f4] 체력·출현·팩션 수를 세로 초상 우측으로 이동
- 시각: 2026-11-18 19:44:24 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / imagegen 스킬 image_gen.py / OpenAI Image API gpt-image-2.6-sunburst high / Python Pillow
- 목적 및 적용 위치: 진행 중 사용자가 첨부한 하단 정보 블록을 포트레잇 오른쪽으로 옮기도록 교정하여 좌상단 HUD를 2열로 재배치.
- 사용 방식: F3 전체 HUD와 사용자 첨부 블록을 입력하여 편집 1회. 큰 세로 초상은 왼쪽, 정보 3단은 오른쪽, 공통 헤더는 상단에 배치. 전체 전장에 비율 유지 합성.
- 결과 및 확인 방법: 성공. API 38.8초, 원본 1536×1024. 전체 목업 1672×941, HUD 460×289, 초상 프레임 약 195×227. 확대·전체·실제 크기 육안 확인. HUD 밖 약 91.60%와 왼쪽 벽·진입·깃발 영역의 기준 전장 동일성 검증 성공.
- 사람의 개입: 사용자가 이동할 정보 블록을 이미지로 지정하고 초상 우측 배치를 제안. F4안 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/F4_PortraitRightStats_20261118/REQUEST.md, PROMPTS.txt | Codex | 생성 | 사용자 교정, 2열 배치, 실제 API 문구. 생성에 사용. |
| ArtDirection/Mockups/F4_PortraitRightStats_20261118/Reference/ | 파일 복사 | 생성 | F3 HUD와 사용자 첨부 정보 블록 사본. 원본 미수정. |
| ArtDirection/Mockups/F4_PortraitRightStats_20261118/StatusHud_F4_Generated.png | OpenAI Image API | 생성 | 세로 초상 왼쪽·정보 오른쪽의 편집 원본. 보존. |
| ArtDirection/Mockups/F4_PortraitRightStats_20261118/BattleScreen_F4_Mockup.png, StatusHud_F4_Closeup.png, StatusHud_F4_GameScale.png | OpenAI Image API + Pillow | 생성 | 전체 화면·확대·실제 배치 크기 목업. 게임 미적용. |
| ArtDirection/Mockups/F4_PortraitRightStats_20261118/compose_hud.py, CompositeRegion.png, validation.json | Codex / Pillow | 생성 | 전체 HUD 합성, 바깥 영역·벽·진입 지점 검증. 생산용 분절 아님. |
| ArtDirection/Mockups/F4_PortraitRightStats_20261118/README.md, api-generation.log | Codex / imagegen CLI | 생성 | 작업·배치·실제 검토 기록. |

#### 증거
- ArtDirection/Mockups/F4_PortraitRightStats_20261118/api-generation.log
- ArtDirection/Mockups/F4_PortraitRightStats_20261118/validation.json
- ArtDirection/Mockups/F4_PortraitRightStats_20261118/README.md

#### OpenAI 도구 사용
- 도구 및 사용 위치: 승인된 fallback CLI 경로의 OpenAI Image API, F4안 폴더.
- 사용 방식: 첨부 정보 영역을 세로 초상 옆으로 이동하는 2열 레이아웃을 생성. 1회 호출 후 전체 화면에 합성해 가독성과 가림을 확인.

#### 주요 프롬프트
- 사용 도구: Codex / OpenAI Image API
- 프롬프트 원문:
> 이걸 포트레잇 우측으로 보내는게 어때?
- 사용자 첨부 사본: ArtDirection/Mockups/F4_PortraitRightStats_20261118/Reference/MoveThisBlock.png
- API 원문: ArtDirection/Mockups/F4_PortraitRightStats_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 초상과 상태 영역을 좌우로 배치한 검토용 목업. Unity 미적용.
- 선정 이유: F3의 긴 세로 정보 배열을 두 열로 묶는 핵심 사용자 교정.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 2열 모듈의 점유 면적과 초상 크기 채택 여부. 초상은 F3보다 약 20% 작게 배치됨. 코드·씬·생산 에셋 수정 및 Unity CLI 실행 없음.

### [20261118-status-hud-f5] 우측 상단 상태 영역 가로 연장
- 시각: 2026-11-18 19:47:03 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / Python Pillow. 이미지 API 추가 호출 없음.
- 목적 및 적용 위치: 사용자가 지정한 헤더·체력·출현 영역을 우측으로 길게 확장한 목업.
- 사용 방식: F4 원본의 빈 면·구분선·체력 바만 구간 확장. 글자·아이콘·초상은 변형 없이 유지. 하단 팩션 수는 기존 폭 유지. 새 돌출부의 모서리를 사선으로 처리.
- 결과 및 확인 방법: 성공. HUD 폭 460→536px, 높이 289px 유지. 확대·전체·실제 크기 확인. 원본 초상/팩션 하단 동일성, HUD 밖 및 왼쪽 벽의 기준 전장 동일성 검사 통과.
- 사람의 개입: 사용자가 첨부 이미지로 확장 범위를 지정. 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/F5_StatusExtended_20261118/Reference/RequestedRegion.png | 파일 복사 | 생성 | 사용자 첨부 사본. 범위 확인에 사용. |
| ArtDirection/Mockups/F5_StatusExtended_20261118/extend_status.py | Codex / Pillow | 생성·수정 | 래스터 구간 확장, 원본 보존 합성, 사선 모서리 처리. |
| ArtDirection/Mockups/F5_StatusExtended_20261118/BattleScreen_F5_Mockup.png, StatusHud_F5_Closeup.png, StatusHud_F5_GameScale.png | Pillow | 생성·수정 | 전체·확대·실제 크기 목업. 게임 미적용. |
| ArtDirection/Mockups/F5_StatusExtended_20261118/README.md, CompositeRegion.png, validation.json | Codex / Pillow | 생성·수정 | 편집 내역과 검증 증거. |

#### 증거
- ArtDirection/Mockups/F5_StatusExtended_20261118/validation.json
- ArtDirection/Mockups/F5_StatusExtended_20261118/README.md

#### OpenAI 도구 사용
- 도구 및 사용 위치: Codex에서 로컬 이미지 편집 스크립트 작성 및 실행. 이미지 생성 API는 호출하지 않음.
- 사용 방식: 사용자 지정 범위의 기하 변경을 기존 그림으로 처리하고 결과를 육안 확인.

#### 주요 프롬프트
- 사용 도구: Codex
- 프롬프트 원문:
> 이걸 우측으로 좀 늘리자.
> 요게 좀 길쭉해지게.
- 결과 및 실제 활용 방식: 상단부를 약 76px 늘린 F5 검토 목업.
- 선정 이유: 첨부 범위의 가로 비율을 조정하는 사용자 교정.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 확장 폭 채택 여부. 코드·씬·생산 에셋 수정 및 Unity CLI 실행 없음.

### [20261118-status-hud-f6] 오른쪽 상태 패널을 이미지 모델로 채색
- 시각: 2026-11-18 19:50:13 +09:00
- 작업자: ParkYeongseo
- 사용 도구: Codex / imagegen image_gen.py / OpenAI Image API gpt-image-2.6-sunburst high / Python Pillow
- 목적 및 적용 위치: 평면적인 아트라는 사용자 피드백에 따라 오른쪽 헤더·체력·출현 패널에 그림으로 표현한 재질과 면의 깊이 추가.
- 사용 방식: F5 확대본과 국소 마스크로 API 편집 1회. 지정 영역만 원본에 합성하여 초상과 하단 팩션 등 나머지 영역 보존.
- 결과 및 확인 방법: 성공. API 38.3초. 생성 원본 1696×927, 전체 목업 1672×941. 원본·확대·전체 화면 육안 확인. 초상과 편집 영역 밖 픽셀 동일성 검사 통과. 전투 표식 위치와 출현 문구의 한 줄 배치는 모델이 변경한 사항으로 기록.
- 사람의 개입: 사용자가 해당 영역을 이미지 생성 모델로 그리도록 명시. 채택 여부 확인 필요.

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| ArtDirection/Mockups/F6_PaintedStatus_20261118/REQUEST.md, PROMPTS.txt | Codex | 생성 | 요청 범위 및 실제 API 문구. 사용됨. |
| ArtDirection/Mockups/F6_PaintedStatus_20261118/Reference/CurrentHud.png, EditMask.png, EditRegion.png | 파일 복사 / Pillow | 생성 | 입력 이미지 및 편집 영역. API와 합성에 사용. |
| ArtDirection/Mockups/F6_PaintedStatus_20261118/StatusHud_F6_Generated.png | OpenAI Image API | 생성 | 손으로 채색한 면·테두리·체력 바 표현. 원본 보존. |
| ArtDirection/Mockups/F6_PaintedStatus_20261118/StatusHud_F6_Closeup.png, BattleScreen_F6_Mockup.png, StatusHud_F6_GameScale.png | OpenAI Image API + Pillow | 생성 | 확대·전체·실제 크기 목업. 게임 미적용. |
| ArtDirection/Mockups/F6_PaintedStatus_20261118/compose_edit.py, validation.json, README.md, api-generation.log | Codex / Pillow / imagegen CLI | 생성 | 영역 합성·보존 검증·시각 검토와 실행 기록. |

#### 증거
- ArtDirection/Mockups/F6_PaintedStatus_20261118/api-generation.log
- ArtDirection/Mockups/F6_PaintedStatus_20261118/validation.json
- ArtDirection/Mockups/F6_PaintedStatus_20261118/README.md

#### OpenAI 도구 사용
- 도구 및 사용 위치: 승인된 API 경로의 OpenAI Image API, F6 폴더.
- 사용 방식: 사용자 지정 패널의 면·붓결·테두리 명암을 모델로 다시 그린 뒤 필요한 영역만 합성. 추가 재시도 없음.

#### 주요 프롬프트
- 사용 도구: Codex / OpenAI Image API
- 프롬프트 원문:
> 이거... 괜찮긴 한데,전반적으로 아트가 플레인해서...
> 저 부분만 이미지 생성 모델로 만들어볼 수 있어?
- API 원문: ArtDirection/Mockups/F6_PaintedStatus_20261118/PROMPTS.txt
- 결과 및 실제 활용 방식: 지정 패널만 그림의 질감을 더한 검토용 목업.
- 선정 이유: 단순 픽셀 확장에서 이미지 모델을 통한 미술 표현으로 작업 방식을 변경한 명시적 요청.

#### 미확인 및 후속 확인
- 팀원이 확인할 내용: 붓결 강도, 체력 바 명암, 모델이 바꾼 텍스트 정렬 채택 여부. 코드·씬·생산 에셋 변경 및 Unity CLI 실행 없음.

### [20261118-dragons-ui-mock] 용족 적 2종(해츨링·와이번)과 GPT UI 목업
- 시각: 2026-11-18 (세부 시각 확인 필요)
- 작업자: ParkYeongseo
- 사용 도구: Claude Code (Claude Opus 5.5), NovelAI V5, OpenAI Images API(gpt-image-2.6-sunburst), Meshy API, Codex CLI 0.170.0 + Blender 4.5 MCP, Unity CLI
- 목적 및 적용 위치: 1칸 잡몹 해츨링, 2칸 엘리트 와이번(`Assets/Art/Enemies/Hatchling`, `Wyvern`). HUD 아트 방향 탐색용 목업(`Tools/_out/ui_mock/`, git 제외)
- 사용 방식:
  - 적: NAI 컨셉 → 사용자 선택(해츨링은 v1 "돼지 같음", v2 "다 큰 용" 피드백 후 v3-3, 와이번 3안) → GPT 4면도 → Meshy(1.5k / 3k) → Codex가 Blender에서 날개 포함 리깅(35 / 38뼈)과 Walk·Attack·Hit·Death 제작 → Unity 가져오기·프리팹
  - UI 목업: 실제 GameScene 전투 화면 캡처 + 그림체 참고 시트 + `UIPalette.cs` 토큰과 `docs/ui-flow.md` 배치를 조건으로 GPT 이미지 편집 2장(인게임 다시 칠하기, HUD 부품 시트)
- 결과 및 확인 방법: 성공. Codex 캡처 확인, Unity Play Mode 55° 미리보기(Test_Dragons, git 제외) 캡처 확인. UI 목업은 이미지 직접 검토, 적용은 하지 않음
- 사람의 개입: 컨셉 선택과 해츨링 디자인 피드백 2회, 목업 요청

#### 생성·수정 결과물
| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Assets/Art/Enemies/Hatchling/`, `Assets/Art/Enemies/Wyvern/` | NAI, GPT, Meshy, Codex, Unity CLI | 생성 | FBX, 텍스처, 머티리얼, 컨트롤러, 프리팹. 사용 |
| `Art Prompts/[Enemies] 01_Enemies.txt` 3-A(v3), 3-B | Claude Code | 수정 | 용족 섹션 추가. 사용 |
| `Tools/_out/ui_mock/mock_A_ingame.png`, `mock_B_hudsheet.png` | GPT 이미지 | 생성 | 참고용 목업. 게임에 미적용 |

#### 증거
- `Art3D/Hatchling/Blender/`, `Art3D/Wyvern/Blender/`의 README·Anim_*.png (git 제외 폴더)

#### Unity CLI 사용
- 구현하거나 수행한 작업: 두 유닛 가져오기·프리팹, Test_Dragons 미리보기, GameScene 준비·전투 화면 캡처(전투 시작은 리플렉션 호출, 씬 저장 안 함)
- 활용 방식: `script eval`, `editor.playmode start/stop`, `ScreenCapture`
- 잘된 점: 실제 화면 캡처를 목업 입력으로 써서 배치가 어긋나지 않음
- 잘 안된 점 및 개선 필요: 전투 HUD 마나 수치가 `100/10`과 `0` 두 줄로 잘리는 기존 표시 문제를 관찰(미수정)

#### OpenAI 도구 사용
- 도구 및 사용 위치: Images API(4면도 2장, UI 목업 2장), Codex CLI(리깅 2건)
- 사용 방식: 게임 캡처와 그림체 시트를 함께 입력하고 팔레트 hex·금지 목록을 프롬프트에 명시
- 특별히 활용한 점: 이전에 채택되지 않은 전면 생성 방식 대신 실제 화면을 바탕으로 다시 칠하게 함

#### 미확인 및 후속 확인
- 해츨링·와이번 걷기 동작이 55° 위에서 보면 움직임이 작음. 게임에서 부족하면 Codex 보정 필요
- 마나 수치 줄바꿈 문제(`DefenseHud.cs`) 담당자 확인 필요
