# Unity AI SFX 1차 시안 — 2026-11-19

Unity AI Assistant 2.22.0-pre.1의 Audio Generator API에서 **ElevenLabs Sound Effects V2**로 신규 23개를 생성했다. 기존 `Casual_Hit_01.wav` 1개를 포함하면 16종·24파일의 기본 세트다. 밝은 로우폴리 판타지 아트에 맞춰 둥근 충격, 나무·돌의 촉감, 맑은 마법음으로 요청했다.

- 사용용 파일: `ProjectBeViolenceDefense/Assets/Audio/SFX/UnityAI/`
- 원본과 Unity AI 메타데이터: `ProjectBeViolenceDefense/GeneratedAssets/<asset GUID>/`
- [파일별 프롬프트 및 경로](unity-ai-sfx-batch.json)
- [16종 연속 미리듣기](sfx-preview.wav), 소리 사이 0.35초 간격
- 현재 생성 음원은 **시안**이다. 게임의 재생 이벤트에는 아직 연결하지 않았다. 기존 공통 타격음 연결은 유지한다.

## 가공 및 검증

실제 생성 원본은 44.1kHz/16bit 스테레오 WAV다. 사용용 파일은 모노로 변환하고, 10ms RMS 구간 기준으로 앞뒤 무음을 줄인 뒤 피크 -9dBFS로 맞췄다. 시작 2ms·끝 10ms 페이드를 적용했다. 원본과 `.meta` GUID, UnityAI 라벨은 보존했다.

Unity 6000.3.28f1의 임포트·AudioClip 로드·GetData 검사 **23/23 통과**. 23개 모두 무음/플레이스홀더가 아닌 오디오 신호를 확인했고, 사용용 PCM 클리핑은 0개다. 길이는 0.17~1.23초다. 일부 발사음·취소음은 제안한 짧은 길이보다 길게 생성되어, 강제로 소리 중간을 자르지 않고 시안으로 보관했다. 음색 적합성, 반복 피로도, 실제 전투 안의 음량 균형은 청취·플레이 검토가 필요하다. 피크 정규화는 체감 음량을 같게 보장하지 않는다.

## 미리듣기 순서

| 시작(초) | 효과음 | 파일 |
| ---: | --- | --- |
| 0.00 | 일반 명중 | `combat_hit_light_02.wav` |
| 0.74 | 활 발사 | `attack_bow_01.wav` |
| 1.78 | 투석 발사 | `attack_sling_02.wav` |
| 2.49 | 코어 피해 | `core_hit_01.wav` |
| 3.46 | 공격력 | `spell_attack.wav` |
| 4.57 | 회복 | `spell_heal.wav` |
| 5.59 | 이동속도 | `spell_speed.wav` |
| 6.57 | 방어력 | `spell_armor.wav` |
| 7.47 | 벽 배치 | `wall_place_01.wav` |
| 8.36 | 벽 철거 | `wall_remove_01.wav` |
| 9.01 | 확정 | `ui_confirm_01.wav` |
| 9.53 | 취소 | `ui_cancel.wav` |
| 10.75 | 실행 불가 | `ui_denied.wav` |
| 11.62 | 웨이브 시작 | `wave_start.wav` |
| 12.59 | 웨이브 종료 | `wave_clear.wav` |
| 13.45 | 패배 | `game_defeat.wav` |
