# 사운드

`Assets/02_Scripts/00_Core/Audio/SoundManager.cs`가 시작 시 자동 생성되며 씬이 바뀌어도 유지된다. 씬에 따로 배치할 필요가 없다. 현재 카메라의 AudioListener를 사용한다. 전투 시뮬레이션 클래스에서는 호출하지 않고 화면·UI·연출 코드에서 사용한다.

```csharp
var sound = SoundManager.Instance;
sound.PlayBgm(bgmClip, volume: 0.7f);    // 기본 반복 재생
sound.PlaySfx(effectClip, volume: 0.5f);
sound.PlayUi(clickClip);
sound.StopBgm();
```

현재는 2D 재생이다. BGM 전환은 즉시 이루어지며 같은 클립을 다시 요청하면 재생 위치를 유지한다. BGM 파일은 아직 연결하지 않았다. 없는 클립은 재생하지 않고 `false`를 반환한다. `PlayBgm(null)`은 기존 BGM을 끄지 않으므로 중지는 `StopBgm()`을 사용한다.

## 볼륨과 음소거

```csharp
sound.SetMasterVolume(0.8f);
sound.SetVolume(SoundChannel.Bgm, 0.6f);
sound.SetVolume(SoundChannel.Sfx, 0.9f);
sound.SetVolume(SoundChannel.Ui, 0.7f);
sound.SetMuted(true);
sound.SaveSettings();  // 설정창을 닫거나 슬라이더 조작을 마쳤을 때
```

볼륨 범위는 0~1이고 실제 소스 볼륨은 `전체 × 채널 × 재생 시 지정한 볼륨`이다. 이미 재생 중인 소리도 즉시 반영한다. 설정은 `BeViolenceDefense.Audio.*` PlayerPrefs에 기록하며 명시적 저장 외에도 앱 일시 정지·종료·매니저 파괴 시 저장한다. 음소거는 BGM 재생 위치를 유지하고 새 효과음/UI 재생 요청은 건너뛴다. 설정 UI는 아직 연결하지 않았다.

## 동시 재생과 씬 전환

- BGM 1개, 효과음 12개, UI 4개를 별도로 관리한다. 효과음이 많아도 UI 재생 공간은 남는다.
- `PlaySfx(clip, volume, maxInstances)`와 `PlayUi`의 마지막 인수로 같은 클립의 동시 재생 수를 제한할 수 있다. 기본 0은 채널 풀 한도만 적용한다. 한도가 차면 기존 소리를 끊지 않고 새 요청에 `false`를 반환한다.
- 활성 씬이 바뀌면 효과음과 UI 소리를 정리하고 BGM은 유지한다. `StopSfx`, `StopUi`, `StopAll`로 명시적으로 정리할 수도 있다.
- 배속·조준 슬로우에 따라 피치나 음원 재생 속도를 바꾸지 않는다. 전투 결과와는 무관한 화면 계층의 소리다.

기존 `BattleSoundPlayer`는 씬에 연결된 타격 클립을 넘기는 역할만 한다. 기존 타격 볼륨 **0.24**, 동일 클립 동시 재생 **6개**는 유지한다. 원래 타격음 파일과 씬 참조는 그대로 사용한다.
