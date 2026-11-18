# 사운드 매니저 — 2026-11-19

## 적용 범위

`SoundManager`는 시작 시 자동 생성되는 화면 계층의 컴포넌트다. BGM 1개, SFX 12개, UI 4개의 독립적인 2D AudioSource를 관리한다. 전체/채널별 볼륨과 음소거는 재생 중인 소리에도 반영하고 PlayerPrefs에 저장한다. 같은 BGM 재요청은 재시작하지 않으며, 활성 씬 전환 시 BGM을 유지하고 SFX/UI를 정리한다. 중복 매니저 생성을 막는다. 새 BGM 음원이나 설정 UI는 만들지 않았다.

기존 BattleSoundPlayer의 독립 소스 풀을 SoundManager 호출로 바꿨다. 기존 타격 클립 참조, 볼륨0.24와 같은 클립 동시6개 제한을 유지한다. 원본 씬/Flow/DefenseBoardView/전투 시뮬레이션/패키지/음원에는 이번 작업으로 수정한 내용이 없다. 해당 파일의 다른 미커밋 변경은 보존했다.

## 실제 오디오 Play Mode 검사 — 통과

Unity 6000.3.28f1의 `GameScene_LeftCoreHud` 복사본에서 실행했고 종료 코드0이다. 테스트용으로 런타임에 만든 무음 AudioClip을 사용해 다음을 검사했다.

- SFX 12개까지 재생하고 13번째 요청 거부, SFX가 차도 UI 재생 가능.
- 클립당 6개 제한, null 클립 처리, StopSfx.
- 전체×채널×클립의 볼륨 곱과 재생 중 변경, 음소거/해제.
- 같은 BGM 요청이 재생 위치를 초기화하지 않음.
- 중복 매니저가 제거되고 AudioSource가 총17개로 유지됨.
- 매니저를 파괴·재생성해 저장된 4종 볼륨과 음소거 상태가 복원됨.
- 실제 씬을 다시 로드해 같은 매니저와 BGM이 살아 있고 짧은 효과음/UI는 정리됨.
- 실제 첫 웨이브의 타격음이 매니저를 통해 재생됨: 관찰 프레임9303, 최대 동시6개, 소스 볼륨0.24. 코어116·생존0으로 웨이브 완료.

검사 중 AudioListener.volume만 일시적으로0으로 두어 무음으로 실행했으며, AudioSource 재생 상태와 볼륨·클립·재생 위치를 검사했다. 출력 장치로 소리를 들어 음질/믹싱을 평가한 것은 아니다. 검사에 사용한 PlayerPrefs 키의 존재 여부/원래 값과 리스너 볼륨은 성공 종료 전에 복구했다(`SOUND_TEST_SETTINGS_RESTORED`). 진단 코드가 원본 Assets에 들어가거나 BGM 파일을 생성하지는 않았다.

## 컴파일과 기존 회귀 검사

- 최초 전체 EditMode: 56/58. `CrowdedBattlesFinishWithoutOverlapOrWallPenetration(20,True)`와 `(40,True)`가 제한 틱 내 종료하지 못했다.
- 사운드 매니저가 없는 별도 복사본에 동일한 전투 소스를 넣어 밀집6사례만 검사: 4/6으로 같은 두 실패 재현. 이번 사운드 작업은 전투 로직을 변경하지 않았다.
- 작업 중 별도로 바뀐 전투 소스/검사를 최신 복사본에 반영한 최종 전체 검사: **61/62**, `(20,True)` 한 건이 남았다. 실패 XML을 보존했으며 전투 코드는 이 작업에서 고치지 않았다. 컴파일 오류는 없다.
- 사운드 파일 2개는 최종 원본과 실제 Play/컴파일 복사본의 바이트 일치를 확인했다. `sound-source-hashes.txt` 참고. 최신 전투 코드 갱신 후에도 이 사운드 파일들은 같았다.

원본 에디터가 열려 있어 `/tmp/be-sound-validation`에서 `-batchmode -nographics -runTests -testPlatform EditMode` 및 그래픽 활성 `-executeMethod SoundPlayValidation.Run`을 실행했다. Play 로그의 Unity 검색 인덱서 ArgumentOutOfRangeException 1건은 함께 기록했다. 실제 Play Mode 검사는 통과했다.

Windows 빌드·실제 장치 청음·새 BGM 제작은 확인 범위가 아니다. 사용법은 [docs/audio.md](../../../docs/audio.md)에 있다.
