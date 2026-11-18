# [20261118-art-3d-pipeline] 결과물 목록

Unity 경로는 `ProjectBeViolenceDefense/` 기준이다. `Art3D/`, `ArtDirection/`, `Tools/`는 로컬 작업 폴더이며 `.gitignore`로 저장소에서 제외했다(사용자 지시).

## 저장소에 포함되는 결과물 (Unity)

| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Assets/Art/Enemies/Goblin_Grunt/` (Walk·Run FBX, Color PNG, .mat, .controller) | Meshy(메시·리깅), Codex+Blender(3k 재베이크), Claude Code+Unity CLI(임포트 설정) | 생성 | 고블린 졸병 3k 버전. 비교용으로 유지, 최종 채택은 1.5k 쪽 권장 |
| `Assets/Art/Enemies/Goblin_Grunt_1500/` (Walk·Run·Combat·HitAlt FBX, Color, .mat, .controller) | Meshy, Codex+Blender(1.5k 재베이크), Claude Code+Unity CLI | 생성 | 고블린 졸병 1.5k. 공격(4)·피격(179 Hit Reaction 1)·쓰러짐(189) 동작. 사용 후보 |
| `Assets/Art/Enemies/Wolf/` (Wolf.fbx, Textures/Wolf_Color.png, .mat, .controller) | Meshy(1.5k 메시), Codex+Blender(4족 리깅·동작 4개), Claude Code(텍스처 추출) | 생성 | 늑대. Walk·Attack·Hit·Death 키프레임 동작. Meshy 자동 리깅은 사람 뼈대를 심어 폐기 |
| `Assets/Art/Enemies/Skeleton/` (Walk·Combat FBX, Color, .mat, .controller) | Meshy(1.5k 메시·리깅·동작), Claude Code+Unity CLI | 생성 | 해골 병사. 사용 후보 |
| `Assets/Art/Heroes/Caster/` (Caster_WithStaff.fbx, Caster_Walk/Combat FBX, Color, .mat, .controller, Caster.prefab) | Meshy(10k 메시·리깅·동작), Codex+Blender(지팡이 손뼈 부착·손목 보정 v2), Claude Code+Unity CLI(프리팹) | 생성 | 주인공 술사. 프리팹은 Caster_WithStaff.fbx 사용(Walk·Attack·Hit, 쓰러짐 제외). 머티리얼 URP/Unlit + 2048 텍스처. Codex FBX의 -8 m 오프셋을 프리팹 래퍼로 상쇄 |
| `Assets/Art/Heroes/Caster_Staff/` (FBX, Color, .mat) | NAI(원화), Meshy(500 메시), Blender(GLB→FBX) | 생성 | 술사 지팡이 소품 |

## 저장소에서 제외(.gitignore)된 확인용·작업 결과물

| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `Assets/Scenes/Test_Goblin60`, `Test_GoblinCompare`, `Test_WolfAnims`, `Test_CasterSkeleton` (.unity), `Test_Goblin60_Ground.mat` | Claude Code+Unity CLI | 생성 | 60기 부하·가독성, 3k/1.5k 비교, 동작 미리보기 씬. 확인용, 미사용 |
| `Assets/Art/**/Preview/*.controller` | Claude Code+Unity CLI | 생성 | 미리보기 씬 전용 반복 재생 컨트롤러. 미사용 |
| `Art3D/<유닛>/` | NAI, OpenAI Images API, Meshy, Codex+Blender | 생성 | 컨셉, GPT 4면 시트, Meshy 원본 GLB, 리깅·동작 원본, Codex 요청서(REQUEST.md)·README·확인 캡처 |
| `ArtDirection/` | OpenAI Images API(Codex 경유), Claude Code | 생성 | 전투 화면 목업 A/B안 4장. 그림체 불일치로 사용자가 반려, 참고용 |
| `Tools/` | Claude Code | 생성 | NAI 원본 출력, `set_openai_key.bat`(키 입력용, 키 미저장) |

## 저장소 밖 결과물

| 파일 또는 위치 | 생성 도구 | 생성·수정 구분 | 수정 내용 및 최종 사용 여부 |
| --- | --- | --- | --- |
| `~/.claude/skills/unit-3d-pipeline/` (SKILL.md, scripts, references, assets) | Claude Code | 생성 | 이 파이프라인을 Claude Code 스킬로 정리. 사용자 PC 로컬 |
