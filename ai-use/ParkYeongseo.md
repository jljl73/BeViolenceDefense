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
